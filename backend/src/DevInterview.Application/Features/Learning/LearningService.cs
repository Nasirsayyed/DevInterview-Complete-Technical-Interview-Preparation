using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Gamification;
using DevInterview.Application.Features.Questions;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Learning;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Learning;

public interface ILearningService
{
    Task<IReadOnlyList<LearningPathListItemDto>> GetPathsAsync(Guid userId, string? level, Guid? technologyId, CancellationToken ct);
    Task<LearningPathDetailDto> GetPathAsync(Guid userId, Guid pathId, CancellationToken ct);
    Task<LessonDetailDto> GetLessonAsync(Guid userId, Guid lessonId, CancellationToken ct);
    Task<LessonProgressDto> UpdateProgressAsync(Guid userId, UpdateLessonProgressRequest request, CancellationToken ct);
    Task<IReadOnlyList<LearningPathListItemDto>> GetRecommendedAsync(Guid userId, int take, CancellationToken ct);
}

public sealed class LearningService : ILearningService
{
    private readonly IAppDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly IGamificationService _gamification;
    private readonly QuestionService _questions;
    private readonly IClock _clock;

    public LearningService(IAppDbContext db, IEntitlementService entitlements, IGamificationService gamification, QuestionService questions, IClock clock)
    {
        _db = db;
        _entitlements = entitlements;
        _gamification = gamification;
        _questions = questions;
        _clock = clock;
    }

    private IQueryable<LearningPath> Published => _db.LearningPaths.Where(p => p.IsPublished && p.IsActive && !p.IsDeleted);

    private IQueryable<LearningPathListItemDto> Project(IQueryable<LearningPath> q, Guid userId) => q.Select(p => new
    {
        p,
        Lessons = p.Modules.SelectMany(m => m.Lessons).Count(),
        Done = _db.UserLearningProgress.Count(x => x.UserId == userId && x.PathId == p.Id && x.Status == ProgressStatus.Completed)
    }).Select(x => new LearningPathListItemDto(x.p.Id, x.p.Title, x.p.Slug, x.p.Description, x.p.Level.ToString(), x.p.EstimatedHours,
        x.p.Modules.Count, x.Lessons, x.Done, x.Lessons == 0 ? 0 : x.Done * 100 / x.Lessons, x.p.IsPremium,
        x.p.Technology == null ? null : new RefDto(x.p.Technology.Id, x.p.Technology.Name, x.p.Technology.Slug)));

    public async Task<IReadOnlyList<LearningPathListItemDto>> GetPathsAsync(Guid userId, string? level, Guid? technologyId, CancellationToken ct)
    {
        var q = Published.AsNoTracking();
        if (EnumParser.ParseOptional<ExperienceLevel>(level, "level") is { } l) q = q.Where(p => p.Level == l);
        if (technologyId is { } t) q = q.Where(p => p.TechnologyId == t);
        return await Project(q.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Title), userId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LearningPathListItemDto>> GetRecommendedAsync(Guid userId, int take, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.AsNoTracking().Include(p => p.PreferredTechnologies).FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var preferred = profile?.PreferredTechnologies.Select(t => t.TechnologyId).ToList() ?? new List<Guid>();
        var level = profile?.ExperienceLevel ?? ExperienceLevel.Beginner;
        var weak = await _db.UserPerformanceStats.Where(s => s.UserId == userId && s.Attempted >= 5 && s.Correct * 100 < s.Attempted * 60)
            .Select(s => s.TechnologyId).ToListAsync(ct);
        var all = await Project(Published.AsNoTracking(), userId).ToListAsync(ct);
        return all.Where(p => p.ProgressPercent < 100)
            .OrderByDescending(p => p.Technology is not null && weak.Contains(p.Technology.Id))
            .ThenByDescending(p => p.Technology is not null && preferred.Contains(p.Technology.Id))
            .ThenByDescending(p => p.ProgressPercent is > 0 and < 100)
            .ThenBy(p => Math.Abs((int)Enum.Parse<ExperienceLevel>(p.Level) - (int)level))
            .Take(take).ToList();
    }

    private async Task<LearningPath> LoadPathAsync(Guid pathId, CancellationToken ct) =>
        await Published.AsNoTracking().Include(p => p.Technology).Include(p => p.Modules).ThenInclude(m => m.Lessons).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == pathId, ct) ?? throw new NotFoundException("Learning path", pathId);

    public async Task<LearningPathDetailDto> GetPathAsync(Guid userId, Guid pathId, CancellationToken ct)
    {
        var path = await LoadPathAsync(pathId, ct);
        if (path.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This learning path", ct);
        var progress = await _db.UserLearningProgress.AsNoTracking().Where(p => p.UserId == userId && p.PathId == pathId)
            .ToDictionaryAsync(p => p.LessonId, p => p.Status, ct);
        var modules = path.Modules.OrderBy(m => m.DisplayOrder).Select(m =>
        {
            var lessons = m.Lessons.OrderBy(l => l.DisplayOrder).Select(l => new LessonSummaryDto(l.Id, l.Title, l.Type.ToString(), l.EstimatedMinutes,
                progress.GetValueOrDefault(l.Id, ProgressStatus.NotStarted).ToString())).ToList();
            return new LearningModuleDto(m.Id, m.Title, m.Description, m.DisplayOrder, lessons, lessons.Count(l => l.Status == nameof(ProgressStatus.Completed)));
        }).ToList();
        var total = modules.Sum(m => m.Lessons.Count);
        var done = modules.Sum(m => m.CompletedLessons);
        var next = modules.SelectMany(m => m.Lessons).FirstOrDefault(l => l.Status != nameof(ProgressStatus.Completed))?.Id;
        return new LearningPathDetailDto(path.Id, path.Title, path.Slug, path.Description, path.Level.ToString(), path.EstimatedHours, path.IsPremium,
            path.Technology is null ? null : new RefDto(path.Technology.Id, path.Technology.Name, path.Technology.Slug),
            total, done, total == 0 ? 0 : done * 100 / total, next, modules);
    }

    public async Task<LessonDetailDto> GetLessonAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var lesson = await _db.LearningLessons.AsNoTracking().Include(l => l.Module).ThenInclude(m => m.Path)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct) ?? throw new NotFoundException("Lesson", lessonId);
        var path = await LoadPathAsync(lesson.Module.PathId, ct);
        if (path.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This learning path", ct);

        var ordered = path.Modules.OrderBy(m => m.DisplayOrder).SelectMany(m => m.Lessons.OrderBy(l => l.DisplayOrder)).Select(l => l.Id).ToList();
        var index = ordered.IndexOf(lessonId);
        var questionIds = await _db.LessonQuestions.Where(lq => lq.LessonId == lessonId).OrderBy(lq => lq.DisplayOrder).Select(lq => lq.QuestionId).ToListAsync(ct);
        var questions = questionIds.Count == 0 ? new List<Contracts.Content.QuestionListItemDto>() :
            await _questions.ProjectList(_db.Questions.Where(q => questionIds.Contains(q.Id) && q.Status == ContentStatus.Published), userId).ToListAsync(ct);
        var status = await _db.UserLearningProgress.Where(p => p.UserId == userId && p.LessonId == lessonId).Select(p => (ProgressStatus?)p.Status).FirstOrDefaultAsync(ct);

        return new LessonDetailDto(lesson.Id, path.Id, path.Title, lesson.ModuleId, lesson.Module.Title, lesson.Title, lesson.Content, lesson.Type.ToString(),
            lesson.EstimatedMinutes, lesson.ExerciseInstructions, lesson.QuizId,
            questions.OrderBy(q => questionIds.IndexOf(q.Id)).ToList(), (status ?? ProgressStatus.NotStarted).ToString(),
            index > 0 ? ordered[index - 1] : null, index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null);
    }

    public async Task<LessonProgressDto> UpdateProgressAsync(Guid userId, UpdateLessonProgressRequest request, CancellationToken ct)
    {
        var status = EnumParser.Parse<ProgressStatus>(request.Status, nameof(request.Status));
        var lesson = await _db.LearningLessons.AsNoTracking().Include(l => l.Module).FirstOrDefaultAsync(l => l.Id == request.LessonId, ct)
                     ?? throw new NotFoundException("Lesson", request.LessonId);
        var path = await _db.LearningPaths.AsNoTracking().FirstAsync(p => p.Id == lesson.Module.PathId, ct);
        if (path.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "This learning path", ct);

        var progress = await _db.UserLearningProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lesson.Id, ct);
        if (progress is null)
        {
            progress = new UserLearningProgress { UserId = userId, LessonId = lesson.Id, PathId = path.Id };
            _db.UserLearningProgress.Add(progress);
        }
        var seconds = Math.Clamp(request.TimeSpentSeconds, 0, 4 * 3600);
        progress.TimeSpentSeconds += seconds;
        // Completed is terminal: re-reading a finished lesson never moves it back.
        var newlyCompleted = status == ProgressStatus.Completed && progress.Status != ProgressStatus.Completed;
        if (progress.Status != ProgressStatus.Completed) progress.Status = status;
        if (newlyCompleted) progress.CompletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        var xp = 0;
        if (newlyCompleted)
        {
            xp += await _gamification.AwardXpAsync(userId, XpRules.LessonCompleted, XpSourceType.LessonCompleted, $"lesson:{lesson.Id}", $"Completed lesson {lesson.Title}", ct);
            xp += await _gamification.RecordActivityAsync(userId, ActivityType.LessonCompleted, lesson.Id, path.TechnologyId, seconds, ct);
        }
        else if (seconds > 0)
        {
            await _gamification.RecordActivityAsync(userId, ActivityType.LessonCompleted, null, path.TechnologyId, seconds, ct);
        }

        var total = await _db.LearningLessons.CountAsync(l => l.Module.PathId == path.Id, ct);
        var done = await _db.UserLearningProgress.CountAsync(p => p.UserId == userId && p.PathId == path.Id && p.Status == ProgressStatus.Completed, ct);
        return new LessonProgressDto(lesson.Id, progress.Status.ToString(), total == 0 ? 0 : done * 100 / total, xp);
    }
}

public sealed class UpdateLessonProgressRequestValidator : AbstractValidator<UpdateLessonProgressRequest>
{
    public UpdateLessonProgressRequestValidator()
    {
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().Must(s => Enum.TryParse<ProgressStatus>(s, true, out _)).WithMessage("Status must be NotStarted, InProgress or Completed.");
        RuleFor(x => x.TimeSpentSeconds).InclusiveBetween(0, 24 * 3600);
    }
}
