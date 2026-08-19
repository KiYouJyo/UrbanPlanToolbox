using System.Text.Json;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

internal sealed class DataPackStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IAppDataPathProvider _paths;

    public DataPackStateStore(IAppDataPathProvider paths) => _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public string GetPackDirectory(string packId)
    {
        var toolId = packId switch
        {
            ReferenceDataPackIds.PlanningRegulations => ToolIds.RegulationsIndex,
            ReferenceDataPackIds.PlanningTerminology => ToolIds.PlanningTerminology,
            ReferenceDataPackIds.DesignConcepts => ToolIds.DesignConceptDictionary,
            _ => throw new ArgumentOutOfRangeException(nameof(packId))
        };
        return _paths.GetToolDataDirectory(toolId);
    }

    public async Task<ReferenceDataPackState?> ReadAsync(string packId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(GetPackDirectory(packId), "active-pack.json");
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var state = await JsonSerializer.DeserializeAsync<ReferenceDataPackState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (state is null || !string.Equals(state.PackId, packId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(state.ArchiveFileName)) return null;
            return state;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            AppLogger.Default.Warning(nameof(DataPackStateStore), "pack_state_read_failed", exception.Message);
            return null;
        }
    }

    public async Task WriteAsync(string packId, ReferenceDataPackState state, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(packId, state.PackId, StringComparison.Ordinal)) throw new InvalidDataException("Data-pack state ID mismatch.");
        var path = Path.Combine(GetPackDirectory(packId), "active-pack.json");
        var temp = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
