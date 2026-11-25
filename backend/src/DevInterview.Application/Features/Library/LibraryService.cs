using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using DevInterview.Contracts.Engagement;
using DevInterview.Contracts.Learning;
using DevInterview.Application.Features.Flashcards;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Library;

/// <summary>Custom collections of saved questions, challenges and AI-evaluated interview answers.</summary>
public interface ICollectionService
{
    Task<IReadOnlyList<CollectionDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<CollectionDetailDto> GetAsync(Guid userId, Guid collectionId, CancellationToken ct);
    Task<CollectionDto> CreateAsync(Guid userId, CreateCollectionRequest request, CancellationToken ct);
    Task<CollectionDto> RenameAsync(Guid userId, Guid collectionId, CreateCollectionRequest request, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid collectionId, CancellationToken ct);
    Task<CollectionItemDto> AddItemAsync(Guid userId, Guid collectionId, AddCollectionItemRequest request, CancellationToken ct);
    Task RemoveItemAsync(Guid userId, Guid collectionId, Guid itemId, CancellationToken ct);
}

public sealed class CollectionService : ICollectionService
{
    public const int MaxCollections = 50;
    private readonly IAppDbContext _db;
    public CollectionService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CollectionDto>> ListAsync(Guid userId, CancellationToken ct) =>
        await _db.SavedCollections.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.Name)
            .Select(c => new CollectionDto(c.Id, c.Name, c.Description, c.Items.Count, c.CreatedAt)).ToListAsync(ct);

    private async Task<SavedCollection> LoadAsync(Guid userId, Guid id, CancellationToken ct) =>
        await _db.SavedCollections.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct)
        ?? throw new NotFoundException("Collection", id);

    public async Task<CollectionDetailDto> GetAsync(Guid userId, Guid collectionId, CancellationToken ct)
    {
        var c = await LoadAsync(userId, collectionId, ct);
        var items = new List<CollectionItemDto>();
        var qIds = c.Items.Where(i => i.ItemType == SavedItemType.Question).Select(i => i.ItemId).ToList();
        var chIds = c.Items.Where(i => i.ItemType == SavedItemType.CodingChallenge).Select(i => i.ItemId).ToList();
        var evIds = c.Items.Where(i => i.ItemType == SavedItemType.AnswerEvaluation).Select(i => i.ItemId).ToList();
        var questions = (await _db.Questions.Where(q => qIds.Contains(q.Id)).Select(q => new { q.Id, q.Title, Tech = q.Technology.Name, q.Difficulty }).ToListAsync(ct))
            .ToDictionary(q => q.Id, q => new { q.Title, Sub = $"{q.Tech} · {q.Difficulty}" });
        var challenges = (await _db.CodingChallenges.Where(q => chIds.Contains(q.Id)).Select(q => new { q.Id, q.Title, q.Category, q.Difficulty }).ToListAsync(ct))
            .ToDictionary(q => q.Id, q => new { q.Title, Sub = $"{q.Category} · {q.Difficulty}" });
        var evals = await _db.AnswerEvaluations.Where(e => evIds.Contains(e.Id) && e.UserId == userId).Select(e => new { e.Id, e.QuestionText, e.OverallScore }).ToDictionaryAsync(e => e.Id, ct);
        foreach (var i in c.Items.OrderByDescending(i => i.CreatedAt))
        {
            (string Title, string? Sub)? info = i.ItemType switch
            {
                SavedItemType.Question when questions.TryGetValue(i.ItemId, out var q) => (q.Title, q.Sub),
                SavedItemType.CodingChallenge when challenges.TryGetValue(i.ItemId, out var ch) => (ch.Title, ch.Sub),
                SavedItemType.AnswerEvaluation when evals.TryGetValue(i.ItemId, out var ev) => (Text.Truncate(ev.QuestionText, 120), $"AI score {ev.OverallScore}"),
                _ => null
            };
            if (info is { } x) items.Add(new CollectionItemDto(i.Id, i.ItemType.ToString(), i.ItemId, x.Title, x.Sub, i.CreatedAt));
        }
        return new CollectionDetailDto(c.Id, c.Name, c.Description, items);
    }

    public async Task<CollectionDto> CreateAsync(Guid userId, CreateCollectionRequest request, CancellationToken ct)
    {
        if (await _db.SavedCollections.CountAsync(c => c.UserId == userId, ct) >= MaxCollections)
            throw new BusinessRuleException(ErrorCodes.QuotaExceeded, $"You can create up to {MaxCollections} collections.");
        var name = request.Name.Trim();
        if (await _db.SavedCollections.AnyAsync(c => c.UserId == userId && c.Name == name, ct)) throw new ConflictException("A collection with this name already exists.");
        var c = new SavedCollection { UserId = userId, Name = name, Description = request.Description?.Trim() };
        _db.SavedCollections.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CollectionDto(c.Id, c.Name, c.Description, 0, c.CreatedAt);
    }

    public async Task<CollectionDto> RenameAsync(Guid userId, Guid collectionId, CreateCollectionRequest request, CancellationToken ct)
    {
        var c = await LoadAsync(userId, collectionId, ct);
        c.Name = request.Name.Trim();
        c.Description = request.Description?.Trim();
        await _db.SaveChangesAsync(ct);
        return new CollectionDto(c.Id, c.Name, c.Description, c.Items.Count, c.CreatedAt);
    }

    public async Task DeleteAsync(Guid userId, Guid collectionId, CancellationToken ct)
    {
        var c = await LoadAsync(userId, collectionId, ct);
        _db.SavedCollections.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CollectionItemDto> AddItemAsync(Guid userId, Guid collectionId, AddCollectionItemRequest request, CancellationToken ct)
    {
        var c = await LoadAsync(userId, collectionId, ct);
        var type = EnumParser.Parse<SavedItemType>(request.ItemType, nameof(request.ItemType));
        var title = type switch
        {
            SavedItemType.Question => await _db.Questions.Where(q => q.Id == request.ItemId && q.Status == ContentStatus.Published).Select(q => q.Title).FirstOrDefaultAsync(ct),
            SavedItemType.CodingChallenge => await _db.CodingChallenges.Where(q => q.Id == request.ItemId && q.Status == ContentStatus.Published).Select(q => q.Title).FirstOrDefaultAsync(ct),
            SavedItemType.AnswerEvaluation => await _db.AnswerEvaluations.Where(q => q.Id == request.ItemId && q.UserId == userId).Select(q => q.QuestionText).FirstOrDefaultAsync(ct),
            _ => null
        } ?? throw new NotFoundException(type.ToString(), request.ItemId);
        var existing = c.Items.FirstOrDefault(i => i.ItemType == type && i.ItemId == request.ItemId);
        if (existing is not null) return new CollectionItemDto(existing.Id, type.ToString(), existing.ItemId, Text.Truncate(title, 120), null, existing.CreatedAt);
        var item = new SavedCollectionItem { CollectionId = c.Id, ItemType = type, ItemId = request.ItemId };
        _db.SavedCollectionItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return new CollectionItemDto(item.Id, type.ToString(), item.ItemId, Text.Truncate(title, 120), null, item.CreatedAt);
    }

    public async Task RemoveItemAsync(Guid userId, Guid collectionId, Guid itemId, CancellationToken ct)
    {
        var c = await LoadAsync(userId, collectionId, ct);
        var item = c.Items.FirstOrDefault(i => i.Id == itemId) ?? throw new NotFoundException("Collection item", itemId);
        _db.SavedCollectionItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class CreateCollectionRequestValidator : AbstractValidator<CreateCollectionRequest>
{
    public CreateCollectionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(400);
    }
}

public sealed class AddCollectionItemRequestValidator : AbstractValidator<AddCollectionItemRequest>
{
    public AddCollectionItemRequestValidator()
    {
        RuleFor(x => x.ItemType).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}

public interface ISearchService
{
    Task<SearchResultsDto> SearchAsync(Guid userId, SearchQuery query, CancellationToken ct);
}

/// <summary>
/// Global search over published content and the user's own notes. Uses indexed LIKE queries; the interface allows
/// swapping in SQL Server full-text search or an external engine later.
/// </summary>
public sealed class SearchService : ISearchService
{
    private static readonly string[] Types = { "question", "technology", "category", "challenge", "learningPath", "flashcard", "note" };
    private readonly IAppDbContext _db;
    public SearchService(IAppDbContext db) => _db = db;

    public async Task<SearchResultsDto> SearchAsync(Guid userId, SearchQuery query, CancellationToken ct)
    {
        var term = (query.Q ?? string.Empty).Trim();
        if (term.Length < 2) throw ValidationAppException.For("q", "Enter at least 2 characters.");
        if (term.Length > 100) term = term[..100];
        var type = query.Type is null ? null : Types.FirstOrDefault(t => t.Equals(query.Type, StringComparison.OrdinalIgnoreCase))
                   ?? throw ValidationAppException.For("type", $"Type must be one of: {string.Join(", ", Types)}.");
        var difficulty = EnumParser.ParseOptional<Difficulty>(query.Difficulty, "difficulty");

        var questions = _db.Questions.AsNoTracking().Where(q => q.Status == ContentStatus.Published && q.IsActive && !q.IsDeleted &&
            (q.Title.Contains(term) || q.Body.Contains(term) || q.Tags.Any(t => t.Tag.Name.Contains(term))));
        if (query.TechnologyId is { } tid) questions = questions.Where(q => q.TechnologyId == tid);
        if (difficulty is { } d) questions = questions.Where(q => q.Difficulty == d);
        var technologies = _db.Technologies.AsNoTracking().Where(t => t.IsActive && !t.IsDeleted && (t.Name.Contains(term) || (t.Description != null && t.Description.Contains(term))));
        var categories = _db.Categories.AsNoTracking().Where(c => c.IsActive && !c.IsDeleted && c.Name.Contains(term));
        if (query.TechnologyId is { } tid2) categories = categories.Where(c => c.TechnologyId == tid2);
        var challenges = _db.CodingChallenges.AsNoTracking().Where(c => c.Status == ContentStatus.Published && c.IsActive && !c.IsDeleted &&
            (c.Title.Contains(term) || c.ProblemStatement.Contains(term)));
        var paths = _db.LearningPaths.AsNoTracking().Where(p => p.IsPublished && p.IsActive && !p.IsDeleted && (p.Title.Contains(term) || p.Description.Contains(term)));
        var flashcards = _db.Flashcards.AsNoTracking().Where(f => f.IsPublished && f.IsActive && !f.IsDeleted && (f.Front.Contains(term) || f.Back.Contains(term)));
        if (query.TechnologyId is { } tid3) flashcards = flashcards.Where(f => f.TechnologyId == tid3);
        var notes = _db.QuestionNotes.AsNoTracking().Where(n => n.UserId == userId && !n.IsRemoved && (n.Content.Contains(term) || n.Question.Title.Contains(term)));

        var counts = new Dictionary<string, int>
        {
            ["question"] = await questions.CountAsync(ct), ["technology"] = await technologies.CountAsync(ct),
            ["category"] = await categories.CountAsync(ct), ["challenge"] = await challenges.CountAsync(ct),
            ["learningPath"] = await paths.CountAsync(ct), ["flashcard"] = await flashcards.CountAsync(ct), ["note"] = await notes.CountAsync(ct)
        };

        // Title matches rank above body matches.
        IQueryable<SearchHitDto> hits = type switch
        {
            "question" => questions.OrderByDescending(q => q.Title.Contains(term)).ThenBy(q => q.Title)
                .Select(q => new SearchHitDto("question", q.Id, q.Title, q.Category.Name, q.Technology.Name, q.Difficulty.ToString(), q.CategoryId)),
            "technology" => technologies.OrderBy(t => t.Name).Select(t => new SearchHitDto("technology", t.Id, t.Name, t.Group, t.Name, null, null)),
            "category" => categories.OrderBy(c => c.Name).Select(c => new SearchHitDto("category", c.Id, c.Name, c.Technology.Name, c.Technology.Name, null, c.TechnologyId)),
            "challenge" => challenges.OrderByDescending(c => c.Title.Contains(term)).ThenBy(c => c.Title)
                .Select(c => new SearchHitDto("challenge", c.Id, c.Title, c.Category.ToString(), null, c.Difficulty.ToString(), null)),
            "learningPath" => paths.OrderBy(p => p.Title).Select(p => new SearchHitDto("learningPath", p.Id, p.Title, p.Level.ToString(), p.Technology == null ? null : p.Technology.Name, null, null)),
            "flashcard" => flashcards.OrderBy(f => f.Front).Select(f => new SearchHitDto("flashcard", f.Id, f.Front, null, f.Technology == null ? null : f.Technology.Name, f.Difficulty.ToString(), f.TechnologyId)),
            "note" => notes.OrderByDescending(n => n.UpdatedAt).Select(n => new SearchHitDto("note", n.QuestionId, n.Question.Title, n.Content.Substring(0, n.Content.Length > 120 ? 120 : n.Content.Length), n.Question.Technology.Name, null, n.QuestionId)),
            _ => null!
        };

        PagedResult<SearchHitDto> page;
        if (type is not null)
        {
            page = await hits.ToPagedAsync(query, ct);
        }
        else
        {
            // "All" view: a few top hits from every type, in a fixed type order.
            var all = new List<SearchHitDto>();
            all.AddRange(await technologies.OrderBy(t => t.Name).Take(3).Select(t => new SearchHitDto("technology", t.Id, t.Name, t.Group, t.Name, null, null)).ToListAsync(ct));
            all.AddRange(await categories.OrderBy(c => c.Name).Take(3).Select(c => new SearchHitDto("category", c.Id, c.Name, c.Technology.Name, c.Technology.Name, null, c.TechnologyId)).ToListAsync(ct));
            all.AddRange(await questions.OrderByDescending(q => q.Title.Contains(term)).ThenBy(q => q.Title).Take(8)
                .Select(q => new SearchHitDto("question", q.Id, q.Title, q.Category.Name, q.Technology.Name, q.Difficulty.ToString(), q.CategoryId)).ToListAsync(ct));
            all.AddRange(await challenges.OrderBy(c => c.Title).Take(4).Select(c => new SearchHitDto("challenge", c.Id, c.Title, c.Category.ToString(), null, c.Difficulty.ToString(), null)).ToListAsync(ct));
            all.AddRange(await paths.OrderBy(p => p.Title).Take(3).Select(p => new SearchHitDto("learningPath", p.Id, p.Title, p.Level.ToString(), p.Technology == null ? null : p.Technology.Name, null, null)).ToListAsync(ct));
            all.AddRange(await flashcards.OrderBy(f => f.Front).Take(3).Select(f => new SearchHitDto("flashcard", f.Id, f.Front, null, f.Technology == null ? null : f.Technology.Name, f.Difficulty.ToString(), f.TechnologyId)).ToListAsync(ct));
            all.AddRange(await notes.OrderByDescending(n => n.UpdatedAt).Take(3).Select(n => new SearchHitDto("note", n.QuestionId, n.Question.Title, null, n.Question.Technology.Name, null, n.QuestionId)).ToListAsync(ct));
            page = new PagedResult<SearchHitDto>(all, 1, all.Count, all.Count);
        }
        return new SearchResultsDto(term, page, counts);
    }
}

public interface ISyncService
{
    Task<SyncPushResponse> PushAsync(Guid userId, SyncPushRequest request, CancellationToken ct);
    Task<SyncPullResponse> PullAsync(Guid userId, DateTime? since, CancellationToken ct);
}

/// <summary>
/// Offline synchronisation for bookmarks, notes and flashcard reviews.
/// Conflict rule for bookmarks/notes: last writer wins by client timestamp (server changes are stamped with server time);
/// a stale client change is ignored and reported as a conflict so the client adopts the server state on the next pull.
/// Flashcard reviews are append-only events, deduplicated by ClientReviewId and applied chronologically.
/// </summary>
public sealed class SyncService : ISyncService
{
    private readonly IAppDbContext _db;
    private readonly IFlashcardService _flashcards;
    private readonly IClock _clock;

    public SyncService(IAppDbContext db, IFlashcardService flashcards, IClock clock)
    {
        _db = db;
        _flashcards = flashcards;
        _clock = clock;
    }

    private DateTime ClampClientTime(DateTime t)
    {
        var utc = t.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : t.ToUniversalTime();
        return utc > _clock.UtcNow ? _clock.UtcNow : utc; // never trust a clock in the future
    }

    public async Task<SyncPushResponse> PushAsync(Guid userId, SyncPushRequest request, CancellationToken ct)
    {
        var conflicts = new List<SyncConflictDto>();
        int bookmarks = 0, notes = 0, reviews = 0;

        var bookmarkChanges = (request.Bookmarks ?? Array.Empty<BookmarkChangeDto>()).GroupBy(b => b.QuestionId).Select(g => g.MaxBy(b => b.ClientUpdatedAt)!).ToList();
        var noteChanges = (request.Notes ?? Array.Empty<NoteChangeDto>()).GroupBy(n => n.QuestionId).Select(g => g.MaxBy(n => n.ClientUpdatedAt)!).ToList();
        var ids = bookmarkChanges.Select(b => b.QuestionId).Concat(noteChanges.Select(n => n.QuestionId)).Distinct().ToList();
        var validQuestions = await _db.Questions.Where(q => ids.Contains(q.Id) && q.Status == ContentStatus.Published).Select(q => q.Id).ToListAsync(ct);

        var existingBookmarks = await _db.QuestionBookmarks.Where(b => b.UserId == userId && ids.Contains(b.QuestionId)).ToDictionaryAsync(b => b.QuestionId, ct);
        foreach (var change in bookmarkChanges)
        {
            if (!validQuestions.Contains(change.QuestionId)) { conflicts.Add(new("bookmark", change.QuestionId, "question-unavailable")); continue; }
            var at = ClampClientTime(change.ClientUpdatedAt);
            if (existingBookmarks.TryGetValue(change.QuestionId, out var b))
            {
                if (at < b.ClientUpdatedAt) { conflicts.Add(new("bookmark", change.QuestionId, "server-newer")); continue; }
                b.IsRemoved = !change.IsBookmarked;
                b.ClientUpdatedAt = at;
            }
            else if (change.IsBookmarked)
            {
                _db.QuestionBookmarks.Add(new QuestionBookmark { UserId = userId, QuestionId = change.QuestionId, ClientUpdatedAt = at });
            }
            bookmarks++;
        }

        var existingNotes = await _db.QuestionNotes.Where(n => n.UserId == userId && ids.Contains(n.QuestionId)).ToDictionaryAsync(n => n.QuestionId, ct);
        foreach (var change in noteChanges)
        {
            if (!validQuestions.Contains(change.QuestionId)) { conflicts.Add(new("note", change.QuestionId, "question-unavailable")); continue; }
            var at = ClampClientTime(change.ClientUpdatedAt);
            var content = Text.Truncate(change.Content?.Trim(), 10000);
            if (existingNotes.TryGetValue(change.QuestionId, out var n))
            {
                if (at < n.ClientUpdatedAt) { conflicts.Add(new("note", change.QuestionId, "server-newer")); continue; }
                n.Content = change.IsDeleted ? string.Empty : content;
                n.IsRemoved = change.IsDeleted || content.Length == 0;
                n.ClientUpdatedAt = at;
            }
            else if (!change.IsDeleted && content.Length > 0)
            {
                _db.QuestionNotes.Add(new QuestionNote { UserId = userId, QuestionId = change.QuestionId, Content = content, ClientUpdatedAt = at });
            }
            notes++;
        }
        await _db.SaveChangesAsync(ct);

        foreach (var review in (request.FlashcardReviews ?? Array.Empty<ReviewFlashcardRequest>()).OrderBy(r => r.ReviewedAt ?? DateTime.MaxValue).Take(500))
        {
            try
            {
                var result = await _flashcards.ReviewAsync(userId, review, ct);
                if (!result.Duplicate) reviews++;
            }
            catch (AppException)
            {
                conflicts.Add(new SyncConflictDto("flashcardReview", review.FlashcardId, "rejected"));
            }
        }
        return new SyncPushResponse(bookmarks, notes, reviews, conflicts, _clock.UtcNow);
    }

    public async Task<SyncPullResponse> PullAsync(Guid userId, DateTime? since, CancellationToken ct)
    {
        var from = since?.ToUniversalTime() ?? DateTime.MinValue;
        var bookmarks = await _db.QuestionBookmarks.AsNoTracking().Where(b => b.UserId == userId && b.UpdatedAt > from)
            .Select(b => new BookmarkStateDto(b.QuestionId, !b.IsRemoved, b.ClientUpdatedAt)).ToListAsync(ct);
        var notes = await _db.QuestionNotes.AsNoTracking().Where(n => n.UserId == userId && n.UpdatedAt > from)
            .Select(n => new NoteStateDto(n.QuestionId, n.Content, n.IsRemoved, n.ClientUpdatedAt)).ToListAsync(ct);
        return new SyncPullResponse(bookmarks, notes, _clock.UtcNow);
    }
}
