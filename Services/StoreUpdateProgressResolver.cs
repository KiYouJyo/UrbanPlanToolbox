using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

/// <summary>Pure progress rules shared by the Store adapter and regression tests.</summary>
public static class StoreUpdateProgressResolver
{
    public sealed record Resolution(double? Value, string Source);

    public static Resolution ResolveDownloadProgress(double totalProgress, double packageProgress, ulong bytesDownloaded, ulong downloadSize)
    {
        var total = Normalize(totalProgress);
        var package = Normalize(packageProgress);
        if (total is double totalValue) return new(totalValue, "Total");
        if (package is double packageValue) return new(AppUpdateProgress.NormalizeValue(packageValue / 0.8d), "PackageNormalized");
        if (downloadSize > 0) return new(AppUpdateProgress.NormalizeValue((double)bytesDownloaded / downloadSize), "Bytes");
        return new(null, "None");
    }

    private static double? Normalize(double value) => double.IsFinite(value) && value is >= 0d and <= 1d ? value : null;
}
