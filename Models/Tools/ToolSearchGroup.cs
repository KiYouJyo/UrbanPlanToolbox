namespace UrbanPlanToolbox.Models.Tools;

public sealed record ToolSearchGroup(
    string Header,
    IReadOnlyList<ToolDefinition> Tools,
    bool IsFavoritesGroup);
