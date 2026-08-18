using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Introduces the project-scoped customizable workspace contract.
/// The first v1.9 workspace open materializes the project-kind-specific default layout;
/// migration only establishes the new field so old v3 data is upgraded without guessing
/// a UI layout during storage-only operations.
/// </summary>
public sealed class ProjectV3ToV4Migration : IDataMigration
{
    public string Name => "project-v3-to-v4-workspace-layout";
    public int FromVersion => 3;
    public int ToVersion => 4;

    public JsonNode Apply(JsonNode payload)
    {
        if (payload is not JsonObject root) return payload;

        // project.json has an id; projects-index.json does not.  The index contract itself
        // is unchanged, but it still travels through the same schema-versioned storage.
        if (root.ContainsKey("id") && !root.ContainsKey("workspaceLayout"))
            root["workspaceLayout"] = null;

        return root;
    }
}
