using System.Text.Json.Nodes;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Ai;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevInterview.Application.Features.Ai;

public interface IAiToolsService
{
    Task<AiStatusDto> GetStatusAsync(Guid userId, CancellationToken ct);
    Task<AnswerEvaluationDto> EvaluateAnswerAsync(Guid userId, EvaluateAnswerRequest request, CancellationToken ct);
    Task<PagedResult<AnswerEvaluationSummaryDto>> GetEvaluationsAsync(Guid userId, PageQuery query, CancellationToken ct);
    Task<AnswerEvaluationDto> GetEvaluationAsync(Guid userId, Guid id, CancellationToken ct);
    Task<CodeReviewDto> ReviewCodeAsync(Guid userId, ReviewCodeRequest request, CancellationToken ct);
    Task<CodeExplanationDto> ExplainCodeAsync(Guid userId, ExplainCodeRequest request, CancellationToken ct);
    Task<GeneratedQuestionsResponse> GenerateQuestionsAsync(Guid userId, GenerateQuestionsRequest request, CancellationToken ct);
}

public sealed class AiToolsService : IAiToolsService
{
    public const string ReviewDisclaimer =
        "AI code review is an automated aid. It can miss issues or be wrong and does not replace a professional security audit or human review.";
    public static readonly string[] ReviewLanguages = { "csharp", "javascript", "typescript", "python", "sql", "java", "kotlin", "cpp" };
    private static readonly string[] Severities = { "critical", "high", "medium", "low", "info" };

    private readonly IAppDbContext _db;
    private readonly IAiOrchestrator _ai;
    private readonly IPromptProvider _prompts;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly AiOptions _options;

    public AiToolsService(IAppDbContext db, IAiOrchestrator ai, IPromptProvider prompts, IEntitlementService entitlements,
        IGamificationService gamification, IOptions<AiOptions> options)
    {
        _db = db;
        _ai = ai;
        _prompts = prompts;
        _entitlements = entitlements;
        _gamification = gamification;
        _options = options.Value;
    }

    public async Task<AiStatusDto> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var premium = await _entitlements.IsPremiumAsync(userId, ct);
        var limit = await _entitlements.GetLimitAsync(userId, UsageFeature.AiAnswerEvaluation, ct) ?? 0;
        var used = await _entitlements.GetUsageTodayAsync(userId, UsageFeature.AiAnswerEvaluation, ct);
        var reason = !_ai.IsAvailable ? "AI is not configured on this server." : !premium ? "AI features require Premium." : null;
        return new AiStatusDto(_ai.IsAvailable && premium, _ai.ActiveProviderName, reason, Math.Max(0, limit - used), premium);
    }

    private void EnsureAvailable()
    {
        if (!_ai.IsAvailable)
            throw new ServiceUnavailableException(ErrorCodes.AiUnavailable, "AI features are not configured on this server.");
    }

    public async Task<AnswerEvaluationDto> EvaluateAnswerAsync(Guid userId, EvaluateAnswerRequest request, CancellationToken ct)
    {
        EnsureAvailable();
        string questionText;
        string? reference = null;
        if (request.QuestionId is { } qid)
        {
            var q = await _db.Questions.AsNoTracking().Include(x => x.Answer)
                .FirstOrDefaultAsync(x => x.Id == qid && x.Status == ContentStatus.Published && !x.IsDeleted, ct)
                ?? throw new NotFoundException("Question", qid);
            questionText = $"{q.Title}\n\n{q.Body}" + (q.CodeSnippet is null ? "" : $"\n\n```{q.CodeLanguage}\n{q.CodeSnippet}\n```");
            reference = q.Answer?.ReferenceAnswer ?? q.Answer?.DetailedExplanation;
        }
        else
        {
            questionText = request.QuestionText!.Trim();
        }
        await _entitlements.ConsumeAsync(userId, UsageFeature.AiAnswerEvaluation, ct);

        var user = string.Join("\n\n",
            UntrustedContent.Wrap("question", questionText, 4000),
            reference is null ? "No reference answer is available; rely on expert knowledge." : $"<reference_answer>\n{Text.Truncate(reference, 6000)}\n</reference_answer>",
            UntrustedContent.Wrap("candidate_answer", request.Answer, _options.MaxInputCharacters),
            "Grade the candidate answer now.");
        var json = await _ai.CompleteJsonAsync(AiFeature.AnswerEvaluation, userId,
            new AiCompletionRequest($"{await _prompts.GetAsync(DefaultPrompts.AnswerEvaluation, ct)}\n\n{UntrustedContent.Guard}",
                new[] { new AiMessage(AiRole.User, user) }, 3000), ct);

        var parsed = ParseEvaluation(json, request.Answer);
        var entity = new AnswerEvaluation
        {
            UserId = userId, QuestionId = request.QuestionId, QuestionText = Text.Truncate(questionText, 4000),
            UserAnswer = Text.Truncate(request.Answer, _options.MaxInputCharacters), OverallScore = parsed.OverallScore,
            ResultJson = Json.Serialize(parsed)
        };
        _db.AnswerEvaluations.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _gamification.RecordActivityAsync(userId, ActivityType.AnswerEvaluated, entity.Id, null, 120, ct);
        return ToDto(entity, parsed);
    }

    /// <summary>Validated, clamped evaluation payload (also the stored JSON shape).</summary>
    internal sealed record EvaluationPayload(int OverallScore, int TechnicalAccuracy, int Completeness, int Clarity, int PracticalExamples,
        List<string> Strengths, List<string> MissingConcepts, List<string> IncorrectStatements, List<string> Suggestions,
        string ImprovedAnswer, string WhyBetter, bool ScoreCappedForLength);

    internal static EvaluationPayload ParseEvaluation(JsonObject json, string answer)
    {
        var accuracy = AnswerHeuristics.ApplyCap(AiJson.Score(json, "technicalAccuracy"), answer);
        var completeness = AnswerHeuristics.ApplyCap(AiJson.Score(json, "completeness"), answer);
        var clarity = AnswerHeuristics.ApplyCap(AiJson.Score(json, "clarity"), answer);
        var practical = AnswerHeuristics.ApplyCap(AiJson.Score(json, "practicalExamples"), answer);
        var aiOverall = AiJson.Score(json, "overallScore", (accuracy + completeness + clarity) / 3);
        var overall = AnswerHeuristics.ApplyCap(aiOverall, answer);
        var incorrect = AiJson.StrList(json, "incorrectStatements");
        // An answer with factual errors cannot be scored as excellent.
        if (incorrect.Count > 0) overall = Math.Min(overall, 75);
        var improved = AiJson.Str(json, "improvedAnswer", 6000);
        if (string.IsNullOrWhiteSpace(improved)) throw new ServiceUnavailableException(ErrorCodes.AiInvalidResponse, "The AI evaluation was incomplete. Please try again.");
        return new EvaluationPayload(overall, accuracy, completeness, clarity, practical,
            AiJson.StrList(json, "strengths"), AiJson.StrList(json, "missingConcepts"), incorrect, AiJson.StrList(json, "suggestions"),
            improved, AiJson.Str(json, "whyBetter", 1500), overall < AnswerHeuristics.Clamp(aiOverall));
    }

    private static AnswerEvaluationDto ToDto(AnswerEvaluation e, EvaluationPayload p) => new(
        e.Id, e.QuestionId, e.QuestionText, e.UserAnswer, p.OverallScore, p.TechnicalAccuracy, p.Completeness, p.Clarity,
        p.PracticalExamples, p.Strengths, p.MissingConcepts, p.IncorrectStatements, p.Suggestions, p.ImprovedAnswer, p.WhyBetter,
        p.ScoreCappedForLength, e.CreatedAt);

    public async Task<PagedResult<AnswerEvaluationSummaryDto>> GetEvaluationsAsync(Guid userId, PageQuery query, CancellationToken ct) =>
        await _db.AnswerEvaluations.AsNoTracking().Where(e => e.UserId == userId).OrderByDescending(e => e.CreatedAt)
            .Select(e => new AnswerEvaluationSummaryDto(e.Id, e.QuestionText, e.OverallScore, e.CreatedAt)).ToPagedAsync(query, ct);

    public async Task<AnswerEvaluationDto> GetEvaluationAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var e = await _db.AnswerEvaluations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
                ?? throw new NotFoundException("Evaluation", id);
        var p = Json.Deserialize<EvaluationPayload>(e.ResultJson) ?? throw new NotFoundException("Evaluation", id);
        return ToDto(e, p);
    }

    public async Task<CodeReviewDto> ReviewCodeAsync(Guid userId, ReviewCodeRequest request, CancellationToken ct)
    {
        EnsureAvailable();
        var language = request.LanguageCode.ToLowerInvariant();
        await _entitlements.ConsumeAsync(userId, UsageFeature.AiCodeReview, ct);
        var user = string.Join("\n\n", $"Language: {language}",
            string.IsNullOrWhiteSpace(request.Context) ? "" : UntrustedContent.Wrap("context", request.Context, 1500),
            UntrustedContent.Wrap("candidate_code", AddLineNumbers(request.Code), _options.MaxInputCharacters + 4000),
            "Line numbers were added for reference only; omit them from optimizedCode. Review the code now.");
        var json = await _ai.CompleteJsonAsync(AiFeature.CodeReview, userId,
            new AiCompletionRequest($"{await _prompts.GetAsync(DefaultPrompts.CodeReview, ct)}\n\n{UntrustedContent.Guard}",
                new[] { new AiMessage(AiRole.User, user) }, 8000), ct);

        var scores = json["scores"] as JsonObject ?? new JsonObject();
        var scoreDto = new CodeReviewScoresDto(AiJson.Score(scores, "quality"), AiJson.Score(scores, "performance"), AiJson.Score(scores, "security"),
            AiJson.Score(scores, "readability"), AiJson.Score(scores, "maintainability"), AiJson.Score(scores, "bestPractices"));
        var lineCount = request.Code.Split('\n').Length;
        var issues = AiJson.Arr(json, "issues").OfType<JsonObject>().Take(30).Select(i =>
        {
            var severity = AiJson.Str(i, "severity", 20).ToLowerInvariant();
            int? line = i["line"] is JsonValue lv && lv.TryGetValue<int>(out var l) && l >= 1 && l <= lineCount ? l : null;
            return new CodeIssueDto(Severities.Contains(severity) ? severity : "info", AiJson.Str(i, "category", 40), line,
                AiJson.Str(i, "title", 200), AiJson.Str(i, "description", 1500), AiJson.Str(i, "recommendation", 1500));
        }).Where(i => i.Title.Length > 0).ToList();
        var overall = AiJson.Score(json, "overallScore", (scoreDto.Quality + scoreDto.Readability + scoreDto.Maintainability) / 3);
        if (issues.Any(i => i.Severity == "critical")) overall = Math.Min(overall, 50);

        var result = new
        {
            overall, scores = scoreDto, timeComplexity = AiJson.Str(json, "timeComplexity", 60), spaceComplexity = AiJson.Str(json, "spaceComplexity", 60),
            issues, potentialBugs = AiJson.StrList(json, "potentialBugs"), optimizedCode = AiJson.Str(json, "optimizedCode", 40000),
            explanation = AiJson.Str(json, "explanation", 6000)
        };
        var entity = new CodeReview { UserId = userId, LanguageCode = language, SourceCode = request.Code, OverallScore = overall, ResultJson = Json.Serialize(result) };
        _db.CodeReviews.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _gamification.RecordActivityAsync(userId, ActivityType.CodeReviewed, entity.Id, null, 60, ct);
        return new CodeReviewDto(entity.Id, language, request.Code, overall, scoreDto, result.timeComplexity, result.spaceComplexity, issues,
            result.potentialBugs, result.optimizedCode, result.explanation, ReviewDisclaimer, entity.CreatedAt);
    }

    private static string AddLineNumbers(string code) =>
        string.Join('\n', code.Replace("\r\n", "\n").Split('\n').Select((l, i) => $"{i + 1,4}| {l}"));

    public async Task<CodeExplanationDto> ExplainCodeAsync(Guid userId, ExplainCodeRequest request, CancellationToken ct)
    {
        EnsureAvailable();
        await _entitlements.ConsumeAsync(userId, UsageFeature.AiCodeExplanation, ct);
        var json = await _ai.CompleteJsonAsync(AiFeature.CodeExplanation, userId, new AiCompletionRequest(
            $"{await _prompts.GetAsync(DefaultPrompts.CodeExplanation, ct)}\n\n{UntrustedContent.Guard}",
            new[] { new AiMessage(AiRole.User, $"Language: {request.LanguageCode}\n\n{UntrustedContent.Wrap("candidate_code", request.Code, _options.MaxInputCharacters)}") }, 3000), ct);
        var summary = AiJson.Str(json, "summary", 2000);
        if (summary.Length == 0) throw new ServiceUnavailableException(ErrorCodes.AiInvalidResponse, "The AI explanation was incomplete. Please try again.");
        return new CodeExplanationDto(summary, AiJson.StrList(json, "steps", 20, 800), AiJson.Str(json, "timeComplexity", 60),
            AiJson.Str(json, "spaceComplexity", 60), AiJson.StrList(json, "keyConcepts"));
    }

    /// <summary>Admin-only: generates draft questions that must be reviewed before publishing.</summary>
    public async Task<GeneratedQuestionsResponse> GenerateQuestionsAsync(Guid userId, GenerateQuestionsRequest request, CancellationToken ct)
    {
        EnsureAvailable();
        var category = await _db.Categories.Include(c => c.Technology).FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.TechnologyId == request.TechnologyId, ct)
                       ?? throw new NotFoundException("Category", request.CategoryId);
        var difficulty = EnumParser.Parse<Difficulty>(request.Difficulty, nameof(request.Difficulty));
        var type = EnumParser.Parse<QuestionType>(request.Type, nameof(request.Type));
        await _entitlements.ConsumeAsync(userId, UsageFeature.AiQuestionGeneration, ct);

        var existingTitles = await _db.Questions.Where(q => q.CategoryId == category.Id).Select(q => q.Title).Take(200).ToListAsync(ct);
        var prompt = $"Technology: {category.Technology.Name}\nCategory: {category.Name}\nDifficulty: {difficulty}\nQuestion type: {type}\n" +
                     $"Number of questions: {request.Count}\nAvoid duplicating these existing titles:\n- {string.Join("\n- ", existingTitles.Take(80))}";
        var json = await _ai.CompleteJsonAsync(AiFeature.QuestionGeneration, userId, new AiCompletionRequest(
            await _prompts.GetAsync(DefaultPrompts.QuestionGeneration, ct), new[] { new AiMessage(AiRole.User, prompt) }, 12000), ct);

        var ids = new List<Guid>();
        var rejected = 0;
        foreach (var item in AiJson.Arr(json, "questions").OfType<JsonObject>().Take(request.Count))
        {
            var title = AiJson.Str(item, "title", 300);
            var body = AiJson.Str(item, "body", 4000);
            var options = AiJson.Arr(item, "options").OfType<JsonObject>()
                .Select((o, i) => new QuestionOption { Text = AiJson.Str(o, "text", 1000), IsCorrect = o["isCorrect"]?.GetValueKind() == System.Text.Json.JsonValueKind.True, Explanation = AiJson.Str(o, "explanation", 1000), DisplayOrder = i })
                .Where(o => o.Text.Length > 0).ToList();
            var objective = type is QuestionType.MultipleChoice or QuestionType.SingleChoice or QuestionType.TrueFalse or QuestionType.MultipleSelect;
            var validOptions = !objective || (options.Count >= 2 && options.Any(o => o.IsCorrect) &&
                (type == QuestionType.MultipleSelect || options.Count(o => o.IsCorrect) == 1));
            if (title.Length < 5 || body.Length < 10 || !validOptions || existingTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
            {
                rejected++;
                continue;
            }
            var question = new Question
            {
                TechnologyId = category.TechnologyId, CategoryId = category.Id, Title = title, Body = body, Type = type, Difficulty = difficulty,
                CodeSnippet = NullIfEmpty(AiJson.Str(item, "codeSnippet", 6000)), CodeLanguage = NullIfEmpty(AiJson.Str(item, "codeLanguage", 30)),
                EstimatedSeconds = item["estimatedSeconds"] is JsonValue sv && sv.TryGetValue<int>(out var secs) ? Math.Clamp(secs, 30, 900) : 90,
                Status = ContentStatus.Draft, CreatedByUserId = userId, Options = options,
                Answer = new QuestionAnswer { ShortAnswer = AiJson.Str(item, "shortAnswer", 2000), DetailedExplanation = AiJson.Str(item, "detailedExplanation", 8000) }
            };
            foreach (var tagName in AiJson.StrList(item, "tags", 6, 40))
            {
                var slug = Slug.From(tagName);
                var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug, ct) ?? _db.Tags.Local.FirstOrDefault(t => t.Slug == slug);
                if (tag is null)
                {
                    tag = new Tag { Name = tagName, Slug = slug };
                    _db.Tags.Add(tag);
                }
                question.Tags.Add(new QuestionTag { Tag = tag });
            }
            _db.Questions.Add(question);
            existingTitles.Add(title);
            ids.Add(question.Id);
        }
        await _db.SaveChangesAsync(ct);
        return new GeneratedQuestionsResponse(ids, rejected,
            "Generated questions are saved as drafts. Review them for accuracy before publishing.");
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) || s == "null" ? null : s;
}

public sealed class EvaluateAnswerRequestValidator : AbstractValidator<EvaluateAnswerRequest>
{
    public EvaluateAnswerRequestValidator()
    {
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(12000);
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(4000).When(x => x.QuestionId is null)
            .WithMessage("Provide a question id or the question text.");
    }
}

public sealed class ReviewCodeRequestValidator : AbstractValidator<ReviewCodeRequest>
{
    public ReviewCodeRequestValidator()
    {
        RuleFor(x => x.LanguageCode).NotEmpty().Must(l => AiToolsService.ReviewLanguages.Contains(l.ToLowerInvariant()))
            .WithMessage($"Supported languages: {string.Join(", ", AiToolsService.ReviewLanguages)}.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16000);
        RuleFor(x => x.Context).MaximumLength(1500);
    }
}

public sealed class ExplainCodeRequestValidator : AbstractValidator<ExplainCodeRequest>
{
    public ExplainCodeRequestValidator()
    {
        RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(12000);
    }
}

public sealed class GenerateQuestionsRequestValidator : AbstractValidator<GenerateQuestionsRequest>
{
    public GenerateQuestionsRequestValidator()
    {
        RuleFor(x => x.TechnologyId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Count).InclusiveBetween(1, 10);
        RuleFor(x => x.Difficulty).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
    }
}
