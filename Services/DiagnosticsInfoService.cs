using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace UrbanPlanToolbox.Services;

public static class DiagnosticsInfoService
{
    public static string Create(string? errorSummary = null)
    {
        var settings = new SettingsService().Load();
        var channelService = new AppDistributionChannelService();
        var channel = channelService.GetCurrentChannel();
        var identity = channelService.GetPackageIdentity();
        var sdk = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name?.Contains("WindowsApp", StringComparison.OrdinalIgnoreCase) == true)
            ?.GetName().Version?.ToString() ?? "Unavailable";
        return string.Join(Environment.NewLine,
            "Application: UrbanPlanToolbox",
            $"Display version: {AppVersionProvider.DisplayVersion}",
            $"Package version: {AppVersionProvider.GetPackageVersion()}",
            $"Distribution channel: {GetChannelLabel(channel)}",
            $"Build channel: {GetBuildChannelLabel(channel)}",
            $"Update provider: {AppUpdateProviderDecision.ForChannel(channel)}",
            $"Architecture: {RuntimeInformation.OSArchitecture}",
            $"Windows version: {Environment.OSVersion.Version}",
            $"Windows App SDK version: {sdk}",
            $"Language: {LanguagePreference.Normalize(settings.Language)}",
            $"Theme: {SettingsService.NormalizeTheme(settings.Theme)}",
            $"Data schema version: {AppVersionProvider.DataSchemaVersion}",
            "Backup format version: 2",
            $"Package identity name: {Sanitize(identity.Name) ?? "Unavailable / Development"}",
            $"Package publisher: {Sanitize(identity.Publisher) ?? "Unavailable / Development"}",
            $"Package publisher ID: {Sanitize(identity.PublisherId) ?? "Unavailable / Development"}",
            $"Package family name: {Sanitize(identity.FamilyName) ?? "Unavailable / Development"}",
            $"Package full name: {Sanitize(identity.FullName) ?? "Unavailable / Development"}",
            $"Store identity validation: {GetStoreValidationLabel(channelService, channel)}",
            "Data handling: local-only diagnostics; no telemetry or project contents",
            $"Error summary: {Sanitize(errorSummary) ?? "None"}");
    }

    public static string GetChannelLabel(DistributionChannel channel) => channel switch
    {
        DistributionChannel.Store => "Microsoft Store",
        DistributionChannel.GitHub => "GitHub sideload",
        _ => "Development"
    };

    private static string GetBuildChannelLabel(DistributionChannel channel) => channel switch
        {
            DistributionChannel.Store => "Store",
            DistributionChannel.GitHub => "GitHub",
            _ => "Development"
        };

    private static string GetStoreValidationLabel(AppDistributionChannelService service, DistributionChannel channel) =>
        channel == DistributionChannel.Development ? "Unavailable / Development" : service.GetStoreIdentityValidation().ToString();

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
