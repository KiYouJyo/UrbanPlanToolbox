using System.Text.RegularExpressions;

namespace UrbanPlanToolbox.Services;

public static partial class VersionParser
{
    [GeneratedRegex("^[vV]?(?<major>\\d+)\\.(?<minor>\\d+)\\.(?<build>\\d+)(?:\\.(?<revision>\\d+))?(?:[-+].*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var match = TagPattern().Match(tag.Trim());
        if (!match.Success || !int.TryParse(match.Groups["major"].Value, out var major) || !int.TryParse(match.Groups["minor"].Value, out var minor) || !int.TryParse(match.Groups["build"].Value, out var build)) return false;
        var revision = 0;
        if (match.Groups["revision"].Success && !int.TryParse(match.Groups["revision"].Value, out revision)) return false;
        try { version = new Version(major, minor, build, revision); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}
