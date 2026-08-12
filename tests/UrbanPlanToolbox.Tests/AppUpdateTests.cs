using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class AppUpdateTests
{
    [Theory]
    [InlineData(true, false, DistributionChannel.Store)]
    [InlineData(true, true, DistributionChannel.Store)]
    [InlineData(false, true, DistributionChannel.GitHub)]
    [InlineData(false, false, DistributionChannel.Development)]
    public void BuildChannelDecisionUsesBuildChannelBeforePackageAvailability(bool storeBuild, bool packageAvailable, DistributionChannel expected) =>
        Assert.Equal(expected, DistributionChannelDecision.ForBuild(storeBuild, packageAvailable));

    [Theory]
    [InlineData("JoKiy.UrbanPlanToolbox", "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8", StoreIdentityValidationResult.Valid)]
    [InlineData("Other.Name", "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8", StoreIdentityValidationResult.NameMismatch)]
    [InlineData("JoKiy.UrbanPlanToolbox", "CN=Other", StoreIdentityValidationResult.PublisherMismatch)]
    [InlineData(null, null, StoreIdentityValidationResult.PackageUnavailable)]
    public void StoreIdentityValidationUsesOnlyNameAndPublisher(string? name, string? publisher, StoreIdentityValidationResult expected) =>
        Assert.Equal(expected, DistributionChannelIdentity.ValidateStoreIdentity(name, publisher));

    [Theory]
    [InlineData(DistributionChannel.Store, AppUpdateProviderKind.Store)]
    [InlineData(DistributionChannel.GitHub, AppUpdateProviderKind.GitHub)]
    [InlineData(DistributionChannel.Development, AppUpdateProviderKind.Development)]
    public void ProviderDecisionMatchesDistributionChannel(DistributionChannel channel, AppUpdateProviderKind expected) =>
        Assert.Equal(expected, AppUpdateProviderDecision.ForChannel(channel));

    [Theory]
    [InlineData(DistributionChannel.GitHub, "About_ChannelGitHub", true, false, true)]
    [InlineData(DistributionChannel.Store, "About_ChannelStore", true, true, false)]
    [InlineData(DistributionChannel.Development, "About_ChannelDevelopment", false, false, false)]
    public void DistributionContextExposesChannelCapabilities(DistributionChannel channel, string resourceKey, bool canCheck, bool canInstall, bool canOpenReleases)
    {
        var context = DistributionChannelContext.For(channel);

        Assert.Equal(resourceKey, context.DisplayResourceKey);
        Assert.Equal(canCheck, context.CanCheckForUpdates);
        Assert.Equal(canInstall, context.CanSelfUpdate);
        Assert.Equal(canOpenReleases, context.CanOpenReleases);
    }

    [Fact]
    public async Task DevelopmentUpdateServiceNeverUsesReleaseChannels()
    {
        var service = new DevelopmentAppUpdateService();

        Assert.Equal(AppUpdateState.UnsupportedChannel, (await service.CheckForUpdatesAsync()).State);
        Assert.Equal(AppUpdateState.UnsupportedChannel, (await service.DownloadAndInstallAsync()).State);
    }

    [Theory]
    [InlineData(FakeAppUpdateScenario.UpToDate, AppUpdateState.UpToDate)]
    [InlineData(FakeAppUpdateScenario.UpdateAvailable, AppUpdateState.UpdateAvailable)]
    [InlineData(FakeAppUpdateScenario.UnsupportedChannel, AppUpdateState.UnsupportedChannel)]
    [InlineData(FakeAppUpdateScenario.NetworkError, AppUpdateState.Failed)]
    [InlineData(FakeAppUpdateScenario.StoreUnavailable, AppUpdateState.Failed)]
    public async Task FakeServiceCoversCheckScenarios(FakeAppUpdateScenario scenario, AppUpdateState expected) => Assert.Equal(expected, (await new FakeAppUpdateService(scenario).CheckForUpdatesAsync()).State);

    [Theory]
    [InlineData(FakeAppUpdateScenario.Cancelled, AppUpdateState.Cancelled)]
    [InlineData(FakeAppUpdateScenario.DownloadFailed, AppUpdateState.Failed)]
    [InlineData(FakeAppUpdateScenario.InstallFailed, AppUpdateState.Failed)]
    [InlineData(FakeAppUpdateScenario.InstallWillCloseApp, AppUpdateState.Installing)]
    public async Task FakeServiceCoversInstallScenarios(FakeAppUpdateScenario scenario, AppUpdateState expected)
    {
        var service = new FakeAppUpdateService(scenario); await service.CheckForUpdatesAsync();
        Assert.Equal(expected, (await service.DownloadAndInstallAsync()).State);
    }

    [Fact]
    public async Task ViewModelTransitionsAndPreventsDuplicateChecks()
    {
        var viewModel = new UpdateViewModel(new FakeAppUpdateService(FakeAppUpdateScenario.UpdateAvailable));
        Assert.True(viewModel.CanCheck); Assert.False(viewModel.CanInstall);
        await Task.WhenAll(viewModel.CheckAsync(), viewModel.CheckAsync());
        Assert.Equal(AppUpdateState.UpdateAvailable, viewModel.Info.State); Assert.True(viewModel.CanInstall);
    }

    [Fact]
    public async Task ViewModelReportsProgressAndCompletion()
    {
        var viewModel = new UpdateViewModel(new FakeAppUpdateService(FakeAppUpdateScenario.UpdateAvailable));
        await viewModel.CheckAsync(); await viewModel.DownloadAndInstallAsync();
        Assert.Equal(AppUpdateState.Completed, viewModel.Info.State); Assert.Null(viewModel.Progress);
    }

    [Theory]
    [InlineData("NetworkError", "Update_ErrorNetwork")]
    [InlineData("DownloadFailed", "Update_ErrorDownload")]
    [InlineData("InstallFailed", "Update_ErrorInstall")]
    [InlineData("0x80004005", "Update_ErrorStoreCode")]
    public void ErrorCodesMapToLocalizedResourceKeys(string code, string key) => Assert.Equal(key, AppUpdateErrorMapper.ToResourceKey(code));

    [Fact]
    public void ProgressValuesAreSafeForTheProgressBar()
    {
        Assert.Null(AppUpdateProgress.NormalizeValue(null));
        Assert.Null(AppUpdateProgress.NormalizeValue(double.NaN));
        Assert.Equal(0, AppUpdateProgress.NormalizeValue(-1));
        Assert.Equal(0.53, AppUpdateProgress.NormalizeValue(0.53));
        Assert.Equal(1, AppUpdateProgress.NormalizeValue(2));
    }

    [Fact]
    public async Task ViewModelRetainsPendingDownloadProgressWhenNextCallbackHasNoValue()
    {
        var viewModel = new UpdateViewModel(new PendingProgressService());
        var observed = new List<double?>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.Progress)) observed.Add(viewModel.Progress);
        };

        await viewModel.CheckAsync();
        await viewModel.DownloadAndInstallAsync();

        Assert.Contains(0.15, observed);
        Assert.DoesNotContain(0d, observed);
    }

    [Theory]
    [InlineData(0.15, 0, 0, 0, 0.15)]
    [InlineData(double.NaN, 0.15, 0, 0, 0.1875)]
    [InlineData(double.NaN, double.NaN, 15, 100, 0.15)]
    [InlineData(0.30, 0.15, 15, 100, 0.30)]
    public void StoreProgressUsesValidOverallPackageOrByteProgress(double total, double package, ulong bytes, ulong size, double expected) =>
        Assert.True(Math.Abs(expected - StoreUpdateProgressResolver.ResolveDownloadProgress(total, package, bytes, size).Value!.Value) < 1e-12);

    [Fact]
    public void StoreProgressDoesNotInventProgressWhenAllSourcesAreInvalid() =>
        Assert.Null(StoreUpdateProgressResolver.ResolveDownloadProgress(double.NaN, double.NaN, 0, 0).Value);

    [Fact]
    public void StoreProgressMappingUsesExplicitSourcesAndCombinedApiPackageScale()
    {
        var total = StoreUpdateProgressResolver.ResolveDownloadProgress(0.15, 0.40, 0, 0);
        var package = StoreUpdateProgressResolver.ResolveDownloadProgress(double.NaN, 0.40, 0, 0);
        var bytes = StoreUpdateProgressResolver.ResolveDownloadProgress(double.NaN, double.NaN, 360900, 1110000);

        Assert.Equal("Total", total.Source);
        Assert.Equal(0.15, total.Value);
        Assert.Equal("PackageNormalized", package.Source);
        Assert.Equal(0.5, package.Value);
        Assert.Equal("Bytes", bytes.Source);
        Assert.Equal(360900d / 1110000d, bytes.Value);
    }

    [Fact]
    public void StoreProgressBridgeUsesSingleAsTaskProgressSubscription()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Services", "StoreAppUpdateService.cs"));
        Assert.DoesNotContain("operation.Progress", source, StringComparison.Ordinal);
        Assert.Contains("AsTask(cancellationToken, storeProgress)", source, StringComparison.Ordinal);
        Assert.Contains("HandleStoreProgress(status, progress)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreUpdateIsNotBoundToAboutPageUnloadToken()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.Contains("_updates.DownloadAndInstallAsync(_updateLifetime.Token)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_updates.DownloadAndInstallAsync(_pageLifetime.Token)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreProgressDiagnosticsContainVersionAndSourceFields()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Services", "StoreAppUpdateService.cs"));
        foreach (var field in new[] { "AppVersion=", "StoreState=", "PackageDownloadProgress=", "TotalDownloadProgress=", "PackageBytesDownloaded=", "PackageDownloadSizeInBytes=", "MappedAppState=", "MappedUiProgress=", "ProgressSource=" })
            Assert.Contains(field, source, StringComparison.Ordinal);
    }

    [Fact]
    public void DottedPropertyKeysUseMrtCorePropertyPath() {
        Assert.Equal("About_UpdateTitle/Text", MrtResourceKeyNormalizer.Normalize("About_UpdateTitle.Text"));
        Assert.Equal("Setting_Language_System/Content", MrtResourceKeyNormalizer.Normalize("Setting_Language_System.Content"));
        Assert.Equal("Action_DownloadAndInstall", MrtResourceKeyNormalizer.Normalize("Action_DownloadAndInstall"));
    }

    [Fact]
    public void UpdateResourcesContainTheDialogTitleAndProgressTextInAllLanguages()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            Assert.False(resources["About_UpdateTitle.Text"].StartsWith("!", StringComparison.Ordinal));
            Assert.False(resources["Dialog_UpdateAvailableTitle"].StartsWith("!", StringComparison.Ordinal));
            Assert.False(resources["Update_ProgressPercent"].StartsWith("!", StringComparison.Ordinal));
            Assert.True(resources.ContainsKey("Update_State_Downloading"));
            Assert.True(resources.ContainsKey("Update_State_Installing"));
            Assert.True(resources.ContainsKey("Update_State_Failed"));
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

internal sealed class PendingProgressService : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, "1.5.4"));

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new(AppUpdateState.Downloading, 0.15));
        progress?.Report(new(AppUpdateState.Downloading));
        await Task.Delay(25, cancellationToken);
        progress?.Report(new(AppUpdateState.Installing));
        return new AppUpdateResult(AppUpdateState.Installing);
    }
}
