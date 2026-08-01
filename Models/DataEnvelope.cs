using System.Text.Json.Serialization;

namespace UrbanPlanToolbox.Models;

public sealed class DataEnvelope<T>
{
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("savedAtUtc")]
    public required DateTimeOffset SavedAtUtc { get; init; }

    [JsonPropertyName("payload")]
    public required T Payload { get; init; }
}
