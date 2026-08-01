using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

public interface IDataMigration
{
    string Name { get; }
    int FromVersion { get; }
    int ToVersion { get; }
    JsonNode Apply(JsonNode payload);
}

public sealed record DataMigrationResult(
    bool Succeeded,
    int Version,
    JsonNode? Payload,
    IReadOnlyList<string> CompletedMigrations,
    string? FailureType = null);
