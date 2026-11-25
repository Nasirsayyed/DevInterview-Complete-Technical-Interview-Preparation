using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Rules;

/// <summary>XP amounts for each rewarded action. Each reward is keyed by a source key so it is granted once.</summary>
public static class XpRules
{
    public const int QuestionCorrect = 5;
    public const int QuizCompleted = 20;
    public const int QuizPassedBonus = 20;
    public const int MockInterviewCompleted = 50;
    public const int LessonCompleted = 10;
    public const int FlashcardReview = 1;
    public const int DailyFlashcardXpCap = 30;
    public const int SevenDayStreakBonus = 50;
    public const int WeeklyGoalBonus = 75;

    public static int ChallengeSolved(ChallengeDifficulty difficulty) => difficulty switch
    {
        ChallengeDifficulty.Easy => 20,
        ChallengeDifficulty.Medium => 40,
        ChallengeDifficulty.Hard => 80,
        _ => 20
    };
}

public sealed record LevelInfo(int Level, int TotalXp, int CurrentLevelXp, int NextLevelXp)
{
    public double ProgressToNext => NextLevelXp == CurrentLevelXp ? 1 :
        (double)(TotalXp - CurrentLevelXp) / (NextLevelXp - CurrentLevelXp);
}

/// <summary>Level n starts at 50·n·(n−1) XP: L1=0, L2=100, L3=300, L4=600, L5=1000 …</summary>
public static class LevelCalculator
{
    public static int XpForLevel(int level) => level <= 1 ? 0 : 50 * level * (level - 1);

    public static LevelInfo FromXp(int totalXp)
    {
        totalXp = Math.Max(0, totalXp);
        var level = 1;
        while (XpForLevel(level + 1) <= totalXp) level++;
        return new LevelInfo(level, totalXp, XpForLevel(level), XpForLevel(level + 1));
    }

    public static string Title(int level) => level switch
    {
        < 3 => "Novice",
        < 6 => "Apprentice",
        < 10 => "Practitioner",
        < 15 => "Specialist",
        < 25 => "Expert",
        _ => "Master"
    };
}

public sealed record StreakUpdate(int CurrentStreak, int LongestStreak, DateOnly LastActiveDate, bool Incremented);

/// <summary>Daily streak rules using the user's local calendar date.</summary>
public static class StreakCalculator
{
    public static StreakUpdate RegisterActivity(int current, int longest, DateOnly? lastActive, DateOnly today)
    {
        if (lastActive == today) return new StreakUpdate(current, longest, today, false);
        var next = lastActive is { } last && last.AddDays(1) == today ? current + 1 : 1;
        return new StreakUpdate(next, Math.Max(longest, next), today, true);
    }

    /// <summary>Streak shown to the user: it is broken if the last active day is before yesterday.</summary>
    public static int EffectiveStreak(int current, DateOnly? lastActive, DateOnly today) =>
        lastActive is { } last && (last == today || last.AddDays(1) == today) ? current : 0;
}

/// <summary>Achievement codes and their thresholds. Definitions (name/description/XP) are seeded into the DB.</summary>
public static class AchievementCodes
{
    public const string FirstQuiz = "FIRST_QUIZ";
    public const string QuizAce = "QUIZ_ACE";
    public const string SqlMaster = "SQL_MASTER";
    public const string CSharpBeginner = "CSHARP_BEGINNER";
    public const string CodingWarrior = "CODING_WARRIOR";
    public const string ProblemSolver = "PROBLEM_SOLVER";
    public const string SevenDayStreak = "SEVEN_DAY_STREAK";
    public const string ThirtyDayStreak = "THIRTY_DAY_STREAK";
    public const string InterviewChampion = "INTERVIEW_CHAMPION";
    public const string FirstMockInterview = "FIRST_MOCK_INTERVIEW";
    public const string Scholar = "SCHOLAR";
    public const string Centurion = "CENTURION";

    public const int SqlMasterCorrect = 50;
    public const int CSharpBeginnerCorrect = 10;
    public const int CodingWarriorSolved = 10;
    public const int ProblemSolverSolved = 1;
    public const int InterviewChampionScore = 85;
    public const int ScholarLessons = 20;
    public const int CenturionAttempts = 100;
}
