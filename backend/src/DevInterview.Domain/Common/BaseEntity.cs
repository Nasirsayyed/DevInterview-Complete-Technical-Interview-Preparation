namespace DevInterview.Domain.Common;

/// <summary>Base type for all persisted entities. Timestamps are UTC and maintained by the DbContext.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Entities that are hidden instead of physically deleted.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>Content entities that can be enabled/disabled by administrators.</summary>
public interface IActivatable
{
    bool IsActive { get; set; }
}

/// <summary>Content entity with active flag and soft delete.</summary>
public abstract class ContentEntity : BaseEntity, ISoftDeletable, IActivatable
{
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
