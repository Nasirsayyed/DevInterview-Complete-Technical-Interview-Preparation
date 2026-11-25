using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Learning;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Flashcards;

public interface IFlashcardService
{
    Task<IReadOnlyList<FlashcardDeckDto>> GetDecksAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<FlashcardDto>> GetCardsAsync(Guid userId, FlashcardQuery query, CancellationToken ct);
    Task<FlashcardReviewResultDto> ReviewAsync(Guid userId, ReviewFlashcardRequest request, CancellationToken ct);
    Task SetBookmarkAsync(Guid userId, Guid flashcardId, bool bookmarked, CancellationToken ct);
}

public sealed class FlashcardService : IFlashcardService
{
    private readonly IAppDbContext _db;
    private readonly IGamificationService _gamification;
    private readonly IClock _clock;

    public FlashcardService(IAppDbContext db, IGamificationService gamification, IClock clock)
    {
        _db = db;
        _gamification = gamification;
        _clock = clock;
    }

    private IQueryable<Flashcard> Published => _db.Flashcards.Where(f => f.IsPublished && f.IsActive && !f.IsDeleted);

    public async Task<IReadOnlyList<FlashcardDeckDto>> GetDecksAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var cards = await Published.AsNoTracking()
            .Select(f => new { f.Id, f.TechnologyId, Name = f.Technology == null ? "General" : f.Technology.Name }).ToListAsync(ct);
        var states = await _db.FlashcardReviews.AsNoTracking().Where(r => r.UserId == userId)
            .Select(r => new { r.FlashcardId, r.DueAt, r.IntervalDays, r.LastReviewedAt }).ToDictionaryAsync(r => r.FlashcardId, ct);
        return cards.GroupBy(c => new { c.TechnologyId, c.Name }).OrderBy(g => g.Key.Name)
            .Select(g => new FlashcardDeckDto(g.Key.TechnologyId, g.Key.Name, g.Count(),
                g.Count(c => states.TryGetValue(c.Id, out var s) && s.LastReviewedAt != null && s.DueAt <= now),
                g.Count(c => !states.TryGetValue(c.Id, out var s) || s.LastReviewedAt == null),
                g.Count(c => states.TryGetValue(c.Id, out var s) && s.IntervalDays >= 21)))
            .ToList();
    }

    public async Task<IReadOnlyList<FlashcardDto>> GetCardsAsync(Guid userId, FlashcardQuery query, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var limit = Math.Clamp(query.Limit, 1, 200);
        var cards = Published.AsNoTracking();
        if (query.TechnologyId is { } t) cards = cards.Where(f => f.TechnologyId == t);

        var joined = from f in cards
                     join r in _db.FlashcardReviews.Where(r => r.UserId == userId) on f.Id equals r.FlashcardId into rs
                     from r in rs.DefaultIfEmpty()
                     select new { f, r };
        if (query.BookmarkedOnly) joined = joined.Where(x => x.r != null && x.r.IsBookmarked);
        // Due session: cards whose review is due, then new cards (SM-2 ordering: most overdue first).
        if (query.DueOnly) joined = joined.Where(x => x.r == null || x.r.DueAt <= now);

        var rows = await joined.OrderBy(x => x.r == null ? 1 : 0).ThenBy(x => x.r == null ? now : x.r.DueAt).ThenBy(x => x.f.Difficulty)
            .Take(limit)
            .Select(x => new
            {
                x.f.Id, x.f.Front, x.f.Back, x.f.Difficulty,
                Tech = x.f.Technology == null ? null : new RefDto(x.f.Technology.Id, x.f.Technology.Name, x.f.Technology.Slug),
                Cat = x.f.Category == null ? null : new RefDto(x.f.Category.Id, x.f.Category.Name, x.f.Category.Slug),
                Review = x.r
            }).ToListAsync(ct);
        return rows.Select(x => new FlashcardDto(x.Id, x.Front, x.Back, x.Tech, x.Cat, x.Difficulty.ToString(), x.Review?.IsBookmarked ?? false,
            x.Review?.DueAt, x.Review?.Repetitions ?? 0, x.Review?.IntervalDays ?? 0, x.Review is null, x.Review?.LastGrade?.ToString())).ToList();
    }

    /// <summary>
    /// Applies one review. Reviews are idempotent by ClientReviewId (safe for offline replays) and applied in
    /// chronological order: a replayed review older than the card's last review is logged but does not rewind the schedule.
    /// </summary>
    public async Task<FlashcardReviewResultDto> ReviewAsync(Guid userId, ReviewFlashcardRequest request, CancellationToken ct)
    {
        var grade = EnumParser.Parse<FlashcardGrade>(request.Grade, nameof(request.Grade));
        if (!await Published.AnyAsync(f => f.Id == request.FlashcardId, ct)) throw new NotFoundException("Flashcard", request.FlashcardId);
        var now = _clock.UtcNow;
        var reviewedAt = request.ReviewedAt is { } at && at <= now.AddMinutes(5) && at > now.AddDays(-60)
            ? DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc) : now;

        var state = await _db.FlashcardReviews.FirstOrDefaultAsync(r => r.UserId == userId && r.FlashcardId == request.FlashcardId, ct);
        if (await _db.FlashcardReviewLogs.AnyAsync(l => l.UserId == userId && l.ClientReviewId == request.ClientReviewId, ct))
            return new FlashcardReviewResultDto(request.FlashcardId, state?.DueAt ?? now, state?.IntervalDays ?? 0, state?.EaseFactor ?? SpacedRepetition.DefaultEase, 0, true);

        _db.FlashcardReviewLogs.Add(new FlashcardReviewLog { UserId = userId, FlashcardId = request.FlashcardId, ClientReviewId = request.ClientReviewId, Grade = grade, ReviewedAt = reviewedAt });
        if (state is null)
        {
            state = new FlashcardReview { UserId = userId, FlashcardId = request.FlashcardId, DueAt = now };
            _db.FlashcardReviews.Add(state);
        }
        if (state.LastReviewedAt is null || reviewedAt >= state.LastReviewedAt)
        {
            var result = SpacedRepetition.Review(new SrsState(state.EaseFactor, state.IntervalDays, state.Repetitions, state.Lapses), grade, reviewedAt);
            state.EaseFactor = Math.Round(result.State.EaseFactor, 3);
            state.IntervalDays = result.State.IntervalDays;
            state.Repetitions = result.State.Repetitions;
            state.Lapses = result.State.Lapses;
            state.DueAt = result.DueAt;
            state.LastReviewedAt = reviewedAt;
            state.LastGrade = grade;
        }
        state.TotalReviews++;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ResetTracking(); // concurrent duplicate replay of the same ClientReviewId
            var current = await _db.FlashcardReviews.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.FlashcardId == request.FlashcardId, ct);
            return new FlashcardReviewResultDto(request.FlashcardId, current?.DueAt ?? now, current?.IntervalDays ?? 0, current?.EaseFactor ?? SpacedRepetition.DefaultEase, 0, true);
        }

        var dayStart = now.Date;
        var todaysXp = await _db.UserXpTransactions.Where(x => x.UserId == userId && x.SourceType == XpSourceType.FlashcardReview && x.CreatedAt >= dayStart)
            .SumAsync(x => (int?)x.Amount, ct) ?? 0;
        var xp = todaysXp < XpRules.DailyFlashcardXpCap
            ? await _gamification.AwardXpAsync(userId, XpRules.FlashcardReview, XpSourceType.FlashcardReview, $"card:{request.ClientReviewId}", "Flashcard review", ct)
            : 0;
        await _gamification.RecordActivityAsync(userId, ActivityType.FlashcardReviewed, request.FlashcardId, null, 15, ct);
        return new FlashcardReviewResultDto(request.FlashcardId, state.DueAt, state.IntervalDays, state.EaseFactor, xp, false);
    }

    public async Task SetBookmarkAsync(Guid userId, Guid flashcardId, bool bookmarked, CancellationToken ct)
    {
        if (!await Published.AnyAsync(f => f.Id == flashcardId, ct)) throw new NotFoundException("Flashcard", flashcardId);
        var state = await _db.FlashcardReviews.FirstOrDefaultAsync(r => r.UserId == userId && r.FlashcardId == flashcardId, ct);
        if (state is null)
        {
            state = new FlashcardReview { UserId = userId, FlashcardId = flashcardId, DueAt = _clock.UtcNow };
            _db.FlashcardReviews.Add(state);
        }
        state.IsBookmarked = bookmarked;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class ReviewFlashcardRequestValidator : AbstractValidator<ReviewFlashcardRequest>
{
    public ReviewFlashcardRequestValidator()
    {
        RuleFor(x => x.FlashcardId).NotEmpty();
        RuleFor(x => x.ClientReviewId).NotEmpty();
        RuleFor(x => x.Grade).NotEmpty().Must(g => Enum.TryParse<FlashcardGrade>(g, true, out _)).WithMessage("Grade must be Again, Hard, Good or Easy.");
    }
}
