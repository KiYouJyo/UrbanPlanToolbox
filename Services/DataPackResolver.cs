using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class DataPackResolver
{
    private readonly DataPackStateStore _stateStore;
    private readonly DataPackInstaller _installer;

    internal DataPackResolver(DataPackStateStore stateStore, DataPackInstaller installer)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
    }

    public Task<ReferenceDataPackState?> GetActiveStateAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        return _stateStore.ReadAsync(packId, cancellationToken);
    }

    public async Task<ReferenceDataPackContent?> ResolveActiveAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        var state = await _stateStore.ReadAsync(packId, cancellationToken).ConfigureAwait(false);
        if (state is null) return null;
        var archivePath = Path.Combine(_stateStore.GetPackDirectory(packId), state.ArchiveFileName);
        if (!File.Exists(archivePath)) return null;
        var validated = await _installer.ValidateArchiveAsync(packId, archivePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(validated.Manifest.Version, state.Version, StringComparison.Ordinal) || validated.Manifest.SchemaVersion != state.SchemaVersion)
            throw new InvalidDataException("The active data-pack state does not match its archive.");
        return new ReferenceDataPackContent(validated.Manifest, state, validated.DataJson, archivePath);
    }

    public async Task<IReadOnlyList<ReferenceDataPackState>> GetInstalledVersionsAsync(string packId, CancellationToken cancellationToken = default)
    {
        ReferenceDataPackService.ValidatePackId(packId);
        var directory = _stateStore.GetPackDirectory(packId);
        var states = new List<ReferenceDataPackState>();
        foreach (var archivePath in Directory.EnumerateFiles(directory, "*.uptdata", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var validated = await _installer.ValidateArchiveAsync(packId, archivePath, cancellationToken).ConfigureAwait(false);
                states.Add(new ReferenceDataPackState
                {
                    PackId = packId,
                    Version = validated.Manifest.Version,
                    SchemaVersion = validated.Manifest.SchemaVersion,
                    ArchiveFileName = Path.GetFileName(archivePath),
                    SourceKind = "installed",
                    InstalledAt = File.GetLastWriteTimeUtc(archivePath)
                });
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException)
            {
                AppLogger.Default.Warning(nameof(DataPackResolver), "installed_pack_skipped", $"{Path.GetFileName(archivePath)}: {exception.Message}");
            }
        }
        return states.OrderByDescending(state => ReferenceDataPackService.ParseDataVersion(state.Version)).ThenByDescending(state => state.InstalledAt).ToArray();
    }

    public async Task<bool> RollbackAsync(string packId, CancellationToken cancellationToken = default)
    {
        var current = await _stateStore.ReadAsync(packId, cancellationToken).ConfigureAwait(false);
        if (current is null) return false;
        var currentVersion = ReferenceDataPackService.ParseDataVersion(current.Version);
        var previous = (await GetInstalledVersionsAsync(packId, cancellationToken).ConfigureAwait(false))
            .Where(state => ReferenceDataPackService.ParseDataVersion(state.Version).CompareTo(currentVersion) < 0)
            .OrderByDescending(state => ReferenceDataPackService.ParseDataVersion(state.Version))
            .ThenByDescending(state => state.InstalledAt)
            .FirstOrDefault();
        if (previous is null) return false;
        await _stateStore.WriteAsync(packId, previous with { SourceKind = "rollback", InstalledAt = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
