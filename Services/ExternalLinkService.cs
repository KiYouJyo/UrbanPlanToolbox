using Windows.System;

namespace UrbanPlanToolbox.Services;

public static class ExternalLinkService
{
    public static bool IsSafeHttpUri(string? value, out Uri? uri)
    {
        uri = null;
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) &&
               parsed.Scheme is "http" or "https" &&
               (uri = parsed) is not null;
    }

    public static Task<bool> OpenAsync(string? value) =>
        IsSafeHttpUri(value, out var uri)
            ? Launcher.LaunchUriAsync(uri).AsTask()
            : Task.FromResult(false);
}
