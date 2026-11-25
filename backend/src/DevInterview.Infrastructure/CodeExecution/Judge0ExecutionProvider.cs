using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using DevInterview.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.CodeExecution;

public sealed class Judge0Options
{
    public string? BaseUrl { get; set; }
    /// <summary>Optional auth token (X-Auth-Token header). Supplied via secrets.</summary>
    public string? AuthToken { get; set; }
    /// <summary>Map of DevInterview language code to Judge0 language id (e.g. "python": 71).</summary>
    public Dictionary<string, int> LanguageIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// External sandbox provider for a self-hosted Judge0 CE instance (isolate-based sandboxing).
/// Each input is submitted as a separate batch entry with the same source.
/// </summary>
public sealed class Judge0ExecutionProvider : ICodeExecutionProvider
{
    private readonly HttpClient _http;
    private readonly Judge0Options _options;
    private readonly ILogger<Judge0ExecutionProvider> _logger;

    public Judge0ExecutionProvider(HttpClient http, IOptions<CodeExecutionOptions> options, ILogger<Judge0ExecutionProvider> logger)
    {
        _http = http;
        _options = options.Value.Judge0;
        _logger = logger;
    }

    public string Name => "Judge0";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.BaseUrl) && _options.LanguageIds.Count > 0;
    public IReadOnlyCollection<string> SupportedLanguages => _options.LanguageIds.Keys.ToList();

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct)
    {
        if (!_options.LanguageIds.TryGetValue(request.LanguageCode, out var languageId))
            return ExecutionResult.Failure("Language not supported by Judge0 configuration.");
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        var submissions = new JsonArray();
        foreach (var input in request.Inputs.DefaultIfEmpty(string.Empty))
        {
            submissions.Add(new JsonObject
            {
                ["language_id"] = languageId,
                ["source_code"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.SourceCode)),
                ["stdin"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
                ["cpu_time_limit"] = request.Limits.TimeLimitMs / 1000.0,
                ["memory_limit"] = request.Limits.MemoryLimitMb * 1024,
                ["enable_network"] = false
            });
        }
        try
        {
            using var create = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/submissions/batch?base64_encoded=true")
            {
                Content = JsonContent.Create(new JsonObject { ["submissions"] = submissions })
            };
            if (!string.IsNullOrEmpty(_options.AuthToken)) create.Headers.Add("X-Auth-Token", _options.AuthToken);
            using var createResponse = await _http.SendAsync(create, ct);
            createResponse.EnsureSuccessStatusCode();
            var tokens = (await createResponse.Content.ReadFromJsonAsync<JsonArray>(cancellationToken: ct))!
                .Select(n => n!["token"]!.GetValue<string>()).ToList();

            var deadline = DateTime.UtcNow.AddSeconds(30 + request.Inputs.Count * (request.Limits.TimeLimitMs / 1000.0 + 2));
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, ct);
                using var poll = new HttpRequestMessage(HttpMethod.Get,
                    $"{baseUrl}/submissions/batch?tokens={string.Join(',', tokens)}&base64_encoded=true&fields=status_id,stdout,stderr,compile_output,time,exit_code");
                if (!string.IsNullOrEmpty(_options.AuthToken)) poll.Headers.Add("X-Auth-Token", _options.AuthToken);
                using var pollResponse = await _http.SendAsync(poll, ct);
                pollResponse.EnsureSuccessStatusCode();
                var results = (await pollResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct))!["submissions"]!.AsArray();
                if (results.Any(r => r!["status_id"]!.GetValue<int>() <= 2)) continue; // 1 = queued, 2 = processing

                var first = results[0]!;
                if (first["status_id"]!.GetValue<int>() == 6)
                    return ExecutionResult.CompileError(Decode(first["compile_output"]));
                return new ExecutionResult(true, null, results.Take(request.Inputs.Count).Select(r =>
                {
                    var status = r!["status_id"]!.GetValue<int>();
                    var outcome = status switch
                    {
                        3 or 4 => CaseOutcome.Ok, // accepted / wrong answer: comparison is done by DevInterview
                        5 => CaseOutcome.TimeLimitExceeded,
                        >= 7 and <= 12 => CaseOutcome.RuntimeError,
                        _ => CaseOutcome.InternalError
                    };
                    var seconds = double.TryParse(r["time"]?.GetValue<string>(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0;
                    return new CaseResult(outcome, Decode(r["stdout"]), Decode(r["stderr"]), r["exit_code"]?.GetValue<int?>() ?? 0, (int)(seconds * 1000));
                }).ToList());
            }
            return ExecutionResult.Failure("Judge0 did not finish in time.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NullReferenceException)
        {
            _logger.LogError(ex, "Judge0 execution failed");
            return ExecutionResult.Failure("Judge0 is unavailable.");
        }
    }

    private static string Decode(JsonNode? node)
    {
        var value = node?.GetValue<string>();
        return string.IsNullOrEmpty(value) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(value.Replace("\n", "")));
    }
}

/// <summary>Used when no sandbox is configured: code execution endpoints return 503 with a clear message.</summary>
public sealed class DisabledExecutionProvider : ICodeExecutionProvider
{
    public string Name => "Disabled";
    public bool IsAvailable => false;
    public IReadOnlyCollection<string> SupportedLanguages => Array.Empty<string>();
    public Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct) =>
        Task.FromResult(ExecutionResult.Failure("Code execution is disabled."));
}
