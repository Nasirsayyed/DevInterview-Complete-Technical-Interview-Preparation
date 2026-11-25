namespace DevInterview.Domain.Rules;

public sealed record GradedAnswer(bool Attempted, bool IsCorrect, decimal PointsAwarded);

public sealed record QuizScore(
    int Total, int Attempted, int Correct, int Incorrect, int Skipped,
    decimal Score, decimal MaxScore, decimal Percentage, decimal Accuracy, bool Passed);

/// <summary>Pure quiz grading rules shared by every quiz mode.</summary>
public static class QuizScoring
{
    /// <summary>
    /// Grades one objective answer. A question is correct only if the selected option set equals the
    /// correct option set exactly (this covers single choice, true/false and multiple select).
    /// Wrong answers lose <paramref name="negativeFraction"/> × points; skipped answers score 0.
    /// </summary>
    public static GradedAnswer Grade(
        IReadOnlyCollection<Guid> selected, IReadOnlyCollection<Guid> correct, decimal points, decimal negativeFraction)
    {
        if (negativeFraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(negativeFraction));
        if (selected.Count == 0) return new GradedAnswer(false, false, 0m);

        var isCorrect = correct.Count > 0 && selected.ToHashSet().SetEquals(correct);
        var awarded = isCorrect ? points : -Math.Round(points * negativeFraction, 2);
        return new GradedAnswer(true, isCorrect, awarded);
    }

    public static QuizScore Summarize(IReadOnlyList<GradedAnswer> answers, IReadOnlyList<decimal> points, int passingPercentage)
    {
        if (answers.Count != points.Count) throw new ArgumentException("answers and points must align");
        var max = points.Sum();
        var raw = answers.Sum(a => a.PointsAwarded);
        var score = Math.Max(0m, raw); // negative marking never drives the total below zero
        var attempted = answers.Count(a => a.Attempted);
        var correct = answers.Count(a => a.IsCorrect);
        var percentage = max == 0 ? 0 : Math.Round(score / max * 100m, 2);
        var accuracy = attempted == 0 ? 0 : Math.Round((decimal)correct / attempted * 100m, 2);
        return new QuizScore(
            answers.Count, attempted, correct, attempted - correct, answers.Count - attempted,
            score, max, percentage, accuracy, percentage >= passingPercentage);
    }

    public static IReadOnlyList<Guid> ParseIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<Guid>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).Distinct().ToList();

    public static string ToCsv(IEnumerable<Guid> ids) => string.Join(',', ids.Distinct());
}
