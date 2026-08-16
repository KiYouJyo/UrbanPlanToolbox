using System.Text;
using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class WebDavProfileService
{
    private readonly IAppDataPathProvider _paths;
    public static WebDavProfileService Default { get; } = new(AppDataPathProvider.Default);

    public WebDavProfileService(IAppDataPathProvider paths) => _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public string ProfilePath => Path.Combine(_paths.Paths.RootDirectory, "webdav-profile.json");

    public async Task<WebDavProfile?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(ProfilePath)) return null;
            var json = await File.ReadAllTextAsync(ProfilePath, cancellationToken).ConfigureAwait(false);
            var stored = JsonSerializer.Deserialize<WebDavProfile>(json, DataStorageJson.Options);
            if (stored is null || !TryNormalize(stored, out var normalized, out _)) return null;
            return normalized with { LastBackupAtUtc = stored.LastBackupAtUtc?.ToUniversalTime() };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task<bool> SaveAsync(WebDavProfile profile, CancellationToken cancellationToken = default)
    {
        if (!TryNormalize(profile, out var normalized, out _)) return false;
        var tempPath = ProfilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(_paths.Paths.RootDirectory);
            var json = JsonSerializer.Serialize(normalized, DataStorageJson.Options);
            await File.WriteAllTextAsync(tempPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, ProfilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return false;
        }
    }

    public async Task<bool> UpdateLastBackupAsync(DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
    {
        var profile = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return profile is not null && await SaveAsync(profile with { LastBackupAtUtc = timestampUtc.ToUniversalTime() }, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync()
    {
        TryDelete(ProfilePath);
        TryDelete(ProfilePath + ".tmp");
        return Task.CompletedTask;
    }

    public static bool TryNormalize(WebDavProfile profile, out WebDavProfile normalized, out string? errorCode)
    {
        normalized = profile;
        errorCode = null;
        if (!Uri.TryCreate(profile.ServerUrl?.Trim(), UriKind.Absolute, out var serverUri) ||
            serverUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(serverUri.UserInfo) ||
            !string.IsNullOrEmpty(serverUri.Query) ||
            !string.IsNullOrEmpty(serverUri.Fragment))
        {
            errorCode = "ServerUrlInvalid";
            return false;
        }

        var username = profile.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            errorCode = "UsernameRequired";
            return false;
        }

        var remotePath = profile.RemotePath?.Trim();
        if (string.IsNullOrWhiteSpace(remotePath) || remotePath.Contains('\\') || remotePath.Any(char.IsControl))
        {
            errorCode = "RemotePathInvalid";
            return false;
        }

        var segments = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Length > 128))
        {
            errorCode = "RemotePathInvalid";
            return false;
        }

        var serverUrl = serverUri.AbsoluteUri.EndsWith('/', StringComparison.Ordinal) ? serverUri.AbsoluteUri : serverUri.AbsoluteUri + "/";
        normalized = new WebDavProfile
        {
            ServerUrl = serverUrl,
            Username = username,
            RemotePath = "/" + string.Join('/', segments),
            LastBackupAtUtc = profile.LastBackupAtUtc?.ToUniversalTime()
        };
        return true;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
