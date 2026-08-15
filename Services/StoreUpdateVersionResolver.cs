namespace UrbanPlanToolbox.Services;

/// <summary>
/// Resolves display-only Store update metadata without allowing stale hosted metadata or the
/// currently installed package version to masquerade as the target update version.
/// </summary>
public static class StoreUpdateVersionResolver
{
    public static string? Resolve(string installedVersion, IEnumerable<string?> packageVersionCandidates, string? hostedManifestVersion)
    {
        if (!VersionParser.TryParseTag(installedVersion, out var installed)) return null;

        Version? best = null;
        foreach (var candidate in packageVersionCandidates.Append(hostedManifestVersion))
        {
            if (!VersionParser.TryParseTag(candidate, out var parsed) || parsed <= installed) continue;
            if (best is null || parsed > best) best = parsed;
        }

        return best is null ? null : $"{best.Major}.{best.Minor}.{best.Build}";
    }
}
