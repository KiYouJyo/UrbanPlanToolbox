using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public static class AppVersionProvider
{
    /// <summary>User-facing version text shared by About and release metadata.</summary>
    public const string DisplayVersion = "0.3.11";

    public static Version GetCurrentVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return new Version((int)version.Major, (int)version.Minor, (int)version.Build, (int)version.Revision);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            return VersionParser.Normalize(typeof(AppVersionProvider).Assembly.GetName().Version ?? new Version(0, 3, 11, 0));
        }
    }
}
