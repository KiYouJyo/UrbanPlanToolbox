using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public static class AppVersionProvider
{
    public const string Version = "1.6.8";
    public const string DisplayVersion = "v1.6.8";
    public const int DataSchemaVersion = 1;

    public static Version GetCurrentVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return new Version((int)version.Major, (int)version.Minor, (int)version.Build, (int)version.Revision);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            return VersionParser.Normalize(typeof(AppVersionProvider).Assembly.GetName().Version ?? new Version(0, 5, 0, 0));
        }
    }

    public static string GetPackageVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch (Exception) when (OperatingSystem.IsWindows()) { return "Unavailable"; }
    }
}
