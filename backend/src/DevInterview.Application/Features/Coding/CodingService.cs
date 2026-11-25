using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Practice;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevInterview.Application.Features.Coding;

public interface ICodingService
{
    Task<IReadOnlyList<SupportedLanguageDto>> GetLanguagesAsync(CancellationToken ct);
    Task<PagedResult<ChallengeListItemDto>> ListAsync(Guid userId, ChallengeQuery query, CancellationToken ct);
    Task<ChallengeDetailDto> GetAsync(Guid userId, Guid challengeId, CancellationToken ct);
    Task<ChallengeHintDto> GetHintAsync(Guid userId, Guid challengeId, int index, CancellationToken ct);
    Task<ChallengeSolutionDto> GetSolutionAsync(Guid userId, Guid challengeId, CancellationToken ct);
    Task<ChallengeListItemDto?> GetDailyChallengeAsync(Guid userId, CancellationToken ct);
    Task<RunCodeResponse> RunAsync(Guid userId, RunCodeRequest request, CancellationToken ct);
    Task<SubmissionDto> SubmitAsync(Guid userId, SubmitCodeRequest request, CancellationToken ct);
    Task<PagedResult<SubmissionDto>> GetSubmissionsAsync(Guid userId, SubmissionQuery query, CancellationToken ct);
    Task<SubmissionDto> GetSubmissionAsync(Guid userId, Guid submissionId, CancellationToken ct);
}

public sealed class CodingService : ICodingService
{
    public const int MaxSourceLength = 64 * 1024;
    public const int MaxCustomInputLength = 16 * 1024;
    public const int SolutionUnlockAttempts = 3;
    private const int StoredOutputLimit = 8 * 1024;

    private readonly IAppDbContext _db;
    private readonly ICodeExecutionProvider _executor;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly ILogger<CodingService> _logger;

    public CodingService(IAppDbContext db, ICodeExecutionProvider executor, IEntitlementService entitlements,
        IGamificationService gamification, ILogger<CodingService> logger)
    {
        _db = db;
        _executor = executor;
        _entitlements = entitlements;
        _gamification = gamification;
        _logger = logger;
    }

    private IQueryable<CodingChallenge> Published =>
        _db.CodingChallenges.Where(c => c.Status == ContentStatus.Published && c.IsActive && !c.IsDeleted);

    public async Task<IReadOnlyList<SupportedLanguageDto>> GetLanguagesAsync(CancellationToken ct) =>
        await _db.SupportedLanguages.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.DisplayOrder)
            .Select(l => new SupportedLanguageDto(l.Code, l.DisplayName, l.Version)).ToListAsync(ct);

    internal IQueryable<ChallengeListItemDto> Project(IQueryable<CodingChallenge> q, Guid userId) => q.Select(c => new ChallengeListItemDto(
        c.Id, c.Title, c.Slug, c.Category.ToString(), c.Difficulty.ToString(), c.IsPremium,
        _db.CodeSubmissions.Any(s => s.UserId == userId && s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted),
        _db.CodeSubmissions.Any(s => s.UserId == userId && s.ChallengeId == c.Id),
        // Integer arithmetic with a non-zero divisor keeps this portable (no division-by-zero branch on any provider).
        (decimal)(_db.CodeSubmissions.Count(s => s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted) * 1000 /
                  Math.Max(1, _db.CodeSubmissions.Count(s => s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit))) / 10m));

    public async Task<PagedResult<ChallengeListItemDto>> ListAsync(Guid userId, ChallengeQuery query, CancellationToken ct)
    {
        var q = Published.AsNoTracking();
        if (EnumParser.ParseOptional<ChallengeCategory>(query.Category, "category") is { } cat) q = q.Where(c => c.Category == cat);
        if (EnumParser.ParseOptional<ChallengeDifficulty>(query.Difficulty, "difficulty") is { } d) q = q.Where(c => c.Difficulty == d);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(c => c.Title.Contains(query.Search) || c.ProblemStatement.Contains(query.Search));
        q = query.Status?.ToLowerInvariant() switch
        {
            "solved" => q.Where(c => _db.CodeSubmissions.Any(s => s.UserId == userId && s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)),
            "unsolved" => q.Where(c => !_db.CodeSubmissions.Any(s => s.UserId == userId && s.ChallengeId == c.Id && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted)),
            "attempted" => q.Where(c => _db.CodeSubmissions.Any(s => s.UserId == userId && s.ChallengeId == c.Id)),
            _ => q
        };
        q = query.Sort?.ToLowerInvariant() switch
        {
            "title" => q.OrderBy(c => c.Title),
            "newest" => q.OrderByDescending(c => c.CreatedAt),
            _ => q.OrderBy(c => c.Difficulty).ThenBy(c => c.Category).ThenBy(c => c.Title)
        };
        return await Project(q, userId).ToPagedAsync(query, ct);
    }

    private async Task<CodingChallenge> LoadAccessibleAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var challenge = await Published.Include(c => c.TestCases).Include(c => c.StarterCodes).AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Challenge", id);
        if (challenge.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This challenge", ct);
        return challenge;
    }

    private Task<bool> IsSolvedAsync(Guid userId, Guid challengeId, CancellationToken ct) =>
        _db.CodeSubmissions.AnyAsync(s => s.UserId == userId && s.ChallengeId == challengeId && s.Kind == SubmissionKind.Submit && s.Status == SubmissionStatus.Accepted, ct);

    public async Task<ChallengeDetailDto> GetAsync(Guid userId, Guid challengeId, CancellationToken ct)
    {
        var c = await LoadAccessibleAsync(userId, challengeId, ct);
        var solved = await IsSolvedAsync(userId, challengeId, ct);
        var submits = await _db.CodeSubmissions.CountAsync(s => s.UserId == userId && s.ChallengeId == challengeId && s.Kind == SubmissionKind.Submit, ct);
        var samples = c.TestCases.Where(t => t.IsSample).OrderBy(t => t.DisplayOrder)
            .Select(t => new SampleTestCaseDto(t.Id, t.Input, t.ExpectedOutput, t.Explanation)).ToList();
        return new ChallengeDetailDto(c.Id, c.Title, c.Slug, c.Category.ToString(), c.Difficulty.ToString(), c.ProblemStatement,
            c.InputDescription, c.OutputDescription, c.Constraints, c.ExpectedTimeComplexity, c.ExpectedSpaceComplexity,
            Json.StringList(c.HintsJson).Count, samples, c.TestCases.Count(t => !t.IsSample),
            c.StarterCodes.ToDictionary(s => s.LanguageCode, s => s.Code), AllowedLanguages(c), c.TimeLimitMs, c.MemoryLimitMb,
            solved, c.IsPremium, solved || submits >= SolutionUnlockAttempts);
    }

    private static IReadOnlyList<string> AllowedLanguages(CodingChallenge c) =>
        c.AllowedLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task<ChallengeHintDto> GetHintAsync(Guid userId, Guid challengeId, int index, CancellationToken ct)
    {
        var c = await LoadAccessibleAsync(userId, challengeId, ct);
        var hints = Json.StringList(c.HintsJson);
        if (index < 0 || index >= hints.Count) throw new NotFoundException("Hint", index);
        return new ChallengeHintDto(index, hints.Count, hints[index]);
    }

    public async Task<ChallengeSolutionDto> GetSolutionAsync(Guid userId, Guid challengeId, CancellationToken ct)
    {
        var c = await LoadAccessibleAsync(userId, challengeId, ct);
        var solved = await IsSolvedAsync(userId, challengeId, ct);
        var submits = await _db.CodeSubmissions.CountAsync(s => s.UserId == userId && s.ChallengeId == challengeId && s.Kind == SubmissionKind.Submit, ct);
        if (!solved && submits < SolutionUnlockAttempts)
            throw new BusinessRuleException(ErrorCodes.Forbidden, $"The solution unlocks after you solve the challenge or make {SolutionUnlockAttempts} submissions.");
        return new ChallengeSolutionDto(c.Id, c.SolutionExplanation, c.ExpectedTimeComplexity, c.ExpectedSpaceComplexity);
    }

    public async Task<ChallengeListItemDto?> GetDailyChallengeAsync(Guid userId, CancellationToken ct)
    {
        var ids = await Published.Where(c => !c.IsPremium).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
        if (ids.Count == 0) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var id = ids[today.DayNumber % ids.Count];
        return await Project(Published.Where(c => c.Id == id), userId).FirstOrDefaultAsync(ct);
    }

    private void EnsureLanguage(CodingChallenge c, string language)
    {
        if (!AllowedLanguages(c).Contains(language, StringComparer.OrdinalIgnoreCase))
            throw ValidationAppException.For("languageCode", $"'{language}' is not supported for this challenge.");
        if (!_executor.IsAvailable)
            throw new ServiceUnavailableException(ErrorCodes.ExecutionUnavailable, "Code execution is not configured on this server.");
        if (!_executor.SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
            throw new ServiceUnavailableException(ErrorCodes.ExecutionUnavailable, $"The execution sandbox does not support '{language}' yet.");
    }

    public async Task<RunCodeResponse> RunAsync(Guid userId, RunCodeRequest request, CancellationToken ct)
    {
        var c = await LoadAccessibleAsync(userId, request.ChallengeId, ct);
        var language = request.LanguageCode.ToLowerInvariant();
        EnsureLanguage(c, language);
        await _entitlements.ConsumeAsync(userId, UsageFeature.CodeRun, ct);

        var samples = c.TestCases.Where(t => t.IsSample).OrderBy(t => t.DisplayOrder).ToList();
        var inputs = samples.Select(s => s.Input).ToList();
        var hasCustom = request.CustomInput is not null;
        if (hasCustom) inputs.Add(request.CustomInput!);

        var result = await _executor.ExecuteAsync(new ExecutionRequest(language, request.SourceCode, inputs, new ExecutionLimits(c.TimeLimitMs, c.MemoryLimitMb)), ct);
        if (result.InternalError is not null)
            throw new ServiceUnavailableException(ErrorCodes.ExecutionUnavailable, "The execution sandbox failed. Please try again.");
        if (!result.CompiledOk)
            return new RunCodeResponse(SubmissionStatus.CompilationError.ToString(), Text.Truncate(result.CompileOutput, StoredOutputLimit), Array.Empty<TestResultDto>(), 0, samples.Count);

        var results = new List<TestResultDto>();
        for (var i = 0; i < samples.Count && i < result.Cases.Count; i++)
        {
            var (status, passed) = Evaluate(result.Cases[i], samples[i].ExpectedOutput);
            results.Add(new TestResultDto(i + 1, true, false, samples[i].Input, samples[i].ExpectedOutput,
                Text.Truncate(result.Cases[i].Stdout, StoredOutputLimit), Text.Truncate(result.Cases[i].Stderr, StoredOutputLimit), passed, status.ToString(), result.Cases[i].RuntimeMs));
        }
        if (hasCustom && result.Cases.Count > samples.Count)
        {
            var custom = result.Cases[samples.Count];
            var status = Map(custom.Outcome) ?? SubmissionStatus.Accepted;
            results.Add(new TestResultDto(results.Count + 1, false, true, request.CustomInput, null, Text.Truncate(custom.Stdout, StoredOutputLimit),
                Text.Truncate(custom.Stderr, StoredOutputLimit), status == SubmissionStatus.Accepted, status.ToString(), custom.RuntimeMs));
        }

        var sampleResults = results.Where(r => !r.IsCustom).ToList();
        var overall = Overall(sampleResults.Select(r => Enum.Parse<SubmissionStatus>(r.Status)).ToList());
        var submission = new CodeSubmission
        {
            UserId = userId, ChallengeId = c.Id, LanguageCode = language, SourceCode = request.SourceCode, Kind = SubmissionKind.Run,
            Status = overall, PassedCount = sampleResults.Count(r => r.Passed), TotalCount = sampleResults.Count,
            MaxRuntimeMs = results.Count == 0 ? null : results.Max(r => r.RuntimeMs)
        };
        _db.CodeSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);
        await _gamification.RecordActivityAsync(userId, ActivityType.ChallengeSubmitted, c.Id, null, 0, ct);
        return new RunCodeResponse(overall.ToString(), null, results, submission.PassedCount, submission.TotalCount);
    }

    public async Task<SubmissionDto> SubmitAsync(Guid userId, SubmitCodeRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.IdempotencyKey == request.IdempotencyKey)
                .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
            if (existing is { } existingId) return await GetSubmissionAsync(userId, existingId, ct);
        }

        var c = await LoadAccessibleAsync(userId, request.ChallengeId, ct);
        var language = request.LanguageCode.ToLowerInvariant();
        EnsureLanguage(c, language);
        await _entitlements.ConsumeAsync(userId, UsageFeature.CodeSubmit, ct);
        var wasSolved = await IsSolvedAsync(userId, c.Id, ct);

        var cases = c.TestCases.OrderBy(t => t.IsSample ? 0 : 1).ThenBy(t => t.DisplayOrder).ToList();
        var result = await _executor.ExecuteAsync(new ExecutionRequest(language, request.SourceCode, cases.Select(t => t.Input).ToList(),
            new ExecutionLimits(c.TimeLimitMs, c.MemoryLimitMb)), ct);
        if (result.InternalError is not null)
            throw new ServiceUnavailableException(ErrorCodes.ExecutionUnavailable, "The execution sandbox failed. Please try again.");

        var submission = new CodeSubmission
        {
            UserId = userId, ChallengeId = c.Id, LanguageCode = language, SourceCode = request.SourceCode, Kind = SubmissionKind.Submit,
            TotalCount = cases.Count, IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };
        if (!result.CompiledOk)
        {
            submission.Status = SubmissionStatus.CompilationError;
            submission.CompileOutput = Text.Truncate(result.CompileOutput, StoredOutputLimit);
        }
        else
        {
            var statuses = new List<SubmissionStatus>();
            for (var i = 0; i < cases.Count; i++)
            {
                var caseResult = i < result.Cases.Count ? result.Cases[i] : new CaseResult(CaseOutcome.InternalError, "", "", -1, 0);
                var (status, passed) = Evaluate(caseResult, cases[i].ExpectedOutput);
                statuses.Add(status);
                submission.Results.Add(new CodeSubmissionResult
                {
                    TestCaseId = cases[i].Id, Ordinal = i + 1, IsSample = cases[i].IsSample, Status = status, Passed = passed,
                    RuntimeMs = caseResult.RuntimeMs,
                    // Hidden test data and outputs are never stored or returned, so they cannot be scraped.
                    ActualOutput = cases[i].IsSample ? Text.Truncate(caseResult.Stdout, StoredOutputLimit) : null,
                    ErrorOutput = cases[i].IsSample ? Text.Truncate(caseResult.Stderr, StoredOutputLimit) : null
                });
            }
            submission.PassedCount = submission.Results.Count(r => r.Passed);
            submission.Status = Overall(statuses);
            submission.MaxRuntimeMs = submission.Results.Count == 0 ? null : submission.Results.Max(r => r.RuntimeMs);
        }

        _db.CodeSubmissions.Add(submission);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (submission.IdempotencyKey is not null)
        {
            // A concurrent request with the same idempotency key won the race; return that submission.
            _db.ResetTracking();
            var winner = await _db.CodeSubmissions.Where(s => s.UserId == userId && s.IdempotencyKey == submission.IdempotencyKey).Select(s => s.Id).FirstAsync(ct);
            return await GetSubmissionAsync(userId, winner, ct);
        }

        var xp = 0;
        if (submission.Status == SubmissionStatus.Accepted)
        {
            xp += await _gamification.AwardXpAsync(userId, XpRules.ChallengeSolved(c.Difficulty), XpSourceType.ChallengeSolved,
                $"challenge:{c.Id}", $"Solved {c.Title}", ct);
            await _gamification.RecordActivityAsync(userId, wasSolved ? ActivityType.ChallengeSubmitted : ActivityType.ChallengeSolved, c.Id, null, 120, ct);
        }
        else
        {
            await _gamification.RecordActivityAsync(userId, ActivityType.ChallengeSubmitted, c.Id, null, 120, ct);
        }
        _logger.LogInformation("Submission {SubmissionId} for challenge {ChallengeId}: {Status} ({Passed}/{Total})",
            submission.Id, c.Id, submission.Status, submission.PassedCount, submission.TotalCount);
        return ToDto(submission, c.Title, cases.ToDictionary(t => t.Id), xp, includeSource: false);
    }

    public async Task<PagedResult<SubmissionDto>> GetSubmissionsAsync(Guid userId, SubmissionQuery query, CancellationToken ct)
    {
        var q = _db.CodeSubmissions.AsNoTracking().Where(s => s.UserId == userId);
        if (query.ChallengeId is { } cid) q = q.Where(s => s.ChallengeId == cid);
        var page = await q.OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s, Title = s.Challenge.Title })
            .ToPagedAsync(query, ct);
        return page.Map(x => ToDto(x.s, x.Title, new Dictionary<Guid, ChallengeTestCase>(), 0, includeSource: false));
    }

    public async Task<SubmissionDto> GetSubmissionAsync(Guid userId, Guid submissionId, CancellationToken ct)
    {
        var s = await _db.CodeSubmissions.AsNoTracking().Include(x => x.Results).Include(x => x.Challenge).ThenInclude(c => c.TestCases)
            .FirstOrDefaultAsync(x => x.Id == submissionId && x.UserId == userId, ct) ?? throw new NotFoundException("Submission", submissionId);
        return ToDto(s, s.Challenge.Title, s.Challenge.TestCases.ToDictionary(t => t.Id), 0, includeSource: true);
    }

    private static SubmissionDto ToDto(CodeSubmission s, string title, IReadOnlyDictionary<Guid, ChallengeTestCase> cases, int xp, bool includeSource) =>
        new(s.Id, s.ChallengeId, title, s.LanguageCode, s.Kind.ToString(), s.Status.ToString(), s.PassedCount, s.TotalCount, s.MaxRuntimeMs,
            s.CreatedAt, s.CompileOutput,
            s.Results.OrderBy(r => r.Ordinal).Select(r =>
            {
                var tc = r.TestCaseId is { } id && cases.TryGetValue(id, out var t) ? t : null;
                var visible = r.IsSample && tc is not null;
                return new TestResultDto(r.Ordinal, r.IsSample, false, visible ? tc!.Input : null, visible ? tc!.ExpectedOutput : null,
                    r.ActualOutput, r.ErrorOutput, r.Passed, r.Status.ToString(), r.RuntimeMs);
            }).ToList(),
            xp, includeSource ? s.SourceCode : null);

    internal static SubmissionStatus? Map(CaseOutcome outcome) => outcome switch
    {
        CaseOutcome.RuntimeError => SubmissionStatus.RuntimeError,
        CaseOutcome.TimeLimitExceeded => SubmissionStatus.TimeLimitExceeded,
        CaseOutcome.MemoryLimitExceeded => SubmissionStatus.MemoryLimitExceeded,
        CaseOutcome.InternalError => SubmissionStatus.InternalError,
        _ => null
    };

    internal static (SubmissionStatus Status, bool Passed) Evaluate(CaseResult result, string expected)
    {
        if (Map(result.Outcome) is { } failure) return (failure, false);
        var passed = OutputComparer.Matches(result.Stdout, expected);
        return (passed ? SubmissionStatus.Accepted : SubmissionStatus.WrongAnswer, passed);
    }

    /// <summary>The first non-accepted case decides the verdict, mirroring common judges.</summary>
    internal static SubmissionStatus Overall(IReadOnlyList<SubmissionStatus> statuses) =>
        statuses.Count == 0 ? SubmissionStatus.InternalError : statuses.FirstOrDefault(s => s != SubmissionStatus.Accepted, SubmissionStatus.Accepted);
}

public sealed class RunCodeRequestValidator : AbstractValidator<RunCodeRequest>
{
    public RunCodeRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SourceCode).NotEmpty().MaximumLength(CodingService.MaxSourceLength);
        RuleFor(x => x.CustomInput).MaximumLength(CodingService.MaxCustomInputLength);
    }
}

public sealed class SubmitCodeRequestValidator : AbstractValidator<SubmitCodeRequest>
{
    public SubmitCodeRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SourceCode).NotEmpty().MaximumLength(CodingService.MaxSourceLength);
        RuleFor(x => x.IdempotencyKey).MaximumLength(64);
    }
}
