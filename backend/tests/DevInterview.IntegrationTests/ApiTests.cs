using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevInterview.IntegrationTests;

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;

[Collection("api")]
public class AuthTests
{
    private readonly ApiFactory _factory;
    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_requires_email_verification_before_login()
    {
        var client = _factory.CreateClient();
        var email = $"verify{Guid.NewGuid():N}@test.local";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Grace", lastName = "Hopper", email, password = "Passw0rd!", confirmPassword = "Passw0rd!", experienceLevel = "Advanced"
        });
        var data = await register.DataAsync();
        Assert.True(data.GetProperty("emailVerificationRequired").GetBoolean());

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("email_not_verified", await login.ErrorCodeAsync());

        var wrongCode = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = _factory.Emails.LastCodeByEmail[email] });
        Assert.True((await verify.DataAsync()).GetProperty("user").GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!" })).StatusCode);
    }

    [Fact]
    public async Task Validation_errors_return_field_details()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "", lastName = "X", email = "not-an-email", password = "short", confirmPassword = "different", experienceLevel = "Guru"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.ReadAsync()).GetProperty("error");
        Assert.Equal("validation_error", error.GetProperty("code").GetString());
        var details = error.GetProperty("details");
        foreach (var field in new[] { "firstName", "email", "password", "confirmPassword", "experienceLevel" })
            Assert.True(details.TryGetProperty(field, out _), $"missing details for {field}: {details}");
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_account_locks_after_repeated_failures()
    {
        var (_, auth) = await _factory.NewUserAsync();
        var email = auth.GetProperty("user").GetProperty("email").GetString();
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            Assert.Equal("invalid_credentials", await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Wrong123!" })).ErrorCodeAsync());
        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal("account_locked", await locked.ErrorCodeAsync());
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_revokes_the_family()
    {
        var (_, auth) = await _factory.NewUserAsync();
        var client = _factory.CreateClient();
        var original = auth.GetProperty("refreshToken").GetString();
        var rotated = await (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original })).DataAsync();
        var next = rotated.GetProperty("refreshToken").GetString();
        Assert.NotEqual(original, next);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterTheft = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = next });
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode); // whole family revoked
    }

    [Fact]
    public async Task Password_reset_with_emailed_code_revokes_sessions()
    {
        var (_, auth) = await _factory.NewUserAsync();
        var email = auth.GetProperty("user").GetProperty("email").GetString()!;
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);
        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { email, code = _factory.Emails.LastCodeByEmail[email], newPassword = "N3wPassword!", confirmPassword = "N3wPassword!" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.GetProperty("refreshToken").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "N3wPassword!" })).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_unknown_accounts()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "nobody@nowhere.test" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage.token.value")]
    public async Task Protected_endpoints_require_a_valid_token(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null) client.UseToken(token);
        var response = await client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Account_deletion_anonymises_and_blocks_login()
    {
        var (client, auth) = await _factory.NewUserAsync();
        var email = auth.GetProperty("user").GetProperty("email").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/users/me/delete", new { password = "Passw0rd!", confirmation = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/users/me/delete", new { password = "Passw0rd!", confirmation = "DELETE" })).StatusCode);
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}

[Collection("api")]
public class AuthorizationTests
{
    private readonly ApiFactory _factory;
    public AuthorizationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Regular_users_cannot_access_admin_endpoints()
    {
        var (client, _) = await _factory.NewUserAsync();
        var response = await client.GetAsync("/api/v1/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Admin_sees_dashboard_and_can_grant_premium_which_unlocks_gated_features()
    {
        var admin = await _factory.AdminAsync();
        var dashboard = await (await admin.GetAsync("/api/v1/admin/dashboard")).DataAsync();
        Assert.True(dashboard.GetProperty("publishedQuestions").GetInt32() > 200);

        var (user, auth) = await _factory.NewUserAsync();
        Assert.Equal(HttpStatusCode.PaymentRequired, (await user.GetAsync("/api/v1/analytics/technology")).StatusCode);

        var plans = await (await admin.GetAsync("/api/v1/admin/plans")).DataAsync();
        var premium = plans.EnumerateArray().First(p => p.GetProperty("tier").GetString() == "Premium").GetProperty("id").GetGuid();
        var userId = auth.GetProperty("user").GetProperty("id").GetGuid();
        var grant = await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/subscriptions", new { planId = premium, months = 1, notes = "test" });
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/analytics/technology")).StatusCode);
        var status = await (await user.GetAsync("/api/v1/subscriptions/status")).DataAsync();
        Assert.Equal("Premium", status.GetProperty("tier").GetString());

        // AI features are premium AND need a configured provider: without an API key the API reports 503, not a fake result.
        var ai = await user.PostAsJsonAsync("/api/v1/ai/evaluate-answer", new { questionText = "What is DI?", answer = "Dependency injection supplies collaborators." });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ai.StatusCode);
        Assert.Equal("ai_unavailable", await ai.ErrorCodeAsync());

        var audit = await (await admin.GetAsync("/api/v1/admin/audit-logs?entityType=UserSubscription")).DataAsync();
        Assert.True(audit.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Content_manager_cannot_manage_users()
    {
        var admin = await _factory.AdminAsync();
        var (user, auth) = await _factory.NewUserAsync();
        var id = auth.GetProperty("user").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}/roles", new { roles = new[] { "ContentManager" } })).StatusCode);

        // Roles changed → sessions revoked; log in again to receive the new role claims.
        var email = auth.GetProperty("user").GetProperty("email").GetString();
        var login = await (await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!" })).DataAsync();
        user.UseToken(login.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/admin/questions?pageSize=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/users")).StatusCode);
    }
}

[Collection("api")]
public class LearningFlowTests
{
    private readonly ApiFactory _factory;
    public LearningFlowTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Question_bank_practice_flow()
    {
        var (client, _) = await _factory.NewUserAsync();
        var techs = await (await client.GetAsync("/api/v1/technologies")).DataAsync();
        var csharp = techs.EnumerateArray().First(t => t.GetProperty("slug").GetString() == "csharp");
        Assert.Equal(30, csharp.GetProperty("questionCount").GetInt32());

        var page = await (await client.GetAsync($"/api/v1/questions?technologyId={csharp.GetProperty("id")}&difficulty=Beginner&pageSize=50")).DataAsync();
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("Beginner", i.GetProperty("difficulty").GetString()));

        var id = items.First(i => i.GetProperty("type").GetString() == "MultipleChoice").GetProperty("id").GetGuid();
        var detail = await (await client.GetAsync($"/api/v1/questions/{id}")).DataAsync();
        Assert.False(detail.GetRawText().Contains("isCorrect"), "Question detail must not reveal correct options");

        var option = detail.GetProperty("options")[0].GetProperty("id").GetGuid();
        var attempt = await (await client.PostAsJsonAsync($"/api/v1/questions/{id}/attempt", new { selectedOptionIds = new[] { option }, timeSpentSeconds = 20 })).DataAsync();
        Assert.Equal(1, attempt.GetProperty("correctOptionIds").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/questions/{id}/bookmark", null)).StatusCode);
        await client.PostAsJsonAsync($"/api/v1/questions/{id}/notes", new { content = "Review this" });
        var bookmarks = await (await client.GetAsync("/api/v1/questions/bookmarks")).DataAsync();
        Assert.Equal(1, bookmarks.GetProperty("totalCount").GetInt32());
        var export = await client.GetAsync("/api/v1/questions/notes/export?format=csv");
        Assert.Contains("Review this", await export.Content.ReadAsStringAsync());

        var duplicateReport = new { reason = "Typo or formatting", details = "x" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/questions/{id}/report", duplicateReport)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/questions/{id}/report", duplicateReport)).StatusCode);

        var dashboard = await (await client.GetAsync("/api/v1/dashboard/summary")).DataAsync();
        Assert.Equal(1, dashboard.GetProperty("questionsAttempted").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task Unknown_resources_return_404_envelope()
    {
        var (client, _) = await _factory.NewUserAsync();
        var response = await client.GetAsync($"/api/v1/questions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Daily_quiz_lifecycle_and_results()
    {
        var (client, _) = await _factory.NewUserAsync();
        var quiz = await (await client.GetAsync("/api/v1/quizzes/daily")).DataAsync();
        var quizId = quiz.GetProperty("id").GetGuid();
        var attempt = await (await client.PostAsync($"/api/v1/quizzes/{quizId}/start", null)).DataAsync();
        var questions = attempt.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(10, questions.Count);

        var first = questions[0];
        var save = await client.PutAsJsonAsync($"/api/v1/quizzes/attempts/{attempt.GetProperty("attemptId")}/answers",
            new { questionId = first.GetProperty("questionId"), selectedOptionIds = new[] { first.GetProperty("options")[0].GetProperty("id") }, isMarkedForReview = true, timeSpentSeconds = 12 });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var result = await (await client.PostAsJsonAsync($"/api/v1/quizzes/{quizId}/submit", new { attemptId = attempt.GetProperty("attemptId"), answers = Array.Empty<object>() })).DataAsync();
        Assert.Equal(10, result.GetProperty("totalQuestions").GetInt32());
        Assert.Equal(9, result.GetProperty("skipped").GetInt32());
        Assert.Equal(10, result.GetProperty("review").GetArrayLength());

        var history = await (await client.GetAsync("/api/v1/quizzes/attempts")).DataAsync();
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Learning_progress_flashcards_and_gamification()
    {
        var (client, _) = await _factory.NewUserAsync();
        var paths = await (await client.GetAsync("/api/v1/learning/paths")).DataAsync();
        Assert.Equal(10, paths.GetArrayLength());
        var free = paths.EnumerateArray().First(p => !p.GetProperty("isPremium").GetBoolean());
        var detail = await (await client.GetAsync($"/api/v1/learning/paths/{free.GetProperty("id")}")).DataAsync();
        var lessonId = detail.GetProperty("nextLessonId").GetGuid();
        var lesson = await (await client.GetAsync($"/api/v1/learning/lessons/{lessonId}")).DataAsync();
        Assert.NotEmpty(lesson.GetProperty("content").GetString()!);

        var progress = await (await client.PostAsJsonAsync("/api/v1/learning/progress", new { lessonId, status = "Completed", timeSpentSeconds = 300 })).DataAsync();
        Assert.Equal(10, progress.GetProperty("xpAwarded").GetInt32());
        var again = await (await client.PostAsJsonAsync("/api/v1/learning/progress", new { lessonId, status = "InProgress", timeSpentSeconds = 30 })).DataAsync();
        Assert.Equal("Completed", again.GetProperty("status").GetString()); // completed is terminal; no duplicate XP
        Assert.Equal(0, again.GetProperty("xpAwarded").GetInt32());

        var premium = paths.EnumerateArray().First(p => p.GetProperty("isPremium").GetBoolean());
        var locked = await client.GetAsync($"/api/v1/learning/paths/{premium.GetProperty("id")}");
        Assert.Equal(HttpStatusCode.PaymentRequired, locked.StatusCode);
        Assert.Equal("premium_required", await locked.ErrorCodeAsync());

        var decks = await (await client.GetAsync("/api/v1/flashcards/decks")).DataAsync();
        Assert.True(decks.GetArrayLength() >= 10);
        var cards = await (await client.GetAsync("/api/v1/flashcards?limit=5")).DataAsync();
        var cardId = cards[0].GetProperty("id").GetGuid();
        var review = await (await client.PostAsJsonAsync("/api/v1/flashcards/review", new { flashcardId = cardId, grade = "Easy", clientReviewId = Guid.NewGuid() })).DataAsync();
        Assert.Equal(3, review.GetProperty("intervalDays").GetInt32());

        var profile = await (await client.GetAsync("/api/v1/gamification/profile")).DataAsync();
        Assert.Equal(11, profile.GetProperty("level").GetProperty("totalXp").GetInt32());
        var leaderboard = await (await client.GetAsync("/api/v1/gamification/leaderboard?period=weekly")).DataAsync();
        Assert.True(leaderboard.GetProperty("currentUser").GetProperty("isCurrentUser").GetBoolean());
    }

    [Fact]
    public async Task Coding_endpoints_fail_clearly_when_no_sandbox_is_configured()
    {
        var (client, _) = await _factory.NewUserAsync();
        var list = await (await client.GetAsync("/api/v1/challenges?difficulty=Easy&pageSize=50")).DataAsync();
        var challenge = list.GetProperty("items").EnumerateArray().First(c => !c.GetProperty("isPremium").GetBoolean() && c.GetProperty("category").GetString() != "Sql");
        var detail = await (await client.GetAsync($"/api/v1/challenges/{challenge.GetProperty("id")}")).DataAsync();
        Assert.True(detail.GetProperty("sampleTestCases").GetArrayLength() >= 1);
        Assert.True(detail.GetProperty("hiddenTestCaseCount").GetInt32() >= 1);
        Assert.True(detail.GetProperty("starterCode").TryGetProperty("python", out _));

        var run = await client.PostAsJsonAsync("/api/v1/code/run", new { challengeId = challenge.GetProperty("id"), languageCode = "python", sourceCode = "print(1)" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, run.StatusCode);
        Assert.Equal("execution_unavailable", await run.ErrorCodeAsync());
        var solution = await client.GetAsync($"/api/v1/challenges/{challenge.GetProperty("id")}/solution");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, solution.StatusCode); // locked until solved or 3 submissions
    }

    [Fact]
    public async Task Search_and_offline_sync()
    {
        var (client, _) = await _factory.NewUserAsync();
        var search = await (await client.GetAsync("/api/v1/search?q=index")).DataAsync();
        Assert.True(search.GetProperty("countsByType").GetProperty("question").GetInt32() > 0);
        var questionHits = await (await client.GetAsync("/api/v1/search?q=index&type=question&pageSize=2")).DataAsync();
        Assert.Equal(2, questionHits.GetProperty("hits").GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/search?q=a")).StatusCode);

        var questionId = questionHits.GetProperty("hits").GetProperty("items")[0].GetProperty("id").GetGuid();
        var push = await (await client.PostAsJsonAsync("/api/v1/sync/push", new
        {
            bookmarks = new[] { new { questionId, isBookmarked = true, clientUpdatedAt = DateTime.UtcNow.AddMinutes(-5) } },
            notes = new[] { new { questionId, content = "offline", isDeleted = false, clientUpdatedAt = DateTime.UtcNow.AddMinutes(-5) } },
            flashcardReviews = Array.Empty<object>()
        })).DataAsync();
        Assert.Equal(1, push.GetProperty("appliedNotes").GetInt32());
        var pull = await (await client.GetAsync("/api/v1/sync/pull")).DataAsync();
        Assert.Equal("offline", pull.GetProperty("notes")[0].GetProperty("content").GetString());
    }
}

[Collection("api")]
public class AdminContentTests
{
    private readonly ApiFactory _factory;
    public AdminContentTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_preview_and_publish_question()
    {
        var admin = await _factory.AdminAsync();
        var techs = await (await admin.GetAsync("/api/v1/admin/technologies")).DataAsync();
        var tech = techs.EnumerateArray().First(t => t.GetProperty("slug").GetString() == "csharp");
        var cats = await (await admin.GetAsync($"/api/v1/admin/categories?technologyId={tech.GetProperty("id")}")).DataAsync();
        var payload = new
        {
            technologyId = tech.GetProperty("id"), categoryId = cats[0].GetProperty("id"), topicId = (Guid?)null, title = "Integration test question",
            body = "Which keyword declares a constant?", codeSnippet = (string?)null, codeLanguage = (string?)null, type = "MultipleChoice", difficulty = "Beginner",
            estimatedSeconds = 30, isPremium = false, status = "Published", tags = new[] { "test" },
            options = new[] { new { text = "const", isCorrect = true, explanation = (string?)null }, new { text = "static", isCorrect = true, explanation = (string?)null } },
            shortAnswer = "const", detailedExplanation = "const declares compile-time constants.", referenceAnswer = (string?)null
        };
        var invalid = await admin.PostAsJsonAsync("/api/v1/admin/questions", payload);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); // two correct options for a single-choice type

        var valid = payload with { options = new[] { new { text = "const", isCorrect = true, explanation = (string?)null }, new { text = "static", isCorrect = false, explanation = (string?)null } } };
        var created = await (await admin.PostAsJsonAsync("/api/v1/admin/questions", valid)).DataAsync();
        var preview = await (await admin.GetAsync($"/api/v1/admin/questions/{created.GetProperty("id")}")).DataAsync();
        Assert.Equal("Published", preview.GetProperty("status").GetString());
        Assert.True(preview.GetProperty("options")[0].GetProperty("isCorrect").GetBoolean());
    }

    [Fact]
    public async Task Csv_import_validates_rows_and_supports_dry_run()
    {
        var admin = await _factory.AdminAsync();
        var template = await (await admin.GetAsync("/api/v1/admin/questions/import-template?format=csv")).Content.ReadAsStringAsync();
        var header = template.Split('\n')[0].Trim().TrimStart('﻿');
        var csv = header + "\n" +
                  "it-imp-001,csharp,C# Fundamentals,,Imported question,Body text,,,MultipleChoice,Beginner,60,false,Published,import,Yes,No,,,,,1,Yes.,Because.,\n" +
                  "it-imp-002,unknown-tech,X,,Bad row,Body,,,MultipleChoice,Beginner,60,false,Draft,,A,B,,,,,1,A,B,\n";
        async Task<JsonElement> Import(bool dryRun)
        {
            using var content = new MultipartFormDataContent { { new StringContent(csv), "file", "questions.csv" } };
            return await (await admin.PostAsync($"/api/v1/admin/questions/import?dryRun={dryRun}", content)).DataAsync();
        }
        var dry = await Import(true);
        Assert.Equal(1, dry.GetProperty("created").GetInt32());
        Assert.Single(dry.GetProperty("errors").EnumerateArray());
        var real = await Import(false);
        Assert.Equal(1, real.GetProperty("created").GetInt32());
        var again = await Import(false);
        Assert.Equal(1, again.GetProperty("updated").GetInt32()); // idempotent by ExternalKey
    }
}

public class RateLimitFactory : ApiFactory
{
    protected override IDictionary<string, string?> Overrides => new Dictionary<string, string?> { ["RateLimits:AuthPerMinute"] = "3" };
}

public class RateLimitTests : IClassFixture<RateLimitFactory>
{
    private readonly RateLimitFactory _factory;
    public RateLimitTests(RateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        var client = _factory.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++) last = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "x@y.z" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Equal("rate_limited", await last.ErrorCodeAsync());
    }

    [Fact]
    public async Task Health_and_swagger_are_available()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/api/v1/mock-interviews/start", swagger);
    }
}
