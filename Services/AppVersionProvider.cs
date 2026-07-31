using Windows.ApplicationModel;

namespace UrbanPlanToolbox.Services;

public static class AppVersionProvider
{
    public static Version GetCurrentVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return new Version((int)version.Major, (int)version.Minor, (int)version.Build, (int)version.Revision);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            return VersionParser.Normalize(typeof(AppVersionProvider).Assembly.GetName().Version ?? new Version(0, 3, 3, 0));
        }
    }
}
