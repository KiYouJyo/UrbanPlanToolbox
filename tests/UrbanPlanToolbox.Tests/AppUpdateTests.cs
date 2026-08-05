using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class AppUpdateTests
{
    [Theory]
    [InlineData("JoKiy.UrbanPlanToolbox", "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8", "c4e4b33a7b774121897c7d720a5471f8", DistributionChannel.Store)]
    [InlineData("556F80C5-C4D4-452B-93B4-00DE3FA7AC29", "CN=AppPublisher", "00000000000000000000000000000000", DistributionChannel.GitHub)]
    [InlineData("JoKiy.UrbanPlanToolbox", "CN=AppPublisher", "c4e4b33a7b774121897c7d720a5471f8", DistributionChannel.GitHub)]
    [InlineData("JoKiy.UrbanPlanToolbox", "CN=C4E4B33A-7B77-4121-897C-7D720A5471F8", "", DistributionChannel.GitHub)]
    public void PackageIdentityMustMatchStoreIdentityExactly(string name, string publisher, string publisherId, DistributionChannel expected) =>
        Assert.Equal(expected, DistributionChannelIdentity.Identify(name, publisher, publisherId));

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
        Assert.Equal(AppUpdateState.Completed, viewModel.Info.State); Assert.Equal(1, viewModel.Progress);
    }

    [Theory]
    [InlineData("NetworkError", "Update_ErrorNetwork")]
    [InlineData("DownloadFailed", "Update_ErrorDownload")]
    [InlineData("InstallFailed", "Update_ErrorInstall")]
    [InlineData("0x80004005", "Update_ErrorStoreCode")]
    public void ErrorCodesMapToLocalizedResourceKeys(string code, string key) => Assert.Equal(key, AppUpdateErrorMapper.ToResourceKey(code));
}
