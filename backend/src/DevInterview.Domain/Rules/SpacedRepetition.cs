using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Rules;

public sealed record SrsState(double EaseFactor, int IntervalDays, int Repetitions, int Lapses);

public sealed record SrsResult(SrsState State, DateTime DueAt);

/// <summary>
/// SM-2 spaced repetition (Wozniak, 1990) adapted to four buttons.
/// Grades map to SM-2 quality: Again=1, Hard=3, Good=4, Easy=5.
/// <list type="bullet">
/// <item>Quality &lt; 3 (Again): repetitions reset, interval 1 day, lapse counted, card relearns in 10 minutes.</item>
/// <item>First success: 1 day; second: 6 days; afterwards interval × EF (Hard uses ×1.2, Easy adds a 1.3 bonus).</item>
/// <item>EF' = EF + (0.1 − (5−q)(0.08 + (5−q)·0.02)), never below 1.3.</item>
/// </list>
/// </summary>
public static class SpacedRepetition
{
    public const double MinimumEase = 1.3;
    public const double DefaultEase = 2.5;
    public const int MaxIntervalDays = 365;

    public static SrsState Initial => new(DefaultEase, 0, 0, 0);

    public static int Quality(FlashcardGrade grade) => grade switch
    {
        FlashcardGrade.Again => 1,
        FlashcardGrade.Hard => 3,
        FlashcardGrade.Good => 4,
        FlashcardGrade.Easy => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(grade))
    };

    public static SrsResult Review(SrsState state, FlashcardGrade grade, DateTime reviewedAtUtc)
    {
        var q = Quality(grade);
        var ease = Math.Max(MinimumEase, state.EaseFactor + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02)));

        if (q < 3)
        {
            var lapsed = new SrsState(ease, 1, 0, state.Lapses + 1);
            return new SrsResult(lapsed, reviewedAtUtc.AddMinutes(10));
        }

        var repetitions = state.Repetitions + 1;
        int interval = repetitions switch
        {
            1 => grade == FlashcardGrade.Easy ? 3 : 1,
            2 => grade == FlashcardGrade.Hard ? 3 : 6,
            _ => (int)Math.Round(state.IntervalDays * grade switch
            {
                FlashcardGrade.Hard => 1.2,
                FlashcardGrade.Easy => ease * 1.3,
                _ => ease
            })
        };
        interval = Math.Clamp(Math.Max(interval, state.IntervalDays + (repetitions > 2 ? 1 : 0)), 1, MaxIntervalDays);
        return new SrsResult(new SrsState(ease, interval, repetitions, state.Lapses), reviewedAtUtc.AddDays(interval));
    }
}
