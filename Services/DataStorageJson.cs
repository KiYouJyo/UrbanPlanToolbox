using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanPlanToolbox.Services;

public static class DataStorageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
}
