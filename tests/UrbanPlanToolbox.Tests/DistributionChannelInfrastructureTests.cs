using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class DistributionChannelInfrastructureTests
{
    [Fact]
    public void StoreIdentityDoesNotUseDerivedPublisherId()
    {
        var root = FindRepositoryRoot();
        var identity = File.ReadAllText(Path.Combine(root, "Services", "DistributionChannelIdentity.cs"));
        var channelService = File.ReadAllText(Path.Combine(root, "Services", "AppDistributionChannelService.cs"));

        Assert.DoesNotContain("c4e4b33a7b774121897c7d720a5471f8", identity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorePublisherId", identity, StringComparison.Ordinal);
        Assert.Contains("#if URBANPLANTOOLBOX_STORE", channelService);
        Assert.Contains("Package identity is diagnostic only", channelService);
    }

    [Fact]
    public void StoreBuildUsesStoreProviderAndHasNoGitHubFallback()
    {
        var root = FindRepositoryRoot();
        var factory = File.ReadAllText(Path.Combine(root, "Services", "AppUpdateServiceFactory.cs"));
        var decision = File.ReadAllText(Path.Combine(root, "Services", "DistributionChannel.cs"));
        var storeService = File.ReadAllText(Path.Combine(root, "Services", "StoreAppUpdateService.cs"));

        Assert.Contains("DistributionChannel.Store => AppUpdateProviderKind.Store", decision);
        Assert.Contains("DistributionChannel.Store => new StoreAppUpdateService", factory);
        Assert.DoesNotContain("StoreCheckFailed.*GitHub", storeService, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitHubAppUpdateService", storeService, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubUpdaterDeploysVerifiedLocalBundleWithoutBrowserFallback()
    {
        var root = FindRepositoryRoot();
        var updater = File.ReadAllText(Path.Combine(root, "Services", "GitHubAppUpdateService.cs"));
        var releaseService = File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs"));

        Assert.Contains("DownloadAndVerifyBundleAsync", releaseService, StringComparison.Ordinal);
        Assert.Contains("_updateService.DownloadAndVerifyBundleAsync", updater, StringComparison.Ordinal);
        Assert.Contains("PackageManager", updater, StringComparison.Ordinal);
        Assert.Contains("AddPackageAsync", updater, StringComparison.Ordinal);
        Assert.Contains("ForceApplicationShutdown", updater, StringComparison.Ordinal);
        Assert.Contains("ApplicationRestartRegistration.Register", updater, StringComparison.Ordinal);
        Assert.Contains("PackageDeploymentCompleted", updater, StringComparison.Ordinal);
        Assert.DoesNotContain("RepositoryLinks.AppInstaller", updater, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalLinkService.OpenAsync", updater, StringComparison.Ordinal);
        Assert.DoesNotContain("Launcher.LaunchFileAsync", updater, StringComparison.Ordinal);
        Assert.DoesNotContain("ms-appinstaller:", updater, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateInstallSource.GitHub", updater, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutPageDoesNotCreateAnUpdateConfirmationDialog()
    {
        var root = FindRepositoryRoot();
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.DoesNotContain("ShowUpdateDialogAsync", about, StringComparison.Ordinal);
        Assert.DoesNotContain("Update_DialogInstall", about, StringComparison.Ordinal);
        Assert.Contains("Action_DownloadAndInstall", about, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingKeepsTheTwoManifestsAndStoreBuildFlagSeparate()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "UrbanPlanToolbox.csproj"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));
        var script = File.ReadAllText(Path.Combine(root, "packaging", "Build-StorePackage.ps1"));

        Assert.Contains("URBANPLANTOOLBOX_STORE", project);
        Assert.Contains("Package.Store.appxmanifest", project);
        Assert.Contains("DistributionChannel=Store", script);
        Assert.Contains("Package.Store.appxmanifest", script);
        Assert.Contains("Name=\"JoKiy.UrbanPlanToolbox\"", storeManifest);
        Assert.Contains("Publisher=\"CN=C4E4B33A-7B77-4121-897C-7D720A5471F8\"", storeManifest);
        Assert.DoesNotContain("JoKiy.UrbanPlanToolbox", githubManifest, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
