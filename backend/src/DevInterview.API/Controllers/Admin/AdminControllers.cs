using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Admin;
using DevInterview.Contracts.Admin;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Engagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInterview.API.Controllers.Admin;

/// <summary>Content management: taxonomy, questions (incl. CSV/Excel import/export), reports.</summary>
[ApiVersion("1.0")]
[Authorize(Policy = Policies.AdminPanel)]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminContentController : ApiControllerBase
{
    private readonly IAdminContentService _content;
    private readonly ICurrentUser _user;

    public AdminContentController(IAdminContentService content, ICurrentUser user)
    {
        _content = content;
        _user = user;
    }

    [HttpGet("technologies")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminTechnologyDto>>>> Technologies(CancellationToken ct) => Success(await _content.GetTechnologiesAsync(ct));

    [HttpPost("technologies")]
    public async Task<ActionResult<ApiResponse<AdminTechnologyDto>>> CreateTechnology(TechnologyUpsertRequest r, CancellationToken ct) => Created(await _content.UpsertTechnologyAsync(null, r, ct));

    [HttpPut("technologies/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminTechnologyDto>>> UpdateTechnology(Guid id, TechnologyUpsertRequest r, CancellationToken ct) => Success(await _content.UpsertTechnologyAsync(id, r, ct));

    [HttpDelete("technologies/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTechnology(Guid id, CancellationToken ct) { await _content.DeleteTechnologyAsync(id, ct); return Done("Deleted."); }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminCategoryDto>>>> Categories([FromQuery] Guid? technologyId, CancellationToken ct) => Success(await _content.GetCategoriesAsync(technologyId, ct));

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<AdminCategoryDto>>> CreateCategory(CategoryUpsertRequest r, CancellationToken ct) => Created(await _content.UpsertCategoryAsync(null, r, ct));

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminCategoryDto>>> UpdateCategory(Guid id, CategoryUpsertRequest r, CancellationToken ct) => Success(await _content.UpsertCategoryAsync(id, r, ct));

    [HttpDelete("categories/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategory(Guid id, CancellationToken ct) { await _content.DeleteCategoryAsync(id, ct); return Done("Deleted."); }

    [HttpPost("topics")]
    public async Task<ActionResult<ApiResponse<AdminTopicDto>>> CreateTopic(TopicUpsertRequest r, CancellationToken ct) => Created(await _content.UpsertTopicAsync(null, r, ct));

    [HttpPut("topics/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminTopicDto>>> UpdateTopic(Guid id, TopicUpsertRequest r, CancellationToken ct) => Success(await _content.UpsertTopicAsync(id, r, ct));

    [HttpDelete("topics/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTopic(Guid id, CancellationToken ct) { await _content.DeleteTopicAsync(id, ct); return Done("Deleted."); }

    [HttpGet("questions")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminQuestionListItemDto>>>> Questions([FromQuery] AdminQuestionQuery q, CancellationToken ct) => Success(await _content.GetQuestionsAsync(q, ct));

    /// <summary>Full question including answers; used for editing and preview before publishing.</summary>
    [HttpGet("questions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminQuestionDto>>> Question(Guid id, CancellationToken ct) => Success(await _content.GetQuestionAsync(id, ct));

    [HttpPost("questions")]
    public async Task<ActionResult<ApiResponse<AdminQuestionDto>>> CreateQuestion(QuestionUpsertRequest r, CancellationToken ct) =>
        Created(await _content.UpsertQuestionAsync(null, r, _user.RequireUserId(), ct));

    [HttpPut("questions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<AdminQuestionDto>>> UpdateQuestion(Guid id, QuestionUpsertRequest r, CancellationToken ct) =>
        Success(await _content.UpsertQuestionAsync(id, r, _user.RequireUserId(), ct));

    [HttpDelete("questions/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteQuestion(Guid id, CancellationToken ct) { await _content.DeleteQuestionAsync(id, ct); return Done("Deleted."); }

    [HttpPost("questions/bulk-status")]
    public async Task<ActionResult<ApiResponse<object>>> BulkStatus(BulkStatusRequest r, CancellationToken ct) =>
        Success<object>(new { updated = await _content.BulkSetStatusAsync(r, ct) });

    /// <summary>Imports questions from a .csv or .xlsx file using the template columns. Use dryRun=true to validate first.</summary>
    [HttpPost("questions/import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportResultDto>>> Import(IFormFile file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Success(await _content.ImportQuestionsAsync(stream, file.FileName, dryRun, _user.RequireUserId(), ct));
    }

    [HttpGet("questions/export")]
    public async Task<IActionResult> Export([FromQuery] Guid? technologyId, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        var bytes = await _content.ExportQuestionsAsync(technologyId, format, ct);
        return format == "xlsx"
            ? File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "devinterview-questions.xlsx")
            : File(bytes, "text/csv", "devinterview-questions.csv");
    }

    [HttpGet("questions/import-template")]
    public IActionResult Template([FromQuery] string format = "csv")
    {
        var bytes = _content.GetImportTemplate(format);
        return format == "xlsx"
            ? File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "question-import-template.xlsx")
            : File(bytes, "text/csv", "question-import-template.csv");
    }

    [HttpGet("question-reports")]
    public async Task<ActionResult<ApiResponse<PagedResult<QuestionReportDto>>>> Reports([FromQuery] PageQuery q, [FromQuery] string? status, CancellationToken ct) =>
        Success(await _content.GetReportsAsync(q, status, ct));

    [HttpPut("question-reports/{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Resolve(Guid id, ResolveReportRequest r, CancellationToken ct) { await _content.ResolveReportAsync(id, r, ct); return Done("Updated."); }
}

/// <summary>Quizzes, coding challenges & test cases, learning paths, flashcards and companies.</summary>
[ApiVersion("1.0")]
[Authorize(Policy = Policies.AdminPanel)]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminCatalogController : ApiControllerBase
{
    private readonly IAdminCatalogService _catalog;
    public AdminCatalogController(IAdminCatalogService catalog) => _catalog = catalog;

    [HttpGet("quizzes")] public async Task<ActionResult<ApiResponse<PagedResult<AdminQuizDto>>>> Quizzes([FromQuery] PageQuery q, CancellationToken ct) => Success(await _catalog.GetQuizzesAsync(q, ct));
    [HttpGet("quizzes/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminQuizDto>>> Quiz(Guid id, CancellationToken ct) => Success(await _catalog.GetQuizAsync(id, ct));
    [HttpPost("quizzes")] public async Task<ActionResult<ApiResponse<AdminQuizDto>>> CreateQuiz(QuizUpsertRequest r, CancellationToken ct) => Created(await _catalog.UpsertQuizAsync(null, r, ct));
    [HttpPut("quizzes/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminQuizDto>>> UpdateQuiz(Guid id, QuizUpsertRequest r, CancellationToken ct) => Success(await _catalog.UpsertQuizAsync(id, r, ct));
    [HttpDelete("quizzes/{id:guid}")] public async Task<ActionResult<ApiResponse<object>>> DeleteQuiz(Guid id, CancellationToken ct) { await _catalog.DeleteQuizAsync(id, ct); return Done("Deleted."); }

    [HttpGet("challenges")] public async Task<ActionResult<ApiResponse<PagedResult<AdminChallengeListItemDto>>>> Challenges([FromQuery] PageQuery q, CancellationToken ct) => Success(await _catalog.GetChallengesAsync(q, ct));
    [HttpGet("challenges/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminChallengeDto>>> Challenge(Guid id, CancellationToken ct) => Success(await _catalog.GetChallengeAsync(id, ct));
    [HttpPost("challenges")] public async Task<ActionResult<ApiResponse<AdminChallengeDto>>> CreateChallenge(ChallengeUpsertRequest r, CancellationToken ct) => Created(await _catalog.UpsertChallengeAsync(null, r, ct));
    [HttpPut("challenges/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminChallengeDto>>> UpdateChallenge(Guid id, ChallengeUpsertRequest r, CancellationToken ct) => Success(await _catalog.UpsertChallengeAsync(id, r, ct));
    [HttpDelete("challenges/{id:guid}")] public async Task<ActionResult<ApiResponse<object>>> DeleteChallenge(Guid id, CancellationToken ct) { await _catalog.DeleteChallengeAsync(id, ct); return Done("Deleted."); }

    [HttpGet("learning-paths")] public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminLearningPathListItemDto>>>> Paths(CancellationToken ct) => Success(await _catalog.GetPathsAsync(ct));
    [HttpGet("learning-paths/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminLearningPathDto>>> Path(Guid id, CancellationToken ct) => Success(await _catalog.GetPathAsync(id, ct));
    [HttpPost("learning-paths")] public async Task<ActionResult<ApiResponse<AdminLearningPathDto>>> CreatePath(LearningPathUpsertRequest r, CancellationToken ct) => Created(await _catalog.UpsertPathAsync(null, r, ct));
    [HttpPut("learning-paths/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminLearningPathDto>>> UpdatePath(Guid id, LearningPathUpsertRequest r, CancellationToken ct) => Success(await _catalog.UpsertPathAsync(id, r, ct));
    [HttpDelete("learning-paths/{id:guid}")] public async Task<ActionResult<ApiResponse<object>>> DeletePath(Guid id, CancellationToken ct) { await _catalog.DeletePathAsync(id, ct); return Done("Deleted."); }

    [HttpGet("flashcards")] public async Task<ActionResult<ApiResponse<PagedResult<AdminFlashcardDto>>>> Flashcards([FromQuery] AdminFlashcardQuery q, CancellationToken ct) => Success(await _catalog.GetFlashcardsAsync(q, ct));
    [HttpPost("flashcards")] public async Task<ActionResult<ApiResponse<AdminFlashcardDto>>> CreateFlashcard(FlashcardUpsertRequest r, CancellationToken ct) => Created(await _catalog.UpsertFlashcardAsync(null, r, ct));
    [HttpPut("flashcards/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminFlashcardDto>>> UpdateFlashcard(Guid id, FlashcardUpsertRequest r, CancellationToken ct) => Success(await _catalog.UpsertFlashcardAsync(id, r, ct));
    [HttpDelete("flashcards/{id:guid}")] public async Task<ActionResult<ApiResponse<object>>> DeleteFlashcard(Guid id, CancellationToken ct) { await _catalog.DeleteFlashcardAsync(id, ct); return Done("Deleted."); }

    [HttpGet("companies")] public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminCompanyDto>>>> Companies(CancellationToken ct) => Success(await _catalog.GetCompaniesAsync(ct));
    [HttpGet("companies/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminCompanyDto>>> Company(Guid id, CancellationToken ct) => Success(await _catalog.GetCompanyAsync(id, ct));
    [HttpPost("companies")] public async Task<ActionResult<ApiResponse<AdminCompanyDto>>> CreateCompany(CompanyUpsertRequest r, CancellationToken ct) => Created(await _catalog.UpsertCompanyAsync(null, r, ct));
    [HttpPut("companies/{id:guid}")] public async Task<ActionResult<ApiResponse<AdminCompanyDto>>> UpdateCompany(Guid id, CompanyUpsertRequest r, CancellationToken ct) => Success(await _catalog.UpsertCompanyAsync(id, r, ct));
    [HttpDelete("companies/{id:guid}")] public async Task<ActionResult<ApiResponse<object>>> DeleteCompany(Guid id, CancellationToken ct) { await _catalog.DeleteCompanyAsync(id, ct); return Done("Deleted."); }
}

/// <summary>Operations: dashboard, users, subscriptions, AI configuration, notifications, audit logs, settings, reports.</summary>
[ApiVersion("1.0")]
[Authorize(Policy = Policies.AdminPanel)]
[Route("api/v{version:apiVersion}/admin")]
public sealed class AdminOperationsController : ApiControllerBase
{
    private readonly IAdminOperationsService _ops;
    private readonly ICurrentUser _user;

    public AdminOperationsController(IAdminOperationsService ops, ICurrentUser user)
    {
        _ops = ops;
        _user = user;
    }

    [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> Dashboard(CancellationToken ct) => Success(await _ops.GetDashboardAsync(ct));
    [HttpGet("reports/content")] public async Task<ActionResult<ApiResponse<ContentReportDto>>> ContentReport(CancellationToken ct) => Success(await _ops.GetContentReportAsync(ct));

    [HttpGet("users"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminUserDto>>>> Users([FromQuery] AdminUserQuery q, CancellationToken ct) => Success(await _ops.GetUsersAsync(q, ct));

    [HttpGet("users/{id:guid}"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> User(Guid id, CancellationToken ct) => Success(await _ops.GetUserAsync(id, ct));

    [HttpPut("users/{id:guid}/roles"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Roles(Guid id, UpdateUserRolesRequest r, CancellationToken ct) => Success(await _ops.UpdateRolesAsync(id, r, _user.RequireUserId(), ct));

    [HttpPut("users/{id:guid}/active"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> Active(Guid id, SetUserActiveRequest r, CancellationToken ct) => Success(await _ops.SetActiveAsync(id, r, _user.RequireUserId(), ct));

    [HttpPost("users/{id:guid}/subscriptions"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AdminSubscriptionDto>>> Grant(Guid id, GrantSubscriptionRequest r, CancellationToken ct) => Created(await _ops.GrantSubscriptionAsync(id, r, ct));

    [HttpGet("subscriptions"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminSubscriptionDto>>>> Subscriptions([FromQuery] PageQuery q, CancellationToken ct) => Success(await _ops.GetSubscriptionsAsync(q, ct));

    [HttpPost("subscriptions/{id:guid}/revoke"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(Guid id, CancellationToken ct) { await _ops.RevokeSubscriptionAsync(id, ct); return Done("Revoked."); }

    [HttpGet("plans"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SubscriptionPlanDto>>>> Plans(CancellationToken ct) => Success(await _ops.GetPlansAsync(ct));

    [HttpPost("plans"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> CreatePlan(SubscriptionPlanUpsertRequest r, CancellationToken ct) => Created(await _ops.UpsertPlanAsync(null, r, ct));

    [HttpPut("plans/{id:guid}"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> UpdatePlan(Guid id, SubscriptionPlanUpsertRequest r, CancellationToken ct) => Success(await _ops.UpsertPlanAsync(id, r, ct));

    [HttpGet("ai/prompts"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiPromptDto>>>> Prompts(CancellationToken ct) => Success(await _ops.GetPromptsAsync(ct));

    [HttpPost("ai/prompts"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AiPromptDto>>> SavePrompt(AiPromptUpsertRequest r, CancellationToken ct) => Created(await _ops.SavePromptAsync(r, ct));

    [HttpGet("ai/providers"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiProviderSettingDto>>>> Providers(CancellationToken ct) => Success(await _ops.GetProvidersAsync(ct));

    [HttpGet("ai/usage"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<AiUsageSummaryDto>>> Usage([FromQuery] int days = 30, CancellationToken ct = default) => Success(await _ops.GetAiUsageAsync(days, ct));

    [HttpPost("notifications/broadcast"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<BroadcastResultDto>>> Broadcast(BroadcastNotificationRequest r, CancellationToken ct) => Success(await _ops.BroadcastAsync(r, ct));

    [HttpGet("audit-logs"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> Audit([FromQuery] AuditLogQuery q, CancellationToken ct) => Success(await _ops.GetAuditLogsAsync(q, ct));

    [HttpGet("settings"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppSettingDto>>>> Settings(CancellationToken ct) => Success(await _ops.GetSettingsAsync(ct));

    [HttpPut("settings"), Authorize(Policy = Policies.SuperAdmin)]
    public async Task<ActionResult<ApiResponse<AppSettingDto>>> SaveSetting(AppSettingDto r, CancellationToken ct) => Success(await _ops.SaveSettingAsync(r, ct));
}
