using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Reserved forward migration for the project workspace contract. v1.9 keeps the current
/// project schema at v3 because workspace layout is optional, reconstructable UI metadata;
/// the step is registered only so a future schema-v4 switch has a deterministic migration.
/// </summary>
public sealed class ProjectV3ToV4Migration : IDataMigration
{
    public string Name => "project-v3-to-v4-workspace-layout";
    public int FromVersion => 3;
    public int ToVersion => 4;

    public JsonNode Apply(JsonNode payload)
    {
        if (payload is not JsonObject root) return payload;
        if (root.ContainsKey("id") && !root.ContainsKey("workspaceLayout"))
            root["workspaceLayout"] = null;
        return root;
    }
}
