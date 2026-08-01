using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

public sealed class ProjectV2ToV3Migration : IDataMigration
{
    public string Name => "project-v2-to-v3-kinds-and-details";
    public int FromVersion => 2;
    public int ToVersion => 3;

    public JsonNode Apply(JsonNode payload)
    {
        if (payload is not JsonObject root) return payload;

        if (root.ContainsKey("id"))
        {
            var design = new JsonObject
            {
                ["administrativeRegion"] = Take(root, "administrativeArea"),
                ["latitude"] = Take(root, "latitude"),
                ["longitude"] = Take(root, "longitude"),
                ["description"] = Take(root, "description"),
                ["planningRequirements"] = Take(root, "planningRequirements")
            };
            root["kind"] = "design";
            root["designDetails"] = design;
            root["researchDetails"] = null;
            return root;
        }

        if (root["projects"] is JsonArray projects)
        {
            foreach (var entry in projects.OfType<JsonObject>()) entry["kind"] = "design";
        }
        return root;
    }

    private static JsonNode? Take(JsonObject source, string name)
    {
        if (!source.TryGetPropertyValue(name, out var value)) return null;
        source.Remove(name);
        return value;
    }
}
