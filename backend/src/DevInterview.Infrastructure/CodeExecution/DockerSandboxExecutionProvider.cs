using System.Diagnostics;
using System.Text;
using DevInterview.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.CodeExecution;

public sealed class SandboxLanguage
{
    public string Image { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    /// <summary>Shell command run once inside /work; non-zero exit = compilation error. Empty = interpreted.</summary>
    public string? Compile { get; set; }
    /// <summary>Shell command that runs the program, reading stdin.</summary>
    public string Run { get; set; } = string.Empty;
    public int CompileTimeoutSeconds { get; set; } = 30;
    /// <summary>Extra milliseconds added to the time limit for slow-starting runtimes (JVM, .NET).</summary>
    public int StartupAllowanceMs { get; set; }
}

public sealed class CodeExecutionOptions
{
    public const string Section = "CodeExecution";
    /// <summary>"Docker", "Judge0" or "Disabled".</summary>
    public string Provider { get; set; } = "Disabled";
    public string DockerPath { get; set; } = "docker";
    public string WorkRoot { get; set; } = Path.Combine(Path.GetTempPath(), "devinterview-sandbox");
    public int MaxConcurrentExecutions { get; set; } = 4;
    public double CpuLimit { get; set; } = 1.0;
    public int PidsLimit { get; set; } = 64;
    public int OutputLimitBytes { get; set; } = 64 * 1024;
    public Dictionary<string, SandboxLanguage> Languages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Judge0Options Judge0 { get; set; } = new();
}

/// <summary>
/// Runs untrusted code in a throw-away Docker container per execution: no network, read-only root filesystem,
/// non-root user, all capabilities dropped, no-new-privileges, memory/CPU/PID limits, per-test wall-clock timeouts
/// and a hard container deadline. The program is compiled once and run against every input inside the same container.
/// The API process only orchestrates the Docker CLI; it never executes user code itself.
/// </summary>
public sealed class DockerSandboxExecutionProvider : ICodeExecutionProvider, IDisposable
{
    private const string CaseMarker = "@@DI_CASE";
    private const string CompileMarker = "@@DI_COMPILE_FAILED";
    private readonly CodeExecutionOptions _options;
    private readonly ILogger<DockerSandboxExecutionProvider> _logger;
    private readonly SemaphoreSlim _gate;

    public DockerSandboxExecutionProvider(IOptions<CodeExecutionOptions> options, ILogger<DockerSandboxExecutionProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentExecutions));
    }

    public string Name => "Docker";
    public bool IsAvailable => _options.Languages.Count > 0;
    public IReadOnlyCollection<string> SupportedLanguages => _options.Languages.Keys.ToList();

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct)
    {
        if (!_options.Languages.TryGetValue(request.LanguageCode, out var language))
            return ExecutionResult.Failure($"Language '{request.LanguageCode}' is not configured.");

        await _gate.WaitAsync(ct);
        var id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(_options.WorkRoot, id);
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "in"));
            await File.WriteAllTextAsync(Path.Combine(dir, language.SourceFile), request.SourceCode, ct);
            for (var i = 0; i < request.Inputs.Count; i++)
                await File.WriteAllTextAsync(Path.Combine(dir, "in", $"{i + 1}.txt"), request.Inputs[i], ct);
            // Support files (SQL runner, C# compile script) shipped next to the binaries.
            var support = Path.Combine(AppContext.BaseDirectory, "CodeExecution");
            if (Directory.Exists(support))
                foreach (var file in Directory.GetFiles(support).Where(f => f.EndsWith(".py") || f.EndsWith(".sh")))
                    File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
            var timeLimitMs = request.Limits.TimeLimitMs + language.StartupAllowanceMs;
            await File.WriteAllTextAsync(Path.Combine(dir, "runner.sh"), BuildRunner(language, request.Inputs.Count, timeLimitMs), ct);
            MakeWorldReadable(dir);

            var deadline = TimeSpan.FromSeconds(language.CompileTimeoutSeconds + 10) +
                           TimeSpan.FromMilliseconds((timeLimitMs + 1500.0) * Math.Max(1, request.Inputs.Count));
            var memory = Math.Clamp(request.Limits.MemoryLimitMb, 32, 2048);
            var name = $"di-sbx-{id}";
            var args = new List<string>
            {
                "run", "--rm", "-i", "--name", name, "--network", "none", "--memory", $"{memory}m", "--memory-swap", $"{memory}m",
                "--cpus", _options.CpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), "--pids-limit", _options.PidsLimit.ToString(),
                "--read-only", "--tmpfs", "/work:rw,exec,nosuid,size=256m,mode=1777", "--tmpfs", "/tmp:rw,exec,nosuid,size=64m,mode=1777",
                "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "65534:65534",
                "-e", "HOME=/tmp", "-e", "DOTNET_CLI_HOME=/tmp", "-e", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1", "-e", "DOTNET_NOLOGO=1", "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                "-v", $"{dir}:/sandbox:ro", "-w", "/work", language.Image, "sh", "/sandbox/runner.sh"
            };
            var (exit, stdout, stderr, timedOut) = await RunProcessAsync(_options.DockerPath, args, deadline, name, ct);
            if (timedOut) _logger.LogWarning("Sandbox {Name} exceeded its hard deadline and was killed", name);
            if (exit == 125 || (exit != 0 && !stdout.Contains(CaseMarker) && !stdout.Contains(CompileMarker) && request.Inputs.Count > 0 && !timedOut))
            {
                _logger.LogError("Sandbox failed to start (exit {Exit}): {Error}", exit, Truncate(stderr, 500));
                return ExecutionResult.Failure("The sandbox could not start.");
            }
            return Parse(stdout, request.Inputs.Count, timeLimitMs, timedOut);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sandbox execution failed");
            return ExecutionResult.Failure("Execution failed.");
        }
        finally
        {
            _gate.Release();
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private string BuildRunner(SandboxLanguage lang, int count, int timeLimitMs)
    {
        var limitSeconds = (timeLimitMs / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var limit = _options.OutputLimitBytes;
        var sb = new StringBuilder();
        sb.Append("#!/bin/sh\n");
        sb.Append("cp -r /sandbox/. /work/ 2>/dev/null\ncd /work\n");
        if (!string.IsNullOrWhiteSpace(lang.Compile))
        {
            sb.Append($"timeout -s KILL {lang.CompileTimeoutSeconds} sh -c '{Escape(lang.Compile)}' > /work/compile.txt 2>&1\n");
            sb.Append($"if [ $? -ne 0 ]; then echo '{CompileMarker}'; head -c {limit} /work/compile.txt | base64 -w0; echo; exit 0; fi\n");
        }
        sb.Append($"i=1\nwhile [ $i -le {count} ]; do\n");
        sb.Append("  s=$(date +%s%N)\n");
        sb.Append($"  timeout -s KILL {limitSeconds} sh -c '{Escape(lang.Run)}' < /sandbox/in/$i.txt > /work/out.txt 2> /work/err.txt\n");
        sb.Append("  c=$?\n  e=$(date +%s%N)\n");
        sb.Append($"  echo \"{CaseMarker} $i $c $(( (e - s) / 1000000 ))\"\n");
        sb.Append($"  head -c {limit} /work/out.txt | base64 -w0; echo\n");
        sb.Append($"  head -c {limit} /work/err.txt | base64 -w0; echo\n");
        sb.Append("  i=$((i + 1))\ndone\n");
        return sb.ToString();
    }

    private static string Escape(string command) => command.Replace("'", "'\"'\"'");

    internal static ExecutionResult Parse(string stdout, int count, int timeLimitMs, bool killed)
    {
        var lines = stdout.Replace("\r\n", "\n").Split('\n');
        var compileIndex = Array.IndexOf(lines, CompileMarker);
        if (compileIndex >= 0)
            return ExecutionResult.CompileError(Decode(compileIndex + 1 < lines.Length ? lines[compileIndex + 1] : ""));

        var cases = new CaseResult[count];
        for (var li = 0; li < lines.Length; li++)
        {
            if (!lines[li].StartsWith(CaseMarker, StringComparison.Ordinal)) continue;
            var parts = lines[li].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !int.TryParse(parts[1], out var index) || index < 1 || index > count) continue;
            var exit = int.TryParse(parts[2], out var e) ? e : -1;
            var ms = int.TryParse(parts[3], out var m) ? m : 0;
            var output = li + 1 < lines.Length ? Decode(lines[li + 1]) : "";
            var error = li + 2 < lines.Length ? Decode(lines[li + 2]) : "";
            var outcome = exit switch
            {
                0 => CaseOutcome.Ok,
                137 when ms >= timeLimitMs - 50 => CaseOutcome.TimeLimitExceeded,
                137 => CaseOutcome.MemoryLimitExceeded,
                124 => CaseOutcome.TimeLimitExceeded,
                _ => CaseOutcome.RuntimeError
            };
            cases[index - 1] = new CaseResult(outcome, output, error, exit, Math.Min(ms, timeLimitMs));
        }
        for (var i = 0; i < count; i++)
            cases[i] ??= new CaseResult(killed ? CaseOutcome.TimeLimitExceeded : CaseOutcome.InternalError, "", "", -1, timeLimitMs);
        return new ExecutionResult(true, null, cases);
    }

    private static string Decode(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return string.Empty;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim())); }
        catch (FormatException) { return string.Empty; }
    }

    private static void MakeWorldReadable(string dir)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(dir, (UnixFileMode)0b111_101_101);
        foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(d, (UnixFileMode)0b111_101_101);
        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(f, (UnixFileMode)0b110_100_100);
    }

    private async Task<(int Exit, string Stdout, string Stderr, bool TimedOut)> RunProcessAsync(
        string file, IEnumerable<string> args, TimeSpan deadline, string containerName, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start docker.");
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(deadline);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            await KillContainerAsync(containerName);
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (!timedOut) throw;
        }
        string stdout = "", stderr = "";
        try
        {
            stdout = await stdoutTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            stderr = await stderrTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        return (process.HasExited ? process.ExitCode : -1, stdout, stderr, timedOut);
    }

    private async Task KillContainerAsync(string name)
    {
        try
        {
            using var kill = Process.Start(new ProcessStartInfo(_options.DockerPath, $"rm -f {name}") { RedirectStandardOutput = true, RedirectStandardError = true });
            if (kill is not null) await kill.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove sandbox container {Name}", name);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public void Dispose() => _gate.Dispose();
}
