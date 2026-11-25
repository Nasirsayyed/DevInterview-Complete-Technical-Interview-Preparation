using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Ai;
using DevInterview.Application.Features.Coding;
using DevInterview.Application.Features.MockInterviews;
using DevInterview.Application.Features.Quizzes;
using DevInterview.Contracts.Ai;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Practice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevInterview.API.Controllers;

/// <summary>Quiz engine: listing, custom/daily quizzes, attempts with a server-enforced timer, submission and results.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/quizzes")]
public sealed class QuizzesController : ApiControllerBase
{
    private readonly IQuizService _quizzes;
    private readonly ICurrentUser _user;

    public QuizzesController(IQuizService quizzes, ICurrentUser user)
    {
        _quizzes = quizzes;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QuizListItemDto>>>> List([FromQuery] QuizQuery query, CancellationToken ct) =>
        Success(await _quizzes.ListAsync(_user.RequireUserId(), query, ct));

    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<QuizListItemDto>>> Daily(CancellationToken ct) => Success(await _quizzes.GetDailyAsync(_user.RequireUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuizListItemDto>>> Get(Guid id, CancellationToken ct) => Success(await _quizzes.GetAsync(_user.RequireUserId(), id, ct));

    /// <summary>Generates a custom quiz from the question bank using the given filters.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<QuizListItemDto>>> Create(CreateQuizRequest request, CancellationToken ct) =>
        Created(await _quizzes.CreateAsync(_user.RequireUserId(), request, ct));

    /// <summary>Starts (or resumes) an attempt. Returns questions without correct answers plus the server deadline.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ApiResponse<QuizAttemptDto>>> Start(Guid id, CancellationToken ct) => Success(await _quizzes.StartAsync(_user.RequireUserId(), id, ct));

    /// <summary>Saves one answer (and mark-for-review state) during an attempt.</summary>
    [HttpPut("attempts/{attemptId:guid}/answers")]
    public async Task<ActionResult<ApiResponse<object>>> SaveAnswer(Guid attemptId, SaveQuizAnswerRequest request, CancellationToken ct)
    {
        await _quizzes.SaveAnswerAsync(_user.RequireUserId(), attemptId, request, ct);
        return Done("Saved.");
    }

    /// <summary>Submits the attempt. Idempotent: repeated submits return the original result.</summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<QuizResultDto>>> Submit(Guid id, SubmitQuizRequest request, CancellationToken ct) =>
        Success(await _quizzes.SubmitAsync(_user.RequireUserId(), id, request, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<ActionResult<ApiResponse<QuizResultDto>>> Result(Guid attemptId, CancellationToken ct) =>
        Success(await _quizzes.GetResultAsync(_user.RequireUserId(), attemptId, ct));

    [HttpGet("attempts/{attemptId:guid}/state")]
    public async Task<ActionResult<ApiResponse<QuizAttemptDto>>> State(Guid attemptId, CancellationToken ct) =>
        Success(await _quizzes.GetAttemptStateAsync(_user.RequireUserId(), attemptId, ct));

    [HttpGet("attempts")]
    public async Task<ActionResult<ApiResponse<PagedResult<QuizAttemptSummaryDto>>>> History([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await _quizzes.GetHistoryAsync(_user.RequireUserId(), query, ct));
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/challenges")]
public sealed class ChallengesController : ApiControllerBase
{
    private readonly ICodingService _coding;
    private readonly ICurrentUser _user;

    public ChallengesController(ICodingService coding, ICurrentUser user)
    {
        _coding = coding;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ChallengeListItemDto>>>> List([FromQuery] ChallengeQuery query, CancellationToken ct) =>
        Success(await _coding.ListAsync(_user.RequireUserId(), query, ct));

    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<ChallengeListItemDto?>>> Daily(CancellationToken ct) => Success(await _coding.GetDailyChallengeAsync(_user.RequireUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChallengeDetailDto>>> Get(Guid id, CancellationToken ct) => Success(await _coding.GetAsync(_user.RequireUserId(), id, ct));

    [HttpGet("{id:guid}/hints/{index:int}")]
    public async Task<ActionResult<ApiResponse<ChallengeHintDto>>> Hint(Guid id, int index, CancellationToken ct) =>
        Success(await _coding.GetHintAsync(_user.RequireUserId(), id, index, ct));

    [HttpGet("{id:guid}/solution")]
    public async Task<ActionResult<ApiResponse<ChallengeSolutionDto>>> Solution(Guid id, CancellationToken ct) =>
        Success(await _coding.GetSolutionAsync(_user.RequireUserId(), id, ct));
}

/// <summary>Code execution in the isolated sandbox.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/code")]
public sealed class CodeController : ApiControllerBase
{
    private readonly ICodingService _coding;
    private readonly ICurrentUser _user;

    public CodeController(ICodingService coding, ICurrentUser user)
    {
        _coding = coding;
        _user = user;
    }

    [HttpGet("languages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SupportedLanguageDto>>>> Languages(CancellationToken ct) => Success(await _coding.GetLanguagesAsync(ct));

    /// <summary>Runs code against the sample test cases and an optional custom input.</summary>
    [HttpPost("run")]
    [EnableRateLimiting(RateLimitPolicies.Code)]
    public async Task<ActionResult<ApiResponse<RunCodeResponse>>> Run(RunCodeRequest request, CancellationToken ct) =>
        Success(await _coding.RunAsync(_user.RequireUserId(), request, ct));

    /// <summary>Submits code against all (sample + hidden) test cases. Supply an IdempotencyKey to make retries safe.</summary>
    [HttpPost("submit")]
    [EnableRateLimiting(RateLimitPolicies.Code)]
    public async Task<ActionResult<ApiResponse<SubmissionDto>>> Submit(SubmitCodeRequest request, CancellationToken ct) =>
        Success(await _coding.SubmitAsync(_user.RequireUserId(), request, ct));

    [HttpGet("submissions")]
    public async Task<ActionResult<ApiResponse<PagedResult<SubmissionDto>>>> Submissions([FromQuery] SubmissionQuery query, CancellationToken ct) =>
        Success(await _coding.GetSubmissionsAsync(_user.RequireUserId(), query, ct));

    [HttpGet("submissions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<SubmissionDto>>> Submission(Guid id, CancellationToken ct) =>
        Success(await _coding.GetSubmissionAsync(_user.RequireUserId(), id, ct));
}

/// <summary>AI answer evaluation, code review and explanation (premium, quota-limited).</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/ai")]
public sealed class AiController : ApiControllerBase
{
    private readonly IAiToolsService _ai;
    private readonly ICurrentUser _user;

    public AiController(IAiToolsService ai, ICurrentUser user)
    {
        _ai = ai;
        _user = user;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<AiStatusDto>>> Status(CancellationToken ct) => Success(await _ai.GetStatusAsync(_user.RequireUserId(), ct));

    [HttpPost("evaluate-answer")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<AnswerEvaluationDto>>> EvaluateAnswer(EvaluateAnswerRequest request, CancellationToken ct) =>
        Success(await _ai.EvaluateAnswerAsync(_user.RequireUserId(), request, ct));

    [HttpGet("evaluations")]
    public async Task<ActionResult<ApiResponse<PagedResult<AnswerEvaluationSummaryDto>>>> Evaluations([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await _ai.GetEvaluationsAsync(_user.RequireUserId(), query, ct));

    [HttpGet("evaluations/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AnswerEvaluationDto>>> Evaluation(Guid id, CancellationToken ct) =>
        Success(await _ai.GetEvaluationAsync(_user.RequireUserId(), id, ct));

    [HttpPost("review-code")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<CodeReviewDto>>> ReviewCode(ReviewCodeRequest request, CancellationToken ct) =>
        Success(await _ai.ReviewCodeAsync(_user.RequireUserId(), request, ct));

    [HttpPost("explain-code")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<CodeExplanationDto>>> ExplainCode(ExplainCodeRequest request, CancellationToken ct) =>
        Success(await _ai.ExplainCodeAsync(_user.RequireUserId(), request, ct));

    /// <summary>Admin: generates draft questions for review in the admin panel.</summary>
    [HttpPost("generate-questions")]
    [Authorize(Policy = Policies.AdminPanel)]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<GeneratedQuestionsResponse>>> GenerateQuestions(GenerateQuestionsRequest request, CancellationToken ct) =>
        Success(await _ai.GenerateQuestionsAsync(_user.RequireUserId(), request, ct));
}

/// <summary>AI mock interview lifecycle.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/mock-interviews")]
public sealed class MockInterviewsController : ApiControllerBase
{
    private readonly IMockInterviewService _interviews;
    private readonly ICurrentUser _user;

    public MockInterviewsController(IMockInterviewService interviews, ICurrentUser user)
    {
        _interviews = interviews;
        _user = user;
    }

    [HttpPost("start")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<MockInterviewDto>>> Start(StartMockInterviewRequest request, CancellationToken ct) =>
        Created(await _interviews.StartAsync(_user.RequireUserId(), request, ct));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MockInterviewListItemDto>>>> List([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await _interviews.ListAsync(_user.RequireUserId(), query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MockInterviewDto>>> Get(Guid id, CancellationToken ct) => Success(await _interviews.GetAsync(_user.RequireUserId(), id, ct));

    [HttpPost("{id:guid}/answer")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<MockInterviewDto>>> Answer(Guid id, SubmitInterviewAnswerRequest request, CancellationToken ct) =>
        Success(await _interviews.AnswerAsync(_user.RequireUserId(), id, request, ct));

    /// <summary>Skips the current question and asks the next one.</summary>
    [HttpPost("{id:guid}/next-question")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<MockInterviewDto>>> Next(Guid id, CancellationToken ct) => Success(await _interviews.NextQuestionAsync(_user.RequireUserId(), id, ct));

    [HttpPost("{id:guid}/complete")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    public async Task<ActionResult<ApiResponse<MockInterviewReportDto>>> Complete(Guid id, CancellationToken ct) =>
        Success(await _interviews.CompleteAsync(_user.RequireUserId(), id, ct));

    [HttpGet("{id:guid}/report")]
    public async Task<ActionResult<ApiResponse<MockInterviewReportDto>>> Report(Guid id, CancellationToken ct) =>
        Success(await _interviews.GetReportAsync(_user.RequireUserId(), id, ct));
}
