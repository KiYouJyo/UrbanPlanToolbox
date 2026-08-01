namespace UrbanPlanToolbox.Models.Tools;

/// <summary>A tool with display text resolved for the current UI language.</summary>
public sealed record LocalizedTool(ToolDefinition Definition, string DisplayName, string Description)
{
    public string Id => Definition.Id;
    public string IconGlyph => Definition.IconGlyph;
}

public sealed record ToolSearchGroup(
    string Header,
    IReadOnlyList<LocalizedTool> Tools,
    bool IsFavoritesGroup);
