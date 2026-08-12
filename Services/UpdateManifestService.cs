using System.Net.Http.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class UpdateManifestService
{
    public static UpdateManifestService Default { get; } = new();
    private static readonly Uri ManifestUri = new("https://kiyoujyo.github.io/UrbanPlanToolbox/update-manifest.json");
    private readonly HttpClient _client;

    public UpdateManifestService(HttpClient? client = null) => _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<string?> GetVersionAsync(DistributionChannel channel, CancellationToken cancellationToken = default)
    {
        try
        {
            var manifest = await _client.GetFromJsonAsync<UpdateManifest>(ManifestUri, cancellationToken);
            var version = manifest?.SchemaVersion == 1 ? manifest.VersionFor(channel) : null;
            return VersionParser.TryParseTag(version ?? string.Empty, out var parsed)
                ? $"{parsed.Major}.{parsed.Minor}.{parsed.Build}"
                : null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) { return null; }
    }
}
