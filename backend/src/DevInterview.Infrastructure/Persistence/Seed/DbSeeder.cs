using System.Text.Json;
using System.Text.Json.Serialization;
using DevInterview.Application.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevInterview.Infrastructure.Persistence.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public bool Enabled { get; set; } = true;
    /// <summary>Directory with the JSON content packs (defaults to SeedData next to the binaries).</summary>
    public string? DataPath { get; set; }
    /// <summary>Optional bootstrap Super Admin. Provide via environment variables only.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}

/// <summary>
/// Idempotent seeder. Reference data (roles, languages, plans, achievements) is upserted by code; content packs
/// (technologies, questions, challenges, learning paths, flashcards, companies) are upserted by stable keys so the
/// seeder can run on every start-up and administrators' later edits to non-seeded content are untouched.
/// </summary>
public sealed class DbSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppDbContext _db;
    private readonly RoleManager<AppRole> _roles;
    private readonly UserManager<AppUser> _users;
    private readonly SeedOptions _options;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(AppDbContext db, RoleManager<AppRole> roles, UserManager<AppUser> users, IOptions<SeedOptions> options, ILogger<DbSeeder> logger)
    {
        _db = db;
        _roles = roles;
        _users = users;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedAdminAsync();
        await SeedLanguagesAsync(ct);
        await SeedPlansAsync(ct);
        await SeedAchievementsAsync(ct);
        await SeedSettingsAsync(ct);

        var path = _options.DataPath ?? Path.Combine(AppContext.BaseDirectory, "SeedData");
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Seed data directory {Path} not found; content packs skipped", path);
            return;
        }
        var technologies = await Load<List<TechnologySeed>>(path, "technologies.json", ct) ?? new();
        await SeedTechnologiesAsync(technologies, ct);
        foreach (var file in Directory.GetFiles(Path.Combine(path, "questions"), "*.json").OrderBy(f => f))
            await SeedQuestionsAsync(await Load<List<QuestionSeed>>(Path.GetDirectoryName(file)!, Path.GetFileName(file), ct) ?? new(), ct);
        await SeedChallengesAsync(await Load<List<ChallengeSeed>>(path, "challenges.json", ct) ?? new(), ct);
        await SeedFlashcardsAsync(await Load<List<FlashcardSeed>>(path, "flashcards.json", ct) ?? new(), ct);
        await SeedSystemQuizzesAsync(ct);
        await SeedLearningPathsAsync(await Load<List<PathSeed>>(path, "learning-paths.json", ct) ?? new(), ct);
        await SeedCompaniesAsync(await Load<List<CompanySeed>>(path, "companies.json", ct) ?? new(), ct);
        _logger.LogInformation("Seeding complete: {Questions} questions, {Challenges} challenges, {Paths} learning paths",
            await _db.Questions.CountAsync(ct), await _db.CodingChallenges.CountAsync(ct), await _db.LearningPaths.CountAsync(ct));
    }

    private static async Task<T?> Load<T>(string dir, string file, CancellationToken ct)
    {
        var full = Path.Combine(dir, file);
        if (!File.Exists(full)) return default;
        await using var stream = File.OpenRead(full);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private async Task SeedRolesAsync()
    {
        var descriptions = new Dictionary<string, string>
        {
            [RoleNames.SuperAdmin] = "Full access including role management",
            [RoleNames.Admin] = "Manage users, content, plans and settings",
            [RoleNames.ContentManager] = "Create and publish learning content",
            [RoleNames.User] = "Regular application user"
        };
        foreach (var (name, description) in descriptions)
            if (!await _roles.RoleExistsAsync(name)) await _roles.CreateAsync(new AppRole(name) { Description = description });
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.AdminEmail) || string.IsNullOrWhiteSpace(_options.AdminPassword)) return;
        var user = await _users.FindByEmailAsync(_options.AdminEmail);
        if (user is null)
        {
            user = new AppUser { UserName = _options.AdminEmail, Email = _options.AdminEmail, EmailConfirmed = true, FirstName = "Platform", LastName = "Admin" };
            var result = await _users.CreateAsync(user, _options.AdminPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("Could not create bootstrap admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            _db.UserProfiles.Add(new UserProfile { UserId = user.Id, ExperienceLevel = ExperienceLevel.Expert });
            _db.UserPreferences.Add(new UserPreference { UserId = user.Id, ShowOnLeaderboard = false });
            await _db.SaveChangesAsync();
            _logger.LogInformation("Bootstrap Super Admin created");
        }
        foreach (var role in new[] { RoleNames.SuperAdmin, RoleNames.User })
            if (!await _users.IsInRoleAsync(user, role)) await _users.AddToRoleAsync(user, role);
    }

    private async Task SeedLanguagesAsync(CancellationToken ct)
    {
        var languages = new[]
        {
            ("csharp", "C#", "12 / .NET 8", ".cs"), ("javascript", "JavaScript", "Node.js 22", ".js"), ("python", "Python", "3.12", ".py"),
            ("java", "Java", "21", ".java"), ("cpp", "C++", "C++17 (GCC 14)", ".cpp"), ("kotlin", "Kotlin", "2.0 (JVM)", ".kt"), ("sql", "SQL", "SQLite 3", ".sql")
        };
        var existing = await _db.SupportedLanguages.ToListAsync(ct);
        for (var i = 0; i < languages.Length; i++)
        {
            var (code, name, version, ext) = languages[i];
            var lang = existing.FirstOrDefault(l => l.Code == code);
            if (lang is null) _db.SupportedLanguages.Add(lang = new SupportedLanguage { Code = code });
            lang.DisplayName = name; lang.Version = version; lang.FileExtension = ext; lang.DisplayOrder = i;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedPlansAsync(CancellationToken ct)
    {
        if (await _db.SubscriptionPlans.AnyAsync(ct)) return;
        _db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                Code = "free", Name = "Free", Tier = SubscriptionTier.Free, PriceMicros = 0, BillingPeriodMonths = 0, DisplayOrder = 0,
                Description = "Everything you need to start preparing.",
                FeaturesJson = Json.Serialize(new[] { "Daily question practice (limited)", "Basic quizzes", "Free learning paths", "Free coding challenges", "Basic progress analytics" })
            },
            new SubscriptionPlan
            {
                Code = "premium-monthly", Name = "Premium Monthly", Tier = SubscriptionTier.Premium, PriceMicros = 9_990_000, BillingPeriodMonths = 1, DisplayOrder = 1,
                GooglePlayProductId = "devinterview_premium", GooglePlayBasePlanId = "monthly",
                Description = "Unlimited practice and every AI feature.",
                FeaturesJson = Json.Serialize(new[] { "Unlimited practice (fair use)", "AI mock interviews", "AI answer evaluation", "AI code reviewer", "Advanced analytics", "All learning paths", "Company-specific preparation" })
            },
            new SubscriptionPlan
            {
                Code = "premium-yearly", Name = "Premium Yearly", Tier = SubscriptionTier.Premium, PriceMicros = 79_990_000, BillingPeriodMonths = 12, DisplayOrder = 2,
                GooglePlayProductId = "devinterview_premium", GooglePlayBasePlanId = "yearly",
                Description = "Best value — two months free.",
                FeaturesJson = Json.Serialize(new[] { "Everything in Premium Monthly", "Detailed performance reports", "Priority access to new content" })
            });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedAchievementsAsync(CancellationToken ct)
    {
        var defs = new[]
        {
            (AchievementCodes.FirstQuiz, "First Quiz", "Complete your first quiz.", "quiz", 20),
            (AchievementCodes.QuizAce, "Quiz Ace", "Score 100% on a quiz with at least 5 questions.", "star", 50),
            (AchievementCodes.CSharpBeginner, "C# Beginner", $"Answer {AchievementCodes.CSharpBeginnerCorrect} C# questions correctly.", "code", 30),
            (AchievementCodes.SqlMaster, "SQL Master", $"Answer {AchievementCodes.SqlMasterCorrect} SQL questions correctly.", "database", 100),
            (AchievementCodes.ProblemSolver, "Problem Solver", "Solve your first coding challenge.", "puzzle", 30),
            (AchievementCodes.CodingWarrior, "Coding Warrior", $"Solve {AchievementCodes.CodingWarriorSolved} coding challenges.", "sword", 100),
            (AchievementCodes.SevenDayStreak, "Seven-Day Streak", "Practise seven days in a row.", "flame", 70),
            (AchievementCodes.ThirtyDayStreak, "Thirty-Day Streak", "Practise thirty days in a row.", "flame", 250),
            (AchievementCodes.FirstMockInterview, "First Interview", "Complete your first AI mock interview.", "mic", 40),
            (AchievementCodes.InterviewChampion, "Interview Champion", $"Score {AchievementCodes.InterviewChampionScore}+ in a mock interview.", "trophy", 150),
            (AchievementCodes.Scholar, "Scholar", $"Complete {AchievementCodes.ScholarLessons} lessons.", "book", 80),
            (AchievementCodes.Centurion, "Centurion", $"Attempt {AchievementCodes.CenturionAttempts} questions.", "target", 80)
        };
        var existing = await _db.Achievements.ToListAsync(ct);
        foreach (var (code, name, description, icon, xp) in defs)
        {
            var a = existing.FirstOrDefault(x => x.Code == code);
            if (a is null) _db.Achievements.Add(a = new Achievement { Code = code });
            a.Name = name; a.Description = description; a.IconKey = icon; a.XpReward = xp;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["support.email"] = ("support@devinterview.app", "Support contact shown in Help & Support"),
            ["app.minSupportedVersionCode"] = ("1", "Android clients below this versionCode are asked to update"),
            ["content.disclaimer.companies"] = ("Company sections contain illustrative practice content unless marked Verified.", "Disclaimer shown on company pages")
        };
        var keys = defaults.Keys.ToList();
        var existing = await _db.AppSettings.Where(s => keys.Contains(s.Key)).Select(s => s.Key).ToListAsync(ct);
        foreach (var (key, value) in defaults.Where(d => !existing.Contains(d.Key)))
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value.Value, Description = value.Description });
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Content packs ----------------
    private async Task SeedTechnologiesAsync(List<TechnologySeed> seeds, CancellationToken ct)
    {
        var techs = await _db.Technologies.Include(t => t.Categories).ToListAsync(ct);
        for (var i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            var t = techs.FirstOrDefault(x => x.Slug == s.Slug);
            if (t is null) { t = new Technology { Slug = s.Slug }; _db.Technologies.Add(t); techs.Add(t); }
            t.Name = s.Name; t.Description = s.Description; t.Group = s.Group; t.IconKey = s.Icon; t.DisplayOrder = i;
            for (var c = 0; c < s.Categories.Count; c++)
            {
                var slug = Slug.From(s.Categories[c]);
                var cat = t.Categories.FirstOrDefault(x => x.Slug == slug);
                if (cat is null) t.Categories.Add(cat = new Category { Slug = slug });
                cat.Name = s.Categories[c]; cat.DisplayOrder = c;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedQuestionsAsync(List<QuestionSeed> seeds, CancellationToken ct)
    {
        if (seeds.Count == 0) return;
        var techs = await _db.Technologies.Include(t => t.Categories).ToDictionaryAsync(t => t.Slug, ct);
        var keys = seeds.Select(s => s.Key).ToList();
        var existing = await _db.Questions.Include(q => q.Options).Include(q => q.Answer).Include(q => q.Tags)
            .Where(q => q.ExternalKey != null && keys.Contains(q.ExternalKey)).ToDictionaryAsync(q => q.ExternalKey!, ct);
        var tags = await _db.Tags.ToDictionaryAsync(t => t.Slug, ct);

        foreach (var s in seeds)
        {
            if (!techs.TryGetValue(s.Tech, out var tech)) { _logger.LogWarning("Seed question {Key}: unknown technology {Tech}", s.Key, s.Tech); continue; }
            var category = tech.Categories.FirstOrDefault(c => c.Slug == Slug.From(s.Category));
            if (category is null) { _logger.LogWarning("Seed question {Key}: unknown category {Category}", s.Key, s.Category); continue; }
            var options = s.Options ?? new List<OptionSeed>();
            var contentErrors = Application.Features.Admin.AdminContentService.ValidateQuestionContent(s.Type, ContentStatus.Published,
                options.Select(o => new Contracts.Admin.OptionUpsertDto(o.T, o.C, o.E)).ToList(), s.Answer);
            if (contentErrors.Count > 0) { _logger.LogWarning("Seed question {Key} skipped: {Error}", s.Key, contentErrors[0]); continue; }

            if (!existing.TryGetValue(s.Key, out var q))
            {
                q = new Question { ExternalKey = s.Key, PublishedAt = DateTime.UtcNow, Status = ContentStatus.Published };
                _db.Questions.Add(q);
            }
            q.TechnologyId = tech.Id; q.CategoryId = category.Id; q.Title = s.Title; q.Body = s.Body; q.CodeSnippet = s.Code; q.CodeLanguage = s.Lang;
            q.Type = s.Type; q.Difficulty = s.Difficulty; q.EstimatedSeconds = s.Seconds ?? 90; q.IsPremium = s.Premium;
            var current = q.Options.OrderBy(o => o.DisplayOrder).ToList();
            for (var i = 0; i < options.Count; i++)
            {
                var o = i < current.Count ? current[i] : new QuestionOption();
                if (i >= current.Count) q.Options.Add(o);
                o.Text = options[i].T; o.IsCorrect = options[i].C; o.Explanation = options[i].E; o.DisplayOrder = i;
            }
            foreach (var extra in current.Skip(options.Count)) _db.QuestionOptions.Remove(extra);
            q.Answer ??= new QuestionAnswer();
            q.Answer.ShortAnswer = s.Answer; q.Answer.DetailedExplanation = s.Explanation; q.Answer.ReferenceAnswer = s.Reference;
            foreach (var tagName in s.Tags ?? new List<string>())
            {
                var slug = Slug.From(tagName);
                if (!tags.TryGetValue(slug, out var tag)) { tag = new Tag { Name = tagName, Slug = slug }; _db.Tags.Add(tag); tags[slug] = tag; }
                if (q.Tags.All(t => t.TagId != tag.Id)) q.Tags.Add(new QuestionTag { Tag = tag, TagId = tag.Id });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedChallengesAsync(List<ChallengeSeed> seeds, CancellationToken ct)
    {
        var existing = await _db.CodingChallenges.Include(c => c.TestCases).Include(c => c.StarterCodes)
            .Where(c => c.ExternalKey != null).ToDictionaryAsync(c => c.ExternalKey!, ct);
        foreach (var s in seeds)
        {
            if (!existing.TryGetValue(s.Key, out var c))
            {
                c = new CodingChallenge { ExternalKey = s.Key, Status = ContentStatus.Published };
                _db.CodingChallenges.Add(c);
            }
            c.Title = s.Title; c.Slug = Slug.From(s.Title); c.Category = s.Category; c.Difficulty = s.Difficulty; c.ProblemStatement = s.Statement;
            c.InputDescription = s.Input; c.OutputDescription = s.Output; c.Constraints = s.Constraints; c.ExpectedTimeComplexity = s.TimeComplexity;
            c.ExpectedSpaceComplexity = s.SpaceComplexity; c.HintsJson = Json.Serialize(s.Hints); c.SolutionExplanation = s.Solution; c.IsPremium = s.Premium;
            c.TimeLimitMs = s.TimeLimitMs ?? 2000; c.MemoryLimitMb = s.MemoryLimitMb ?? 256;
            c.AllowedLanguages = s.Category == ChallengeCategory.Sql ? "sql" : "csharp,javascript,python,java,cpp,kotlin";
            // Test cases and starter code are owned by the seed: replace them wholesale (keeps results' FK nullable).
            foreach (var tc in c.TestCases.ToList())
            {
                await _db.CodeSubmissionResults.Where(r => r.TestCaseId == tc.Id).ExecuteUpdateAsync(x => x.SetProperty(r => r.TestCaseId, (Guid?)null), ct);
                _db.ChallengeTestCases.Remove(tc);
            }
            c.TestCases.Clear();
            for (var i = 0; i < s.Tests.Count; i++)
                c.TestCases.Add(new ChallengeTestCase { Input = s.Tests[i].In, ExpectedOutput = s.Tests[i].Out, IsSample = s.Tests[i].Sample, Explanation = s.Tests[i].Why, DisplayOrder = i });
            foreach (var sc in c.StarterCodes.ToList()) _db.ChallengeStarterCodes.Remove(sc);
            c.StarterCodes.Clear();
            foreach (var (lang, code) in s.Starter) c.StarterCodes.Add(new ChallengeStarterCode { LanguageCode = lang, Code = code });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedFlashcardsAsync(List<FlashcardSeed> seeds, CancellationToken ct)
    {
        var techs = await _db.Technologies.ToDictionaryAsync(t => t.Slug, t => t.Id, ct);
        var existing = await _db.Flashcards.Where(f => f.ExternalKey != null).ToDictionaryAsync(f => f.ExternalKey!, ct);
        foreach (var s in seeds)
        {
            if (!existing.TryGetValue(s.Key, out var f)) { f = new Flashcard { ExternalKey = s.Key }; _db.Flashcards.Add(f); }
            f.TechnologyId = techs.TryGetValue(s.Tech, out var id) ? id : null; f.Front = s.Front; f.Back = s.Back; f.Difficulty = s.Difficulty; f.IsPublished = true;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedLearningPathsAsync(List<PathSeed> seeds, CancellationToken ct)
    {
        var techs = await _db.Technologies.ToDictionaryAsync(t => t.Slug, t => t.Id, ct);
        var questionIds = await _db.Questions.Where(q => q.ExternalKey != null).ToDictionaryAsync(q => q.ExternalKey!, q => q.Id, ct);
        var quizIds = (await _db.Quizzes.Where(q => q.IsSystem && !q.IsDeleted).Select(q => new { q.Title, q.Id }).ToListAsync(ct))
            .GroupBy(q => q.Title).ToDictionary(g => g.Key, g => g.First().Id);
        for (var p = 0; p < seeds.Count; p++)
        {
            var s = seeds[p];
            var path = await _db.LearningPaths.Include(x => x.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Questions).AsSplitQuery()
                .FirstOrDefaultAsync(x => x.ExternalKey == s.Key, ct);
            if (path is null) { path = new LearningPath { ExternalKey = s.Key, IsPublished = true }; _db.LearningPaths.Add(path); }
            path.Title = s.Title; path.Slug = Slug.From(s.Title); path.Description = s.Description; path.Level = s.Level; path.EstimatedHours = s.Hours;
            path.IsPremium = s.Premium; path.DisplayOrder = p; path.TechnologyId = s.Tech is not null && techs.TryGetValue(s.Tech, out var tid) ? tid : null;
            for (var m = 0; m < s.Modules.Count; m++)
            {
                var ms = s.Modules[m];
                var module = path.Modules.FirstOrDefault(x => x.DisplayOrder == m);
                if (module is null) path.Modules.Add(module = new LearningModule { DisplayOrder = m });
                module.Title = ms.Title; module.Description = ms.Description;
                for (var l = 0; l < ms.Lessons.Count; l++)
                {
                    var ls = ms.Lessons[l];
                    var lesson = module.Lessons.FirstOrDefault(x => x.DisplayOrder == l);
                    if (lesson is null) module.Lessons.Add(lesson = new LearningLesson { DisplayOrder = l });
                    lesson.Title = ls.Title; lesson.Content = ls.Content; lesson.Type = ls.Type; lesson.EstimatedMinutes = ls.Minutes; lesson.ExerciseInstructions = ls.Exercise;
                    lesson.QuizId = ls.Quiz is not null && quizIds.TryGetValue(ls.Quiz, out var quizId) ? quizId : null;
                    var wanted = (ls.Questions ?? new List<string>()).Where(questionIds.ContainsKey).Select(k => questionIds[k]).Distinct().ToList();
                    foreach (var lq in lesson.Questions.Where(x => !wanted.Contains(x.QuestionId)).ToList()) lesson.Questions.Remove(lq);
                    for (var qi = 0; qi < wanted.Count; qi++)
                        if (lesson.Questions.All(x => x.QuestionId != wanted[qi])) lesson.Questions.Add(new LessonQuestion { QuestionId = wanted[qi], DisplayOrder = qi });
                }
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedCompaniesAsync(List<CompanySeed> seeds, CancellationToken ct)
    {
        var techs = await _db.Technologies.ToDictionaryAsync(t => t.Slug, t => t.Id, ct);
        var questionIds = await _db.Questions.Where(q => q.ExternalKey != null).ToDictionaryAsync(q => q.ExternalKey!, q => q.Id, ct);
        var challengeIds = await _db.CodingChallenges.Where(q => q.ExternalKey != null).ToDictionaryAsync(q => q.ExternalKey!, q => q.Id, ct);
        for (var i = 0; i < seeds.Count; i++)
        {
            var s = seeds[i];
            var slug = Slug.From(s.Name);
            if (await _db.Companies.AnyAsync(c => c.Slug == slug, ct)) continue; // companies are admin-managed after the first seed
            var company = new Company
            {
                Name = s.Name, Slug = slug, Overview = s.Overview, OverviewSource = ContentSource.Illustrative, Industry = s.Industry, WebsiteUrl = s.Website,
                DifficultyLevel = s.Difficulty, DisplayOrder = i, IsPremium = s.Premium
            };
            for (var r = 0; r < s.Rounds.Count; r++)
                company.Rounds.Add(new CompanyInterviewRound { DisplayOrder = r, Name = s.Rounds[r].Name, Description = s.Rounds[r].Description, Source = ContentSource.Illustrative });
            for (var t = 0; t < s.Topics.Count; t++)
                company.Topics.Add(new CompanyTopic { DisplayOrder = t, Name = s.Topics[t].Name, Description = s.Topics[t].Description,
                    TechnologyId = s.Topics[t].Tech is not null && techs.TryGetValue(s.Topics[t].Tech!, out var id) ? id : null });
            foreach (var col in s.Collections)
            {
                var collection = new CompanyQuestionCollection { Title = col.Title, Description = col.Description, Kind = col.Kind, Source = ContentSource.Illustrative };
                var qs = col.Questions.Where(questionIds.ContainsKey).Select(k => questionIds[k]).Distinct().ToList();
                for (var q = 0; q < qs.Count; q++) collection.Questions.Add(new CompanyCollectionQuestion { QuestionId = qs[q], DisplayOrder = q });
                var cs = col.Challenges.Where(challengeIds.ContainsKey).Select(k => challengeIds[k]).Distinct().ToList();
                for (var c = 0; c < cs.Count; c++) collection.Challenges.Add(new CompanyCollectionChallenge { ChallengeId = cs[c], DisplayOrder = c });
                company.Collections.Add(collection);
            }
            _db.Companies.Add(company);
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>One curated "fundamentals" quiz per technology with enough objective questions.</summary>
    private async Task SeedSystemQuizzesAsync(CancellationToken ct)
    {
        var techs = await _db.Technologies.Where(t => t.IsActive).ToListAsync(ct);
        foreach (var tech in techs)
        {
            var title = $"{tech.Name} Fundamentals";
            if (await _db.Quizzes.AnyAsync(q => q.IsSystem && q.Title == title, ct)) continue;
            var ids = await _db.Questions.Where(q => q.TechnologyId == tech.Id && q.Status == ContentStatus.Published && !q.IsPremium && q.Options.Any(o => o.IsCorrect))
                .OrderBy(q => q.Difficulty).ThenBy(q => q.Title).Select(q => q.Id).Take(10).ToListAsync(ct);
            if (ids.Count < 5) continue;
            var quiz = new Quiz
            {
                Title = title, Description = $"A {ids.Count}-question check of core {tech.Name} concepts.", Mode = QuizMode.Technology, IsSystem = true,
                IsPublished = true, TechnologyId = tech.Id, TimeLimitSeconds = ids.Count * 90, PassingPercentage = 60
            };
            for (var i = 0; i < ids.Count; i++) quiz.Questions.Add(new QuizQuestion { QuestionId = ids[i], DisplayOrder = i });
            _db.Quizzes.Add(quiz);
        }
        var assessment = "Full-Stack .NET Assessment";
        if (!await _db.Quizzes.AnyAsync(q => q.IsSystem && q.Title == assessment, ct))
        {
            var slugs = new[] { "csharp", "aspnet-core", "sql-server", "javascript", "react" };
            var ids = await _db.Questions.Where(q => slugs.Contains(q.Technology.Slug) && q.Status == ContentStatus.Published && q.Options.Any(o => o.IsCorrect) &&
                    (q.Difficulty == Difficulty.Intermediate || q.Difficulty == Difficulty.Advanced))
                .OrderBy(q => q.Title).Select(q => q.Id).Take(25).ToListAsync(ct);
            if (ids.Count >= 10)
            {
                var quiz = new Quiz
                {
                    Title = assessment, Description = "A timed, full-length assessment across C#, ASP.NET Core, SQL Server, JavaScript and React. Negative marking applies.",
                    Mode = QuizMode.Assessment, IsSystem = true, IsPublished = true, IsPremium = true, TimeLimitSeconds = 30 * 60, PassingPercentage = 65,
                    NegativeMarkingFraction = 0.25m
                };
                for (var i = 0; i < ids.Count; i++) quiz.Questions.Add(new QuizQuestion { QuestionId = ids[i], DisplayOrder = i });
                _db.Quizzes.Add(quiz);
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Seed file shapes ----------------
    private sealed record TechnologySeed(string Slug, string Name, string Group, string? Description, string? Icon, List<string> Categories);
    private sealed record OptionSeed(string T, bool C, string? E);
    private sealed record QuestionSeed(string Key, string Tech, string Category, QuestionType Type, Difficulty Difficulty, string Title, string Body,
        string? Code, string? Lang, List<OptionSeed>? Options, string Answer, string Explanation, string? Reference, List<string>? Tags, int? Seconds, bool Premium);
    private sealed record TestSeed(string In, string Out, bool Sample, string? Why);
    private sealed record ChallengeSeed(string Key, string Title, ChallengeCategory Category, ChallengeDifficulty Difficulty, string Statement, string Input,
        string Output, string Constraints, string? TimeComplexity, string? SpaceComplexity, List<string> Hints, string Solution, bool Premium,
        int? TimeLimitMs, int? MemoryLimitMb, List<TestSeed> Tests, Dictionary<string, string> Starter);
    private sealed record FlashcardSeed(string Key, string Tech, string Front, string Back, Difficulty Difficulty);
    private sealed record LessonSeed(string Title, LessonType Type, int Minutes, string Content, string? Exercise, List<string>? Questions, string? Quiz);
    private sealed record ModuleSeed(string Title, string? Description, List<LessonSeed> Lessons);
    private sealed record PathSeed(string Key, string Title, string Description, ExperienceLevel Level, int Hours, bool Premium, string? Tech, List<ModuleSeed> Modules);
    private sealed record RoundSeed(string Name, string Description);
    private sealed record CompanyTopicSeed(string Name, string? Description, string? Tech);
    private sealed record CompanyCollectionSeed(string Title, string? Description, CompanyCollectionKind Kind, List<string> Questions, List<string> Challenges);
    private sealed record CompanySeed(string Name, string Overview, string? Industry, string? Website, Difficulty Difficulty, bool Premium,
        List<RoundSeed> Rounds, List<CompanyTopicSeed> Topics, List<CompanyCollectionSeed> Collections);
}
