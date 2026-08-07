using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public static class DiagnosticsInfoService
{
    public static string Create(string? errorSummary = null)
    {
        var settings = new SettingsService().Load();
        var sdk = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name?.Contains("WindowsApp", StringComparison.OrdinalIgnoreCase) == true)
            ?.GetName().Version?.ToString() ?? "Unavailable";
        return string.Join(Environment.NewLine,
            "Application: UrbanPlanToolbox",
            $"Display version: {AppVersionProvider.DisplayVersion}",
            $"Package version: {AppVersionProvider.GetPackageVersion()}",
            $"Channel: {DistributionChannelProvider.CurrentContext.Channel}",
            $"Architecture: {RuntimeInformation.OSArchitecture}",
            $"Windows version: {Environment.OSVersion.Version}",
            $"Windows App SDK version: {sdk}",
            $"Language: {LanguagePreference.Normalize(settings.Language)}",
            $"Theme: {SettingsService.NormalizeTheme(settings.Theme)}",
            $"Data schema version: {AppVersionProvider.DataSchemaVersion}",
            "Backup format version: 2",
            $"Package identity: {GetPackageIdentity()}",
            "Data handling: local-only diagnostics; no telemetry or project contents",
            $"Error summary: {Sanitize(errorSummary) ?? "None"}");
    }

    public static string GetChannelLabel(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => "Microsoft Store",
        DistributionChannel.GitHub => "GitHub sideload",
        _ => "Development"
    };

    private static string GetPackageIdentity()
    {
        try
        {
            var id = Package.Current.Id;
            return $"family={Sanitize(id.FamilyName) ?? "Unavailable"}; fullName={Sanitize(id.FullName) ?? "Unavailable"}";
        }
        catch { return "Unpackaged"; }
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
        text = Regex.Replace(text, @"(?i)(bearer\s+|token|password|secret|api[-_]?key)\s*[:=]\s*[^\s,;]+", "$1[redacted]", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(?i)([A-Za-z]:\\|\\\\)[^\s]+", "[path]", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(?i)(user(name)?|account)\s*[:=]\s*[^\s,;]+", "$1=[redacted]", RegexOptions.CultureInvariant);
        return text.Length <= 240 ? text : text[..240];
    }
}
