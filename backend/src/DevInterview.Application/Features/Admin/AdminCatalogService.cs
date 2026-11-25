using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Admin;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Admin;

public interface IAdminCatalogService
{
    Task<PagedResult<AdminQuizDto>> GetQuizzesAsync(PageQuery query, CancellationToken ct);
    Task<AdminQuizDto> GetQuizAsync(Guid id, CancellationToken ct);
    Task<AdminQuizDto> UpsertQuizAsync(Guid? id, QuizUpsertRequest request, CancellationToken ct);
    Task DeleteQuizAsync(Guid id, CancellationToken ct);

    Task<PagedResult<AdminChallengeListItemDto>> GetChallengesAsync(PageQuery query, CancellationToken ct);
    Task<AdminChallengeDto> GetChallengeAsync(Guid id, CancellationToken ct);
    Task<AdminChallengeDto> UpsertChallengeAsync(Guid? id, ChallengeUpsertRequest request, CancellationToken ct);
    Task DeleteChallengeAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<AdminLearningPathListItemDto>> GetPathsAsync(CancellationToken ct);
    Task<AdminLearningPathDto> GetPathAsync(Guid id, CancellationToken ct);
    Task<AdminLearningPathDto> UpsertPathAsync(Guid? id, LearningPathUpsertRequest request, CancellationToken ct);
    Task DeletePathAsync(Guid id, CancellationToken ct);

    Task<PagedResult<AdminFlashcardDto>> GetFlashcardsAsync(AdminFlashcardQuery query, CancellationToken ct);
    Task<AdminFlashcardDto> UpsertFlashcardAsync(Guid? id, FlashcardUpsertRequest request, CancellationToken ct);
    Task DeleteFlashcardAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<AdminCompanyDto>> GetCompaniesAsync(CancellationToken ct);
    Task<AdminCompanyDto> GetCompanyAsync(Guid id, CancellationToken ct);
    Task<AdminCompanyDto> UpsertCompanyAsync(Guid? id, CompanyUpsertRequest request, CancellationToken ct);
    Task DeleteCompanyAsync(Guid id, CancellationToken ct);
}

public sealed class AdminCatalogService : IAdminCatalogService
{
    private readonly IAppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public AdminCatalogService(IAppDbContext db, IAuditLogger audit, IClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    // ---------------- Quizzes ----------------
    private IQueryable<AdminQuizDto> ProjectQuiz(IQueryable<Quiz> q) => q.Select(x => new AdminQuizDto(x.Id, x.Title, x.Description, x.Mode.ToString(),
        x.TimeLimitSeconds, x.PassingPercentage, x.NegativeMarkingFraction, x.IsPublished, x.IsPremium, x.TechnologyId,
        x.Difficulty == null ? null : x.Difficulty.ToString(), x.Questions.OrderBy(qq => qq.DisplayOrder).Select(qq => qq.QuestionId).ToList(),
        _db.QuizAttempts.Count(a => a.QuizId == x.Id), x.UpdatedAt));

    public async Task<PagedResult<AdminQuizDto>> GetQuizzesAsync(PageQuery query, CancellationToken ct)
    {
        var q = _db.Quizzes.AsNoTracking().Where(x => x.IsSystem && !x.IsDeleted && x.Mode != QuizMode.Daily);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(x => x.Title.Contains(query.Search));
        return await ProjectQuiz(q.OrderByDescending(x => x.UpdatedAt)).ToPagedAsync(query, ct);
    }

    public async Task<AdminQuizDto> GetQuizAsync(Guid id, CancellationToken ct) =>
        await ProjectQuiz(_db.Quizzes.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Quiz", id);

    public async Task<AdminQuizDto> UpsertQuizAsync(Guid? id, QuizUpsertRequest r, CancellationToken ct)
    {
        var quiz = id is null ? new Quiz { IsSystem = true } :
            await _db.Quizzes.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id && x.IsSystem && !x.IsDeleted, ct) ?? throw new NotFoundException("Quiz", id);
        if (id is null) _db.Quizzes.Add(quiz);
        var ids = r.QuestionIds.Distinct().ToList();
        var valid = await _db.Questions.Where(q => ids.Contains(q.Id) && !q.IsDeleted && q.Options.Any(o => o.IsCorrect)).Select(q => q.Id).ToListAsync(ct);
        if (valid.Count != ids.Count) throw ValidationAppException.For("questionIds", "All quiz questions must exist and be auto-gradable (have a correct option).");
        if (r.IsPublished && ids.Count == 0) throw ValidationAppException.For("questionIds", "A published quiz needs at least one question.");
        quiz.Title = r.Title.Trim(); quiz.Description = r.Description?.Trim(); quiz.Mode = EnumParser.Parse<QuizMode>(r.Mode, "mode");
        quiz.TimeLimitSeconds = r.TimeLimitSeconds; quiz.PassingPercentage = r.PassingPercentage; quiz.NegativeMarkingFraction = r.NegativeMarkingFraction;
        quiz.IsPublished = r.IsPublished; quiz.IsPremium = r.IsPremium; quiz.TechnologyId = r.TechnologyId;
        quiz.Difficulty = EnumParser.ParseOptional<Difficulty>(r.Difficulty, "difficulty");
        foreach (var existing in quiz.Questions.ToList()) _db.QuizQuestions.Remove(existing);
        quiz.Questions.Clear();
        for (var i = 0; i < ids.Count; i++) quiz.Questions.Add(new QuizQuestion { QuestionId = ids[i], DisplayOrder = i });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Quiz), quiz.Id.ToString(), new { quiz.Title, Questions = ids.Count }, ct);
        return await GetQuizAsync(quiz.Id, ct);
    }

    public async Task DeleteQuizAsync(Guid id, CancellationToken ct)
    {
        var quiz = await _db.Quizzes.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Quiz", id);
        quiz.IsDeleted = true; quiz.DeletedAt = _clock.UtcNow; quiz.IsPublished = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(Quiz), id.ToString(), null, ct);
    }

    // ---------------- Challenges ----------------
    public async Task<PagedResult<AdminChallengeListItemDto>> GetChallengesAsync(PageQuery query, CancellationToken ct)
    {
        var q = _db.CodingChallenges.AsNoTracking().Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(c => c.Title.Contains(query.Search));
        return await q.OrderBy(c => c.Difficulty).ThenBy(c => c.Title)
            .Select(c => new AdminChallengeListItemDto(c.Id, c.Title, c.Category.ToString(), c.Difficulty.ToString(), c.Status.ToString(), c.IsPremium,
                c.TestCases.Count, _db.CodeSubmissions.Count(s => s.ChallengeId == c.Id))).ToPagedAsync(query, ct);
    }

    public async Task<AdminChallengeDto> GetChallengeAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CodingChallenges.AsNoTracking().Include(x => x.TestCases).Include(x => x.StarterCodes).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Challenge", id);
        return new AdminChallengeDto(c.Id, c.Title, c.Slug, c.Category.ToString(), c.Difficulty.ToString(), c.ProblemStatement, c.InputDescription,
            c.OutputDescription, c.Constraints, c.ExpectedTimeComplexity, c.ExpectedSpaceComplexity, Json.StringList(c.HintsJson), c.SolutionExplanation,
            c.IsPremium, c.Status.ToString(), c.TimeLimitMs, c.MemoryLimitMb, c.AllowedLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries),
            c.StarterCodes.ToDictionary(s => s.LanguageCode, s => s.Code),
            c.TestCases.OrderBy(t => t.DisplayOrder).Select(t => new TestCaseUpsertDto(t.Id, t.Input, t.ExpectedOutput, t.IsSample, t.Explanation)).ToList(), c.UpdatedAt);
    }

    public async Task<AdminChallengeDto> UpsertChallengeAsync(Guid? id, ChallengeUpsertRequest r, CancellationToken ct)
    {
        var c = id is null ? new CodingChallenge() :
            await _db.CodingChallenges.Include(x => x.TestCases).Include(x => x.StarterCodes).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Challenge", id);
        var status = EnumParser.Parse<ContentStatus>(r.Status, "status");
        var cases = r.TestCases ?? Array.Empty<TestCaseUpsertDto>();
        if (status == ContentStatus.Published && (cases.Count(t => t.IsSample) == 0 || cases.Count(t => !t.IsSample) == 0))
            throw ValidationAppException.For("testCases", "A published challenge needs at least one sample and one hidden test case.");
        var slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Title : r.Slug);
        if (await _db.CodingChallenges.AnyAsync(x => x.Slug == slug && x.Id != id, ct)) throw new ConflictException($"Slug '{slug}' is already used.");
        var languages = r.AllowedLanguages.Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0).Distinct().ToList();
        var known = await _db.SupportedLanguages.Select(l => l.Code).ToListAsync(ct);
        if (languages.Count == 0 || languages.Any(l => !known.Contains(l))) throw ValidationAppException.For("allowedLanguages", $"Languages must be among: {string.Join(", ", known)}.");

        if (id is null) _db.CodingChallenges.Add(c);
        c.Title = r.Title.Trim(); c.Slug = slug; c.Category = EnumParser.Parse<ChallengeCategory>(r.Category, "category");
        c.Difficulty = EnumParser.Parse<ChallengeDifficulty>(r.Difficulty, "difficulty"); c.ProblemStatement = r.ProblemStatement; c.InputDescription = r.InputDescription;
        c.OutputDescription = r.OutputDescription; c.Constraints = r.Constraints; c.ExpectedTimeComplexity = r.ExpectedTimeComplexity; c.ExpectedSpaceComplexity = r.ExpectedSpaceComplexity;
        c.HintsJson = Json.Serialize(r.Hints ?? Array.Empty<string>()); c.SolutionExplanation = r.SolutionExplanation; c.IsPremium = r.IsPremium; c.Status = status;
        c.TimeLimitMs = r.TimeLimitMs; c.MemoryLimitMb = r.MemoryLimitMb; c.AllowedLanguages = string.Join(',', languages);

        // Test cases: update by id, add new, remove missing. Submission results keep a nullable FK.
        var keep = cases.Where(t => t.Id is not null).Select(t => t.Id!.Value).ToHashSet();
        foreach (var removed in c.TestCases.Where(t => !keep.Contains(t.Id)).ToList())
        {
            await _db.CodeSubmissionResults.Where(x => x.TestCaseId == removed.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TestCaseId, (Guid?)null), ct);
            _db.ChallengeTestCases.Remove(removed);
        }
        for (var i = 0; i < cases.Count; i++)
        {
            var dto = cases[i];
            var tc = dto.Id is { } tid ? c.TestCases.FirstOrDefault(t => t.Id == tid) : null;
            if (tc is null) { tc = new ChallengeTestCase(); c.TestCases.Add(tc); }
            tc.Input = dto.Input; tc.ExpectedOutput = dto.ExpectedOutput; tc.IsSample = dto.IsSample; tc.Explanation = dto.Explanation; tc.DisplayOrder = i;
        }
        foreach (var sc in c.StarterCodes.ToList()) _db.ChallengeStarterCodes.Remove(sc);
        c.StarterCodes.Clear();
        foreach (var (lang, code) in r.StarterCode ?? new Dictionary<string, string>())
            if (languages.Contains(lang.ToLowerInvariant())) c.StarterCodes.Add(new ChallengeStarterCode { LanguageCode = lang.ToLowerInvariant(), Code = code });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(CodingChallenge), c.Id.ToString(), new { c.Title, TestCases = cases.Count }, ct);
        return await GetChallengeAsync(c.Id, ct);
    }

    public async Task DeleteChallengeAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.CodingChallenges.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Challenge", id);
        c.IsDeleted = true; c.DeletedAt = _clock.UtcNow; c.Status = ContentStatus.Archived;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(CodingChallenge), id.ToString(), null, ct);
    }

    // ---------------- Learning paths ----------------
    public async Task<IReadOnlyList<AdminLearningPathListItemDto>> GetPathsAsync(CancellationToken ct) =>
        await _db.LearningPaths.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.DisplayOrder)
            .Select(p => new AdminLearningPathListItemDto(p.Id, p.Title, p.Level.ToString(), p.IsPublished, p.IsPremium, p.Modules.Count, p.Modules.SelectMany(m => m.Lessons).Count()))
            .ToListAsync(ct);

    public async Task<AdminLearningPathDto> GetPathAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.LearningPaths.AsNoTracking().Include(x => x.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Questions).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Learning path", id);
        var enrolled = await _db.UserLearningProgress.Where(x => x.PathId == id).Select(x => x.UserId).Distinct().CountAsync(ct);
        return new AdminLearningPathDto(p.Id, p.Title, p.Slug, p.Description, p.Level.ToString(), p.EstimatedHours, p.IsPremium, p.IsPublished, p.DisplayOrder, p.TechnologyId,
            p.Modules.OrderBy(m => m.DisplayOrder).Select(m => new ModuleUpsertDto(m.Id, m.Title, m.Description,
                m.Lessons.OrderBy(l => l.DisplayOrder).Select(l => new LessonUpsertDto(l.Id, l.Title, l.Content, l.Type.ToString(), l.EstimatedMinutes,
                    l.ExerciseInstructions, l.QuizId, l.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.QuestionId).ToList())).ToList())).ToList(), enrolled);
    }

    public async Task<AdminLearningPathDto> UpsertPathAsync(Guid? id, LearningPathUpsertRequest r, CancellationToken ct)
    {
        var path = id is null ? new LearningPath() :
            await _db.LearningPaths.Include(x => x.Modules).ThenInclude(m => m.Lessons).ThenInclude(l => l.Questions).AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Learning path", id);
        var slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Title : r.Slug);
        if (await _db.LearningPaths.AnyAsync(p => p.Slug == slug && p.Id != id, ct)) throw new ConflictException($"Slug '{slug}' is already used.");
        if (id is null) _db.LearningPaths.Add(path);
        path.Title = r.Title.Trim(); path.Slug = slug; path.Description = r.Description; path.Level = EnumParser.Parse<ExperienceLevel>(r.Level, "level");
        path.EstimatedHours = r.EstimatedHours; path.IsPremium = r.IsPremium; path.IsPublished = r.IsPublished; path.DisplayOrder = r.DisplayOrder; path.TechnologyId = r.TechnologyId;

        // Modules and lessons are matched by id so user progress on existing lessons survives edits.
        var keepModules = r.Modules.Where(m => m.Id is not null).Select(m => m.Id!.Value).ToHashSet();
        foreach (var module in path.Modules.Where(m => !keepModules.Contains(m.Id)).ToList())
        {
            var lessonIds = module.Lessons.Select(l => l.Id).ToList();
            await _db.UserLearningProgress.Where(p => lessonIds.Contains(p.LessonId)).ExecuteDeleteAsync(ct);
            _db.LearningModules.Remove(module);
        }
        for (var mi = 0; mi < r.Modules.Count; mi++)
        {
            var dto = r.Modules[mi];
            var module = dto.Id is { } mid ? path.Modules.FirstOrDefault(m => m.Id == mid) : null;
            if (module is null) { module = new LearningModule(); path.Modules.Add(module); }
            module.Title = dto.Title.Trim(); module.Description = dto.Description; module.DisplayOrder = mi;
            var keepLessons = dto.Lessons.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();
            foreach (var lesson in module.Lessons.Where(l => !keepLessons.Contains(l.Id)).ToList())
            {
                await _db.UserLearningProgress.Where(p => p.LessonId == lesson.Id).ExecuteDeleteAsync(ct);
                _db.LearningLessons.Remove(lesson);
            }
            for (var li = 0; li < dto.Lessons.Count; li++)
            {
                var ldto = dto.Lessons[li];
                var lesson = ldto.Id is { } lid ? module.Lessons.FirstOrDefault(l => l.Id == lid) : null;
                if (lesson is null) { lesson = new LearningLesson(); module.Lessons.Add(lesson); }
                lesson.Title = ldto.Title.Trim(); lesson.Content = ldto.Content; lesson.Type = EnumParser.Parse<LessonType>(ldto.Type, "type");
                lesson.EstimatedMinutes = ldto.EstimatedMinutes; lesson.ExerciseInstructions = ldto.ExerciseInstructions; lesson.QuizId = ldto.QuizId; lesson.DisplayOrder = li;
                foreach (var lq in lesson.Questions.ToList()) _db.LessonQuestions.Remove(lq);
                lesson.Questions.Clear();
                var qids = (ldto.QuestionIds ?? Array.Empty<Guid>()).Distinct().ToList();
                for (var qi = 0; qi < qids.Count; qi++) lesson.Questions.Add(new LessonQuestion { QuestionId = qids[qi], DisplayOrder = qi });
            }
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(LearningPath), path.Id.ToString(), new { path.Title }, ct);
        return await GetPathAsync(path.Id, ct);
    }

    public async Task DeletePathAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.LearningPaths.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Learning path", id);
        p.IsDeleted = true; p.DeletedAt = _clock.UtcNow; p.IsPublished = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(LearningPath), id.ToString(), null, ct);
    }

    // ---------------- Flashcards ----------------
    public async Task<PagedResult<AdminFlashcardDto>> GetFlashcardsAsync(AdminFlashcardQuery query, CancellationToken ct)
    {
        var q = _db.Flashcards.AsNoTracking().Where(f => !f.IsDeleted);
        if (query.TechnologyId is { } t) q = q.Where(f => f.TechnologyId == t);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(f => f.Front.Contains(query.Search) || f.Back.Contains(query.Search));
        return await q.OrderBy(f => f.Front).Select(f => new AdminFlashcardDto(f.Id, f.TechnologyId, f.Technology == null ? null : f.Technology.Name, f.CategoryId,
            f.Front, f.Back, f.Difficulty.ToString(), f.IsPublished)).ToPagedAsync(query, ct);
    }

    public async Task<AdminFlashcardDto> UpsertFlashcardAsync(Guid? id, FlashcardUpsertRequest r, CancellationToken ct)
    {
        var f = id is null ? new Flashcard() : await _db.Flashcards.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Flashcard", id);
        if (id is null) _db.Flashcards.Add(f);
        f.TechnologyId = r.TechnologyId; f.CategoryId = r.CategoryId; f.Front = r.Front.Trim(); f.Back = r.Back.Trim();
        f.Difficulty = EnumParser.Parse<Difficulty>(r.Difficulty, "difficulty"); f.IsPublished = r.IsPublished;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Flashcard), f.Id.ToString(), null, ct);
        var tech = f.TechnologyId is null ? null : await _db.Technologies.Where(t => t.Id == f.TechnologyId).Select(t => t.Name).FirstOrDefaultAsync(ct);
        return new AdminFlashcardDto(f.Id, f.TechnologyId, tech, f.CategoryId, f.Front, f.Back, f.Difficulty.ToString(), f.IsPublished);
    }

    public async Task DeleteFlashcardAsync(Guid id, CancellationToken ct)
    {
        var f = await _db.Flashcards.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Flashcard", id);
        f.IsDeleted = true; f.DeletedAt = _clock.UtcNow; f.IsPublished = false;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Companies ----------------
    private IQueryable<Company> CompanyQuery => _db.Companies.Include(c => c.Rounds).Include(c => c.Topics)
        .Include(c => c.Collections).ThenInclude(col => col.Questions).Include(c => c.Collections).ThenInclude(col => col.Challenges).AsSplitQuery();

    private static AdminCompanyDto ToDto(Company c) => new(c.Id, c.Name, c.Slug, c.Overview, c.OverviewSource.ToString(), c.Industry, c.LogoUrl, c.WebsiteUrl,
        c.DifficultyLevel.ToString(), c.DisplayOrder, c.IsPremium, c.IsActive,
        c.Rounds.OrderBy(r => r.DisplayOrder).Select(r => new RoundUpsertDto(r.Name, r.Description, r.Source.ToString(), r.SourceReference)).ToList(),
        c.Topics.OrderBy(t => t.DisplayOrder).Select(t => new CompanyTopicUpsertDto(t.Name, t.Description, t.TechnologyId)).ToList(),
        c.Collections.Select(col => new CollectionUpsertDto(col.Id, col.Title, col.Description, col.Kind.ToString(), col.Source.ToString(), col.SourceReference,
            col.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.QuestionId).ToList(), col.Challenges.OrderBy(q => q.DisplayOrder).Select(q => q.ChallengeId).ToList())).ToList());

    public async Task<IReadOnlyList<AdminCompanyDto>> GetCompaniesAsync(CancellationToken ct) =>
        (await CompanyQuery.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.DisplayOrder).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<AdminCompanyDto> GetCompanyAsync(Guid id, CancellationToken ct) =>
        ToDto(await CompanyQuery.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct) ?? throw new NotFoundException("Company", id));

    public async Task<AdminCompanyDto> UpsertCompanyAsync(Guid? id, CompanyUpsertRequest r, CancellationToken ct)
    {
        var c = id is null ? new Company() : await CompanyQuery.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Company", id);
        var overviewSource = EnumParser.Parse<ContentSource>(r.OverviewSource, "overviewSource");
        void RequireReference(ContentSource source, string? reference, string field)
        {
            if (source == ContentSource.Verified && string.IsNullOrWhiteSpace(reference))
                throw ValidationAppException.For(field, "Verified content must cite a source reference (URL or publication).");
        }
        var slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug);
        if (await _db.Companies.AnyAsync(x => x.Slug == slug && x.Id != id, ct)) throw new ConflictException($"Slug '{slug}' is already used.");
        if (id is null) _db.Companies.Add(c);
        c.Name = r.Name.Trim(); c.Slug = slug; c.Overview = r.Overview; c.OverviewSource = overviewSource; c.Industry = r.Industry; c.LogoUrl = r.LogoUrl;
        c.WebsiteUrl = r.WebsiteUrl; c.DifficultyLevel = EnumParser.Parse<Difficulty>(r.DifficultyLevel, "difficultyLevel"); c.DisplayOrder = r.DisplayOrder;
        c.IsPremium = r.IsPremium; c.IsActive = r.IsActive;

        foreach (var round in c.Rounds.ToList()) _db.CompanyInterviewRounds.Remove(round);
        c.Rounds.Clear();
        var rounds = r.Rounds ?? Array.Empty<RoundUpsertDto>();
        for (var i = 0; i < rounds.Count; i++)
        {
            var source = EnumParser.Parse<ContentSource>(rounds[i].Source, "source");
            RequireReference(source, rounds[i].SourceReference, $"rounds[{i}].sourceReference");
            c.Rounds.Add(new CompanyInterviewRound { DisplayOrder = i, Name = rounds[i].Name, Description = rounds[i].Description, Source = source, SourceReference = rounds[i].SourceReference });
        }
        foreach (var topic in c.Topics.ToList()) _db.CompanyTopics.Remove(topic);
        c.Topics.Clear();
        var topics = r.Topics ?? Array.Empty<CompanyTopicUpsertDto>();
        for (var i = 0; i < topics.Count; i++) c.Topics.Add(new CompanyTopic { DisplayOrder = i, Name = topics[i].Name, Description = topics[i].Description, TechnologyId = topics[i].TechnologyId });

        var collections = r.Collections ?? Array.Empty<CollectionUpsertDto>();
        var keep = collections.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();
        foreach (var removed in c.Collections.Where(x => !keep.Contains(x.Id)).ToList()) _db.CompanyQuestionCollections.Remove(removed);
        for (var i = 0; i < collections.Count; i++)
        {
            var dto = collections[i];
            var source = EnumParser.Parse<ContentSource>(dto.Source, "source");
            RequireReference(source, dto.SourceReference, $"collections[{i}].sourceReference");
            var col = dto.Id is { } cid ? c.Collections.FirstOrDefault(x => x.Id == cid) : null;
            if (col is null) { col = new CompanyQuestionCollection(); c.Collections.Add(col); }
            col.Title = dto.Title; col.Description = dto.Description; col.Kind = EnumParser.Parse<CompanyCollectionKind>(dto.Kind, "kind"); col.Source = source; col.SourceReference = dto.SourceReference;
            foreach (var q in col.Questions.ToList()) _db.CompanyCollectionQuestions.Remove(q);
            col.Questions.Clear();
            var qids = (dto.QuestionIds ?? Array.Empty<Guid>()).Distinct().ToList();
            for (var qi = 0; qi < qids.Count; qi++) col.Questions.Add(new CompanyCollectionQuestion { QuestionId = qids[qi], DisplayOrder = qi });
            foreach (var ch in col.Challenges.ToList()) _db.CompanyCollectionChallenges.Remove(ch);
            col.Challenges.Clear();
            var chids = (dto.ChallengeIds ?? Array.Empty<Guid>()).Distinct().ToList();
            for (var ci = 0; ci < chids.Count; ci++) col.Challenges.Add(new CompanyCollectionChallenge { ChallengeId = chids[ci], DisplayOrder = ci });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Company), c.Id.ToString(), new { c.Name }, ct);
        return await GetCompanyAsync(c.Id, ct);
    }

    public async Task DeleteCompanyAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Company", id);
        c.IsDeleted = true; c.DeletedAt = _clock.UtcNow; c.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(Company), id.ToString(), null, ct);
    }
}

public sealed class QuizUpsertRequestValidator : AbstractValidator<QuizUpsertRequest>
{
    public QuizUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Mode).NotEmpty();
        RuleFor(x => x.PassingPercentage).InclusiveBetween(1, 100);
        RuleFor(x => x.NegativeMarkingFraction).InclusiveBetween(0m, 1m);
        RuleFor(x => x.TimeLimitSeconds).InclusiveBetween(60, 4 * 3600).When(x => x.TimeLimitSeconds is not null);
        RuleFor(x => x.QuestionIds).NotNull().Must(q => q.Count <= 200);
    }
}

public sealed class ChallengeUpsertRequestValidator : AbstractValidator<ChallengeUpsertRequest>
{
    public ChallengeUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProblemStatement).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Difficulty).NotEmpty();
        RuleFor(x => x.TimeLimitMs).InclusiveBetween(100, 20000);
        RuleFor(x => x.MemoryLimitMb).InclusiveBetween(32, 1024);
        RuleFor(x => x.TestCases).Must(t => t is null || t.Count <= 100);
        RuleForEach(x => x.TestCases).ChildRules(t =>
        {
            t.RuleFor(x => x.Input).MaximumLength(1_000_000);
            t.RuleFor(x => x.ExpectedOutput).MaximumLength(1_000_000);
        });
    }
}

public sealed class LearningPathUpsertRequestValidator : AbstractValidator<LearningPathUpsertRequest>
{
    public LearningPathUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Level).NotEmpty();
        RuleFor(x => x.EstimatedHours).InclusiveBetween(1, 1000);
        RuleFor(x => x.Modules).NotNull();
        RuleForEach(x => x.Modules).ChildRules(m =>
        {
            m.RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            m.RuleForEach(x => x.Lessons).ChildRules(l =>
            {
                l.RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
                l.RuleFor(x => x.Content).NotEmpty().MaximumLength(100_000);
                l.RuleFor(x => x.EstimatedMinutes).InclusiveBetween(1, 600);
            });
        });
    }
}

public sealed class FlashcardUpsertRequestValidator : AbstractValidator<FlashcardUpsertRequest>
{
    public FlashcardUpsertRequestValidator()
    {
        RuleFor(x => x.Front).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Back).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Difficulty).NotEmpty();
    }
}

public sealed class CompanyUpsertRequestValidator : AbstractValidator<CompanyUpsertRequest>
{
    public CompanyUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Overview).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.OverviewSource).NotEmpty().NotEqual("Verified").WithMessage("Company overviews cannot be marked Verified without a citation field; use Illustrative or CommunityContributed.");
        RuleFor(x => x.DifficultyLevel).NotEmpty();
        RuleFor(x => x.WebsiteUrl).MaximumLength(300);
    }
}
