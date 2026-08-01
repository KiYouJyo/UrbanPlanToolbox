using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

public sealed class ProjectV1ToV2Migration : IDataMigration
{
    public string Name => "project-v1-to-v2-workspace";
    public int FromVersion => 1;
    public int ToVersion => 2;

    public JsonNode Apply(JsonNode payload)
    {
        if (payload is not JsonObject project || !project.ContainsKey("id"))
        {
            return payload;
        }

        project.TryAdd("planningRequirements", null);
        project.TryAdd("milestones", new JsonArray());
        return project;
    }
}
