using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevInterview.UnitTests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>Relational SQLite in-memory database with the real EF Core model, one per test.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public FakeClock Clock { get; } = new();

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = Create();
        ctx.Database.EnsureCreated();
    }

    public AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, Clock);

    public void Dispose() => _connection.Dispose();

    public static IOptions<T> Options<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);

    /// <summary>Seeds a user, a technology/category and objective questions; returns ids.</summary>
    public async Task<(Guid UserId, Guid TechId, List<Question> Questions)> SeedBasicsAsync(int questions = 6)
    {
        await using var db = Create();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = $"u{Guid.NewGuid():N}@x.io", Email = $"u{Guid.NewGuid():N}@x.io", FirstName = "Test", LastName = "User", SecurityStamp = Guid.NewGuid().ToString() };
        db.Users.Add(user);
        db.UserPreferences.Add(new UserPreference { UserId = user.Id, WeeklyGoalMinutes = 60 });
        var tech = new Technology { Name = "C#", Slug = "csharp" };
        var cat = new Category { Technology = tech, Name = "Basics", Slug = "basics" };
        db.Technologies.Add(tech);
        var list = new List<Question>();
        for (var i = 0; i < questions; i++)
        {
            var q = new Question
            {
                Technology = tech, Category = cat, Title = $"Q{i}", Body = "Body", Type = QuestionType.MultipleChoice,
                Difficulty = (Difficulty)(i % 4), Status = ContentStatus.Published, PublishedAt = Clock.UtcNow,
                Answer = new QuestionAnswer { ShortAnswer = "A", DetailedExplanation = "Because" }
            };
            q.Options.Add(new QuestionOption { Text = "right", IsCorrect = true, DisplayOrder = 0 });
            q.Options.Add(new QuestionOption { Text = "wrong", IsCorrect = false, DisplayOrder = 1 });
            db.Questions.Add(q);
            list.Add(q);
        }
        await db.SaveChangesAsync();
        return (user.Id, tech.Id, list);
    }

    public async Task GrantPremiumAsync(Guid userId, DateTime expires)
    {
        await using var db = Create();
        var plan = new SubscriptionPlan { Code = $"p{Guid.NewGuid():N}"[..10], Name = "Premium", Tier = SubscriptionTier.Premium };
        db.SubscriptionPlans.Add(plan);
        db.UserSubscriptions.Add(new UserSubscription { UserId = userId, Plan = plan, Status = SubscriptionStatus.Active, StartsAt = Clock.UtcNow.AddDays(-1), ExpiresAt = expires });
        await db.SaveChangesAsync();
    }
}

public static class Limits
{
    public static PlanLimitsOptions Default => new()
    {
        FreeDailyQuestionViews = 3, FreeDailyCodeRuns = 2, PremiumDailyAnswerEvaluations = 2, PremiumDailyMockInterviews = 2, FreeMaxQuizQuestions = 20
    };
}
