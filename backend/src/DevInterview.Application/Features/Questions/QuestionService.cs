using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Questions;

public interface IQuestionService
{
    Task<IReadOnlyList<TechnologyDto>> GetTechnologiesAsync(CancellationToken ct);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(Guid technologyId, CancellationToken ct);
    Task<PagedResult<QuestionListItemDto>> GetQuestionsAsync(Guid userId, QuestionQuery query, CancellationToken ct);
    Task<QuestionDetailDto> GetQuestionAsync(Guid userId, Guid questionId, CancellationToken ct);
    Task<QuestionDetailDto> GetRandomAsync(Guid userId, Guid? technologyId, string? difficulty, CancellationToken ct);
    Task<QuestionAnswerDto> GetAnswerAsync(Guid userId, Guid questionId, CancellationToken ct);
    Task<AttemptQuestionResult> AttemptAsync(Guid userId, Guid questionId, AttemptQuestionRequest request, CancellationToken ct);
    Task SetBookmarkAsync(Guid userId, Guid questionId, bool bookmarked, CancellationToken ct);
    Task<PagedResult<QuestionListItemDto>> GetBookmarksAsync(Guid userId, QuestionQuery query, CancellationToken ct);
    Task<NoteDto> SaveNoteAsync(Guid userId, Guid questionId, SaveNoteRequest request, CancellationToken ct);
    Task DeleteNoteAsync(Guid userId, Guid questionId, CancellationToken ct);
    Task<PagedResult<NoteDto>> GetNotesAsync(Guid userId, PageQuery query, Guid? technologyId, CancellationToken ct);
    Task<string> ExportNotesAsync(Guid userId, string format, CancellationToken ct);
    Task ReportAsync(Guid userId, Guid questionId, ReportQuestionRequest request, CancellationToken ct);
}

public sealed class QuestionService : IQuestionService
{
    private readonly IAppDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly IClock _clock;

    public QuestionService(IAppDbContext db, IEntitlementService entitlements, IGamificationService gamification, IClock clock)
    {
        _db = db;
        _entitlements = entitlements;
        _gamification = gamification;
        _clock = clock;
    }

    private IQueryable<Question> Published => _db.Questions.Where(q => q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted);

    public async Task<IReadOnlyList<TechnologyDto>> GetTechnologiesAsync(CancellationToken ct) =>
        await _db.Technologies.AsNoTracking().Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new TechnologyDto(t.Id, t.Name, t.Slug, t.Description, t.Group, t.IconKey,
                _db.Questions.Count(q => q.TechnologyId == t.Id && q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted),
                t.Categories.Count(c => c.IsActive && !c.IsDeleted)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(Guid technologyId, CancellationToken ct)
    {
        if (!await _db.Technologies.AnyAsync(t => t.Id == technologyId && t.IsActive, ct)) throw new NotFoundException("Technology", technologyId);
        var categories = await _db.Categories.AsNoTracking().Where(c => c.TechnologyId == technologyId && c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.TechnologyId, c.Name, c.Slug, c.Description,
                Count = _db.Questions.Count(q => q.CategoryId == c.Id && q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted),
                Topics = c.Topics.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.DisplayOrder).Select(t => new TopicDto(t.Id, t.Name, t.Slug)).ToList()
            }).ToListAsync(ct);
        return categories.Select(c => new CategoryDto(c.Id, c.TechnologyId, c.Name, c.Slug, c.Description, c.Count, c.Topics)).ToList();
    }

    public async Task<PagedResult<QuestionListItemDto>> GetQuestionsAsync(Guid userId, QuestionQuery query, CancellationToken ct)
    {
        var q = Published.AsNoTracking();
        if (query.TechnologyId is { } tech) q = q.Where(x => x.TechnologyId == tech);
        if (query.CategoryId is { } cat) q = q.Where(x => x.CategoryId == cat);
        if (query.TopicId is { } topic) q = q.Where(x => x.TopicId == topic);
        if (EnumParser.ParseOptional<Difficulty>(query.Difficulty, "difficulty") is { } diff) q = q.Where(x => x.Difficulty == diff);
        if (EnumParser.ParseOptional<QuestionType>(query.Type, "type") is { } type) q = q.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = Slug.From(query.Tag);
            q = q.Where(x => x.Tags.Any(t => t.Tag.Slug == tag));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.Body.Contains(term) || x.Tags.Any(t => t.Tag.Name.Contains(term)));
        }
        if (query.BookmarkedOnly)
            q = q.Where(x => _db.QuestionBookmarks.Any(b => b.UserId == userId && b.QuestionId == x.Id && !b.IsRemoved));
        q = query.AttemptFilter?.ToLowerInvariant() switch
        {
            "unattempted" => q.Where(x => !_db.QuestionAttempts.Any(a => a.UserId == userId && a.QuestionId == x.Id)),
            "incorrect" => q.Where(x => _db.QuestionAttempts.Where(a => a.UserId == userId && a.QuestionId == x.Id)
                .OrderByDescending(a => a.CreatedAt).Select(a => a.IsCorrect).FirstOrDefault() == false),
            "correct" => q.Where(x => _db.QuestionAttempts.Where(a => a.UserId == userId && a.QuestionId == x.Id)
                .OrderByDescending(a => a.CreatedAt).Select(a => a.IsCorrect).FirstOrDefault() == true),
            _ => q
        };
        q = query.Sort?.ToLowerInvariant() switch
        {
            "newest" => q.OrderByDescending(x => x.PublishedAt).ThenBy(x => x.Id),
            "title" => q.OrderBy(x => x.Title).ThenBy(x => x.Id),
            "difficulty_desc" => q.OrderByDescending(x => x.Difficulty).ThenBy(x => x.Title),
            _ => q.OrderBy(x => x.Difficulty).ThenBy(x => x.Title).ThenBy(x => x.Id)
        };

        var page = await ProjectList(q, userId).ToPagedAsync(query, ct);
        return page;
    }

    internal IQueryable<QuestionListItemDto> ProjectList(IQueryable<Question> q, Guid userId) =>
        q.Select(x => new QuestionListItemDto(
            x.Id, x.Title, new RefDto(x.TechnologyId, x.Technology.Name, x.Technology.Slug),
            new RefDto(x.CategoryId, x.Category.Name, x.Category.Slug), x.Difficulty.ToString(), x.Type.ToString(),
            x.EstimatedSeconds, x.Tags.Select(t => t.Tag.Name).ToList(), x.IsPremium,
            _db.QuestionBookmarks.Any(b => b.UserId == userId && b.QuestionId == x.Id && !b.IsRemoved),
            _db.QuestionAttempts.Where(a => a.UserId == userId && a.QuestionId == x.Id)
                .OrderByDescending(a => a.CreatedAt).Select(a => a.IsCorrect).FirstOrDefault()));

    private async Task<Question> LoadAccessibleAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var question = await Published.AsNoTracking()
            .Include(x => x.Technology).Include(x => x.Category).Include(x => x.Topic)
            .Include(x => x.Options).Include(x => x.Tags).ThenInclude(t => t.Tag)
            .Include(x => x.Answer)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == questionId, ct) ?? throw new NotFoundException("Question", questionId);
        if (question.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This question", ct);
        return question;
    }

    public async Task<QuestionDetailDto> GetQuestionAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var question = await LoadAccessibleAsync(userId, questionId, ct);
        return await ToDetailAsync(userId, question, ct);
    }

    public async Task<QuestionDetailDto> GetRandomAsync(Guid userId, Guid? technologyId, string? difficulty, CancellationToken ct)
    {
        var premium = await _entitlements.IsPremiumAsync(userId, ct);
        var q = Published.Where(x => premium || !x.IsPremium);
        if (technologyId is { } t) q = q.Where(x => x.TechnologyId == t);
        if (EnumParser.ParseOptional<Difficulty>(difficulty, "difficulty") is { } d) q = q.Where(x => x.Difficulty == d);
        // Prefer questions the user has not attempted yet.
        var unattempted = q.Where(x => !_db.QuestionAttempts.Any(a => a.UserId == userId && a.QuestionId == x.Id));
        // Random selection is done over candidate ids in memory to stay provider-agnostic (NEWID()/random() differ).
        var candidates = await unattempted.Select(x => x.Id).Take(5000).ToListAsync(ct);
        if (candidates.Count == 0) candidates = await q.Select(x => x.Id).Take(5000).ToListAsync(ct);
        if (candidates.Count == 0) throw new NotFoundException("Question");
        var id = candidates[Random.Shared.Next(candidates.Count)];
        return await GetQuestionAsync(userId, id, ct);
    }

    private async Task<QuestionDetailDto> ToDetailAsync(Guid userId, Question q, CancellationToken ct)
    {
        var bookmarked = await _db.QuestionBookmarks.AnyAsync(b => b.UserId == userId && b.QuestionId == q.Id && !b.IsRemoved, ct);
        var note = await _db.QuestionNotes.Where(n => n.UserId == userId && n.QuestionId == q.Id && !n.IsRemoved)
            .Select(n => n.Content).FirstOrDefaultAsync(ct);

        // Previous/next within the same category using the default listing order (difficulty, title, id).
        var ordered = await Published.Where(x => x.CategoryId == q.CategoryId)
            .OrderBy(x => x.Difficulty).ThenBy(x => x.Title).ThenBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(ct);
        var index = ordered.IndexOf(q.Id);
        Guid? prev = index > 0 ? ordered[index - 1] : null;
        Guid? next = index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null;

        var options = q.Options.OrderBy(o => o.DisplayOrder).Select(o => new QuestionOptionDto(o.Id, o.Text, o.DisplayOrder)).ToList();
        var correctCount = q.Options.Count(o => o.IsCorrect);
        var share = $"{q.Title}\n\n{Text.Truncate(q.Body, 400)}\n\n#{q.Technology.Name.Replace(" ", "")} #DevInterview";
        return new QuestionDetailDto(q.Id, q.Title, q.Body, q.CodeSnippet, q.CodeLanguage,
            new RefDto(q.TechnologyId, q.Technology.Name, q.Technology.Slug), new RefDto(q.CategoryId, q.Category.Name, q.Category.Slug),
            q.Topic is null ? null : new RefDto(q.Topic.Id, q.Topic.Name, q.Topic.Slug),
            q.Difficulty.ToString(), q.Type.ToString(), q.EstimatedSeconds, q.Tags.Select(t => t.Tag.Name).ToList(), options,
            q.IsObjective, q.Type == QuestionType.MultipleSelect || correctCount > 1, bookmarked, note, q.IsPremium, prev, next, share);
    }

    private static QuestionAnswerDto ToAnswer(Question q) => new(
        q.Id, q.Answer?.ShortAnswer ?? string.Empty, q.Answer?.DetailedExplanation ?? string.Empty,
        q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList(),
        q.Options.OrderBy(o => o.DisplayOrder).Select(o => new OptionExplanationDto(o.Id, o.IsCorrect, o.Explanation)).ToList());

    public async Task<QuestionAnswerDto> GetAnswerAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var question = await LoadAccessibleAsync(userId, questionId, ct);
        await ConsumeDailyPracticeAsync(userId, questionId, ct);
        return ToAnswer(question);
    }

    /// <summary>Counts a question against the free daily limit once per question per day.</summary>
    private async Task ConsumeDailyPracticeAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var dayStart = _clock.UtcNow.Date;
        var alreadyToday = await _db.QuestionAttempts.AnyAsync(a => a.UserId == userId && a.QuestionId == questionId && a.CreatedAt >= dayStart, ct)
                           || await _db.UserActivityLogs.AnyAsync(a => a.UserId == userId && a.EntityId == questionId &&
                                a.ActivityType == ActivityType.QuestionAttempted && a.OccurredAt >= dayStart, ct);
        if (!alreadyToday) await _entitlements.ConsumeAsync(userId, UsageFeature.QuestionView, ct);
    }

    public async Task<AttemptQuestionResult> AttemptAsync(Guid userId, Guid questionId, AttemptQuestionRequest request, CancellationToken ct)
    {
        var question = await LoadAccessibleAsync(userId, questionId, ct);
        await ConsumeDailyPracticeAsync(userId, questionId, ct);

        var selected = (request.SelectedOptionIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var validIds = question.Options.Select(o => o.Id).ToHashSet();
        if (selected.Any(id => !validIds.Contains(id))) throw ValidationAppException.For("selectedOptionIds", "Unknown option selected.");

        var correctIds = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();
        bool? isCorrect;
        if (question.IsObjective && question.Options.Count > 0)
        {
            if (selected.Count == 0) throw ValidationAppException.For("selectedOptionIds", "Select at least one option.");
            isCorrect = QuizScoring.Grade(selected, correctIds, 1m, 0m).IsCorrect;
        }
        else
        {
            isCorrect = request.SelfAssessedCorrect; // written/scenario questions are self-assessed (or AI-evaluated separately)
        }

        _db.QuestionAttempts.Add(new QuestionAttempt
        {
            UserId = userId, QuestionId = questionId, TechnologyId = question.TechnologyId, CategoryId = question.CategoryId,
            Difficulty = question.Difficulty, IsCorrect = isCorrect, SelectedOptionIds = QuizScoring.ToCsv(selected),
            TimeSpentSeconds = Math.Clamp(request.TimeSpentSeconds, 0, 3600)
        });
        await _db.SaveChangesAsync(ct);

        if (isCorrect is not null)
            await _gamification.RecordAnswerStatsAsync(userId, new[] { (question.TechnologyId, isCorrect.Value) }, ct);
        var xp = isCorrect == true
            ? await _gamification.AwardXpAsync(userId, XpRules.QuestionCorrect, XpSourceType.QuestionCorrect, $"q:{questionId}", $"Answered \"{Text.Truncate(question.Title, 60)}\"", ct)
            : 0;
        xp += await _gamification.RecordActivityAsync(userId, ActivityType.QuestionAttempted, questionId, question.TechnologyId,
            Math.Clamp(request.TimeSpentSeconds, 0, 3600), ct);

        return new AttemptQuestionResult(isCorrect, correctIds, xp, ToAnswer(question));
    }

    public async Task SetBookmarkAsync(Guid userId, Guid questionId, bool bookmarked, CancellationToken ct)
    {
        if (!await Published.AnyAsync(q => q.Id == questionId, ct)) throw new NotFoundException("Question", questionId);
        var bookmark = await _db.QuestionBookmarks.FirstOrDefaultAsync(b => b.UserId == userId && b.QuestionId == questionId, ct);
        var now = _clock.UtcNow;
        if (bookmark is null)
        {
            if (!bookmarked) return;
            _db.QuestionBookmarks.Add(new QuestionBookmark { UserId = userId, QuestionId = questionId, ClientUpdatedAt = now });
        }
        else
        {
            bookmark.IsRemoved = !bookmarked;
            bookmark.ClientUpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<QuestionListItemDto>> GetBookmarksAsync(Guid userId, QuestionQuery query, CancellationToken ct)
    {
        query.BookmarkedOnly = true;
        return await GetQuestionsAsync(userId, query, ct);
    }

    public async Task<NoteDto> SaveNoteAsync(Guid userId, Guid questionId, SaveNoteRequest request, CancellationToken ct)
    {
        var question = await Published.Include(q => q.Technology).FirstOrDefaultAsync(q => q.Id == questionId, ct)
                       ?? throw new NotFoundException("Question", questionId);
        var note = await _db.QuestionNotes.FirstOrDefaultAsync(n => n.UserId == userId && n.QuestionId == questionId, ct);
        var now = _clock.UtcNow;
        if (note is null)
        {
            note = new QuestionNote { UserId = userId, QuestionId = questionId };
            _db.QuestionNotes.Add(note);
        }
        note.Content = request.Content.Trim();
        note.IsRemoved = false;
        note.ClientUpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new NoteDto(questionId, question.Title, question.Technology.Name, note.Content, note.UpdatedAt);
    }

    public async Task DeleteNoteAsync(Guid userId, Guid questionId, CancellationToken ct)
    {
        var note = await _db.QuestionNotes.FirstOrDefaultAsync(n => n.UserId == userId && n.QuestionId == questionId, ct);
        if (note is null) return;
        note.IsRemoved = true; // tombstone so offline clients learn about the deletion
        note.Content = string.Empty;
        note.ClientUpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NoteDto>> GetNotesAsync(Guid userId, PageQuery query, Guid? technologyId, CancellationToken ct)
    {
        var q = _db.QuestionNotes.AsNoTracking().Where(n => n.UserId == userId && !n.IsRemoved);
        if (technologyId is { } t) q = q.Where(n => n.Question.TechnologyId == t);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(n => n.Content.Contains(query.Search) || n.Question.Title.Contains(query.Search));
        return await q.OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteDto(n.QuestionId, n.Question.Title, n.Question.Technology.Name, n.Content, n.UpdatedAt))
            .ToPagedAsync(query, ct);
    }

    public async Task<string> ExportNotesAsync(Guid userId, string format, CancellationToken ct)
    {
        var notes = await _db.QuestionNotes.AsNoTracking().Where(n => n.UserId == userId && !n.IsRemoved)
            .OrderBy(n => n.Question.Technology.Name).ThenBy(n => n.Question.Title)
            .Select(n => new { n.Question.Title, Technology = n.Question.Technology.Name, Category = n.Question.Category.Name, n.Content, n.UpdatedAt })
            .ToListAsync(ct);
        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var lines = new List<string> { "Technology,Category,Question,Note,UpdatedAt" };
            lines.AddRange(notes.Select(n => string.Join(',', Text.Csv(n.Technology), Text.Csv(n.Category), Text.Csv(n.Title), Text.Csv(n.Content), n.UpdatedAt.ToString("O"))));
            return string.Join('\n', lines);
        }
        var md = new System.Text.StringBuilder("# DevInterview — My Notes\n\n");
        foreach (var group in notes.GroupBy(n => n.Technology))
        {
            md.Append("## ").Append(group.Key).Append("\n\n");
            foreach (var n in group)
                md.Append("### ").Append(n.Title).Append("\n_").Append(n.Category).Append(" · ")
                  .Append(n.UpdatedAt.ToString("yyyy-MM-dd")).Append("_\n\n").Append(n.Content).Append("\n\n");
        }
        return md.ToString();
    }

    public async Task ReportAsync(Guid userId, Guid questionId, ReportQuestionRequest request, CancellationToken ct)
    {
        if (!await _db.Questions.AnyAsync(q => q.Id == questionId, ct)) throw new NotFoundException("Question", questionId);
        var openDuplicate = await _db.QuestionReports.AnyAsync(r => r.UserId == userId && r.QuestionId == questionId && r.Status == QuestionReportStatus.Open, ct);
        if (openDuplicate) throw new ConflictException("You have already reported this question. Our content team will review it.");
        _db.QuestionReports.Add(new QuestionReport { UserId = userId, QuestionId = questionId, Reason = request.Reason.Trim(), Details = request.Details?.Trim() });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class SaveNoteRequestValidator : AbstractValidator<SaveNoteRequest>
{
    public SaveNoteRequestValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(10000);
}

public sealed class ReportQuestionRequestValidator : AbstractValidator<ReportQuestionRequest>
{
    public static readonly string[] Reasons = { "Incorrect answer", "Unclear question", "Typo or formatting", "Outdated information", "Duplicate", "Other" };

    public ReportQuestionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}

public sealed class AttemptQuestionRequestValidator : AbstractValidator<AttemptQuestionRequest>
{
    public AttemptQuestionRequestValidator()
    {
        RuleFor(x => x.TimeSpentSeconds).InclusiveBetween(0, 24 * 3600);
        RuleFor(x => x.SelectedOptionIds).Must(l => l is null || l.Count <= 10);
    }
}
