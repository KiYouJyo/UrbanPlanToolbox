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
    public void UpdateResourcesContainTheDialogTitleAndProgressTextInAllLanguages()
    {
        foreach (var language in ReswCatalog.Languages)
        {
            var resources = ReswCatalog.Load(language);
            Assert.False(resources["About_UpdateTitle.Text"].StartsWith("!", StringComparison.Ordinal));
            Assert.False(resources["Update_ProgressPercent"].StartsWith("!", StringComparison.Ordinal));
            Assert.True(resources.ContainsKey("Update_State_Downloading"));
            Assert.True(resources.ContainsKey("Update_State_Installing"));
            Assert.True(resources.ContainsKey("Update_State_Failed"));
        }
    }
}
