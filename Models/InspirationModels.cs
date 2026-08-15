namespace UrbanPlanToolbox.Models;

public enum InspirationCategory { Design, Research }

public sealed class Inspiration
{
    public Guid Id { get; init; }
    public InspirationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? LinkedProjectId { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A separate, id-less working copy. It can never be rendered as a saved card.</summary>
public sealed class InspirationDraft
{
    public InspirationCategory Category { get; set; } = InspirationCategory.Design;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDirty => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Content);
}

public sealed class InspirationDocument
{
    public List<Inspiration> Items { get; set; } = [];
    public InspirationDraft? Draft { get; set; }
}
