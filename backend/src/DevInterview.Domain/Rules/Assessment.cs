using System.Text.RegularExpressions;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Rules;

/// <summary>
/// Server-side guard rails applied to AI answer scores so that vague or very short answers can never be
/// scored highly, regardless of what the model returns.
/// </summary>
public static partial class AnswerHeuristics
{
    public static int WordCount(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : WordRegex().Matches(text).Count;

    /// <summary>Maximum score an answer of this length may receive.</summary>
    public static int MaxScoreForLength(int words) => words switch
    {
        0 => 0,
        < 5 => 10,
        < 15 => 35,
        < 30 => 60,
        _ => 100
    };

    public static int Clamp(int score) => Math.Clamp(score, 0, 100);

    public static int ApplyCap(int aiScore, string? answer) =>
        Math.Min(Clamp(aiScore), MaxScoreForLength(WordCount(answer)));

    [GeneratedRegex(@"[\p{L}\p{N}_#+.\-]+")]
    private static partial Regex WordRegex();
}

/// <summary>Adaptive difficulty for mock interviews: strong answers raise, weak answers lower, one step at a time.</summary>
public static class AdaptiveDifficulty
{
    public const int RaiseThreshold = 80;
    public const int LowerThreshold = 45;

    public static Difficulty Next(Difficulty current, int answerScore) => answerScore switch
    {
        >= RaiseThreshold when current < Difficulty.Expert => current + 1,
        < LowerThreshold when current > Difficulty.Beginner => current - 1,
        _ => current
    };
}

/// <summary>Compares program output to expected output: line-ending, trailing-whitespace and trailing-blank-line
/// insensitive, but otherwise exact.</summary>
public static class OutputComparer
{
    public static string Normalize(string? output)
    {
        if (string.IsNullOrEmpty(output)) return string.Empty;
        var lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd());
        return string.Join('\n', lines).TrimEnd('\n').Trim();
    }

    public static bool Matches(string? actual, string? expected) =>
        string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal);
}

public sealed record PreparationInputs(
    int QuestionsAttempted, int QuestionsCorrect,
    int ChallengesAttempted, int ChallengesSolved,
    int MockInterviewsCompleted, double AverageMockScore,
    int LessonsCompleted, int LessonsAvailable);

/// <summary>
/// Overall preparation score (0-100). Each component is weighted by a confidence factor
/// min(1, volume / target) so that a handful of lucky answers does not produce a high score.
/// Weights: questions 40%, coding 20%, mock interviews 25%, learning 15%.
/// </summary>
public static class PreparationScore
{
    public static int Calculate(PreparationInputs i)
    {
        double Confidence(int volume, int target) => Math.Min(1.0, volume / (double)target);

        var questions = i.QuestionsAttempted == 0 ? 0 :
            (double)i.QuestionsCorrect / i.QuestionsAttempted * 100 * Confidence(i.QuestionsAttempted, 50);
        var coding = i.ChallengesAttempted == 0 ? 0 :
            (double)i.ChallengesSolved / i.ChallengesAttempted * 100 * Confidence(i.ChallengesAttempted, 10);
        var mock = i.MockInterviewsCompleted == 0 ? 0 : i.AverageMockScore * Confidence(i.MockInterviewsCompleted, 3);
        var learning = i.LessonsAvailable == 0 ? 0 : Math.Min(100.0, (double)i.LessonsCompleted / i.LessonsAvailable * 100);

        var total = questions * 0.40 + coding * 0.20 + mock * 0.25 + learning * 0.15;
        return (int)Math.Round(Math.Clamp(total, 0, 100));
    }
}
