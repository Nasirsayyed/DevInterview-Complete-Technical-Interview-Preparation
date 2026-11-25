using DevInterview.Domain.Enums;
using DevInterview.Domain.Rules;

namespace DevInterview.UnitTests.Domain;

public class QuizScoringTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    [Fact]
    public void Exact_set_match_is_correct_for_multiple_select()
    {
        var result = QuizScoring.Grade(new[] { B, A }, new[] { A, B }, 2m, 0m);
        Assert.True(result.IsCorrect);
        Assert.Equal(2m, result.PointsAwarded);
    }

    [Fact]
    public void Partial_selection_is_incorrect_and_negative_marking_applies()
    {
        var result = QuizScoring.Grade(new[] { A }, new[] { A, B }, 1m, 0.25m);
        Assert.True(result.Attempted);
        Assert.False(result.IsCorrect);
        Assert.Equal(-0.25m, result.PointsAwarded);
    }

    [Fact]
    public void Skipped_answer_scores_zero_even_with_negative_marking()
    {
        var result = QuizScoring.Grade(Array.Empty<Guid>(), new[] { A }, 1m, 0.5m);
        Assert.False(result.Attempted);
        Assert.Equal(0m, result.PointsAwarded);
    }

    [Fact]
    public void Summary_never_goes_below_zero_and_reports_pass_fail()
    {
        var answers = new[]
        {
            QuizScoring.Grade(new[] { B }, new[] { A }, 1m, 1m),
            QuizScoring.Grade(new[] { C }, new[] { A }, 1m, 1m),
            QuizScoring.Grade(Array.Empty<Guid>(), new[] { A }, 1m, 1m)
        };
        var summary = QuizScoring.Summarize(answers, new[] { 1m, 1m, 1m }, 60);
        Assert.Equal(0m, summary.Score);
        Assert.Equal(2, summary.Incorrect);
        Assert.Equal(1, summary.Skipped);
        Assert.False(summary.Passed);

        var passing = QuizScoring.Summarize(new[] { QuizScoring.Grade(new[] { A }, new[] { A }, 1m, 0m), QuizScoring.Grade(new[] { A }, new[] { A }, 1m, 0m) },
            new[] { 1m, 1m }, 60);
        Assert.True(passing.Passed);
        Assert.Equal(100m, passing.Percentage);
    }

    [Fact]
    public void Csv_round_trip_ignores_garbage_and_duplicates()
    {
        var csv = QuizScoring.ToCsv(new[] { A, A, B });
        Assert.Equal(new[] { A, B }, QuizScoring.ParseIds(csv + ",not-a-guid,"));
    }
}

public class SpacedRepetitionTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Good_reviews_follow_sm2_intervals()
    {
        var first = SpacedRepetition.Review(SpacedRepetition.Initial, FlashcardGrade.Good, Now);
        Assert.Equal(1, first.State.IntervalDays);
        var second = SpacedRepetition.Review(first.State, FlashcardGrade.Good, first.DueAt);
        Assert.Equal(6, second.State.IntervalDays);
        var third = SpacedRepetition.Review(second.State, FlashcardGrade.Good, second.DueAt);
        Assert.Equal((int)Math.Round(6 * third.State.EaseFactor), third.State.IntervalDays);
        Assert.Equal(second.DueAt.AddDays(third.State.IntervalDays), third.DueAt);
    }

    [Fact]
    public void Again_resets_repetitions_and_counts_a_lapse()
    {
        var state = new SrsState(2.5, 15, 4, 0);
        var result = SpacedRepetition.Review(state, FlashcardGrade.Again, Now);
        Assert.Equal(0, result.State.Repetitions);
        Assert.Equal(1, result.State.Lapses);
        Assert.Equal(Now.AddMinutes(10), result.DueAt);
        Assert.True(result.State.EaseFactor < 2.5);
    }

    [Fact]
    public void Ease_factor_never_drops_below_minimum()
    {
        var state = SpacedRepetition.Initial;
        for (var i = 0; i < 20; i++) state = SpacedRepetition.Review(state, FlashcardGrade.Again, Now).State;
        Assert.Equal(SpacedRepetition.MinimumEase, state.EaseFactor, 3);
    }

    [Fact]
    public void Easy_grows_faster_than_hard()
    {
        var baseState = new SrsState(2.5, 10, 3, 0);
        var hard = SpacedRepetition.Review(baseState, FlashcardGrade.Hard, Now).State.IntervalDays;
        var easy = SpacedRepetition.Review(baseState, FlashcardGrade.Easy, Now).State.IntervalDays;
        Assert.True(easy > hard);
        Assert.True(hard > 10);
    }
}

public class GamificationRuleTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(1000, 5)]
    public void Level_thresholds(int xp, int level) => Assert.Equal(level, LevelCalculator.FromXp(xp).Level);

    [Fact]
    public void Streak_increments_on_consecutive_days_and_resets_after_gap()
    {
        var d = new DateOnly(2026, 2, 1);
        var s1 = StreakCalculator.RegisterActivity(0, 0, null, d);
        Assert.Equal(1, s1.CurrentStreak);
        var s2 = StreakCalculator.RegisterActivity(s1.CurrentStreak, s1.LongestStreak, s1.LastActiveDate, d.AddDays(1));
        Assert.Equal(2, s2.CurrentStreak);
        var same = StreakCalculator.RegisterActivity(s2.CurrentStreak, s2.LongestStreak, s2.LastActiveDate, d.AddDays(1));
        Assert.False(same.Incremented);
        var reset = StreakCalculator.RegisterActivity(s2.CurrentStreak, s2.LongestStreak, s2.LastActiveDate, d.AddDays(5));
        Assert.Equal(1, reset.CurrentStreak);
        Assert.Equal(2, reset.LongestStreak);
    }

    [Fact]
    public void Effective_streak_is_zero_when_yesterday_was_missed()
    {
        var today = new DateOnly(2026, 2, 10);
        Assert.Equal(5, StreakCalculator.EffectiveStreak(5, today.AddDays(-1), today));
        Assert.Equal(0, StreakCalculator.EffectiveStreak(5, today.AddDays(-2), today));
    }
}

public class AssessmentRuleTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("polymorphism", 10)]
    [InlineData("It is about objects and classes basically", 35)]
    public void Vague_or_short_answers_are_capped(string answer, int expectedCap) =>
        Assert.Equal(expectedCap, AnswerHeuristics.ApplyCap(95, answer));

    [Fact]
    public void Long_answers_keep_the_ai_score() =>
        Assert.Equal(88, AnswerHeuristics.ApplyCap(88, string.Join(' ', Enumerable.Repeat("word", 40))));

    [Theory]
    [InlineData(Difficulty.Intermediate, 85, Difficulty.Advanced)]
    [InlineData(Difficulty.Intermediate, 30, Difficulty.Beginner)]
    [InlineData(Difficulty.Expert, 95, Difficulty.Expert)]
    [InlineData(Difficulty.Beginner, 10, Difficulty.Beginner)]
    [InlineData(Difficulty.Advanced, 60, Difficulty.Advanced)]
    public void Adaptive_difficulty_moves_one_step(Difficulty current, int score, Difficulty expected) =>
        Assert.Equal(expected, AdaptiveDifficulty.Next(current, score));

    [Theory]
    [InlineData("1 2\r\n3  \n\n", "1 2\n3", true)]
    [InlineData("1 2", "1  2", false)]
    [InlineData("", "", true)]
    public void Output_comparison_normalises_whitespace_at_line_ends(string actual, string expected, bool match) =>
        Assert.Equal(match, OutputComparer.Matches(actual, expected));

    [Fact]
    public void Preparation_score_is_confidence_weighted()
    {
        var lucky = PreparationScore.Calculate(new PreparationInputs(2, 2, 0, 0, 0, 0, 0, 0));
        var steady = PreparationScore.Calculate(new PreparationInputs(100, 80, 10, 7, 3, 75, 20, 20));
        Assert.True(lucky < 5, $"2/2 correct should not yield a high score, got {lucky}");
        Assert.InRange(steady, 70, 85);
        Assert.Equal(0, PreparationScore.Calculate(new PreparationInputs(0, 0, 0, 0, 0, 0, 0, 0)));
    }
}
