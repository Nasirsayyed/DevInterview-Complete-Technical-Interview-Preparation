using DevInterview.Domain.Common;
using DevInterview.Domain.Enums;

namespace DevInterview.Domain.Entities;

public class Company : ContentEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public Difficulty DifficultyLevel { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPremium { get; set; } = true;
    /// <summary>Source classification for the overview text.</summary>
    public ContentSource OverviewSource { get; set; } = ContentSource.Illustrative;
    public ICollection<CompanyInterviewRound> Rounds { get; set; } = new List<CompanyInterviewRound>();
    public ICollection<CompanyTopic> Topics { get; set; } = new List<CompanyTopic>();
    public ICollection<CompanyQuestionCollection> Collections { get; set; } = new List<CompanyQuestionCollection>();
}

public class CompanyInterviewRound : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ContentSource Source { get; set; } = ContentSource.Illustrative;
    /// <summary>Citation (URL or reference) required when Source is Verified.</summary>
    public string? SourceReference { get; set; }
}

public class CompanyTopic : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid? TechnologyId { get; set; }
    public Technology? Technology { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>A curated set of practice questions/challenges relevant to a company's interview style.
/// The questions are DevInterview practice content, not claims of real interview questions,
/// unless Source is Verified with a reference.</summary>
public class CompanyQuestionCollection : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CompanyCollectionKind Kind { get; set; }
    public ContentSource Source { get; set; } = ContentSource.Illustrative;
    public string? SourceReference { get; set; }
    public ICollection<CompanyCollectionQuestion> Questions { get; set; } = new List<CompanyCollectionQuestion>();
    public ICollection<CompanyCollectionChallenge> Challenges { get; set; } = new List<CompanyCollectionChallenge>();
}

public class CompanyCollectionQuestion
{
    public Guid CollectionId { get; set; }
    public CompanyQuestionCollection Collection { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int DisplayOrder { get; set; }
}

public class CompanyCollectionChallenge
{
    public Guid CollectionId { get; set; }
    public CompanyQuestionCollection Collection { get; set; } = null!;
    public Guid ChallengeId { get; set; }
    public CodingChallenge Challenge { get; set; } = null!;
    public int DisplayOrder { get; set; }
}
