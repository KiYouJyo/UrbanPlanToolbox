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
    [InlineData(DistributionChannel.GitHub, "About_ChannelGitHub", true, true, false)]
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
        var viewModel = new UpdateViewModel(new FakeAppUpdateService(FakeAppUpdateScenario.UpdateAvailable), new UpdateRestartStub(true));
        await viewModel.CheckAsync(); await viewModel.DownloadAndInstallAsync();
        Assert.Equal(AppUpdateState.Completed, viewModel.Info.State); Assert.Null(viewModel.Progress);
    }

    [Theory]
    [InlineData(AppUpdateState.Completed, true)]
    [InlineData(AppUpdateState.Restarting, true)]
    [InlineData(AppUpdateState.Failed, false)]
    [InlineData(AppUpdateState.Cancelled, false)]
    [InlineData(AppUpdateState.UpToDate, false)]
    public async Task UpdateCompletionDoesNotRequestAnAppOwnedRestart(AppUpdateState resultState, bool _)
    {
        var restart = new UpdateRestartStub(true);
        var viewModel = new UpdateViewModel(new InstallResultUpdateService(resultState), restart);
        await viewModel.CheckAsync(); await viewModel.DownloadAndInstallAsync();
        Assert.Equal(0, restart.CallCount);
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

    [Theory]
    [InlineData(AppUpdateState.NotChecked, false)]
    [InlineData(AppUpdateState.UpdateAvailable, true)]
    public void UpdateInfoOnlyEnablesInstallForAnAvailableUpdate(AppUpdateState state, bool expected) =>
        Assert.Equal(expected, new AppUpdateInfo(state).IsUpdateAvailable);

    [Fact]
    public void GitHubChannelSupportsInAppInstallAndDoesNotOpenReleasesByDefault()
    {
        var context = DistributionChannelContext.For(DistributionChannel.GitHub);
        Assert.True(context.CanSelfUpdate);
        Assert.False(context.CanOpenReleases);
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

    [Fact]
    public async Task ViewModelKeepsReleaseMetadataAndRequiresSecondActionAfterDownload()
    {
        var service = new ReadyToInstallUpdateService();
        var viewModel = new UpdateViewModel(service);

        await viewModel.CheckAsync();
        await viewModel.DownloadAndInstallAsync();

        Assert.Equal(AppUpdateState.ReadyToInstall, viewModel.Info.State);
        Assert.Equal("1.6.1", viewModel.Info.AvailableVersion);
        Assert.True(viewModel.CanInstall);
        Assert.Equal(1, service.DownloadCalls);
        Assert.Equal(0, service.InstallCalls);

        await viewModel.DownloadAndInstallAsync();

        Assert.Equal(AppUpdateState.Restarting, viewModel.Info.State);
        Assert.Equal("1.6.1", viewModel.Info.AvailableVersion);
        Assert.Equal(1, service.InstallCalls);
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
    public void AboutPageUsesOneConfirmationDialogAndNoInlineInstallControls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.DoesNotContain("ShowUpdateDialogAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Update_DialogInstall", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InstallUpdateButton", source, StringComparison.Ordinal);
        Assert.Contains("UpdateProgressBar", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutPageUsesNativeProgressRingsOnlyWhileChecking()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("x:Name=\"UpdateTargetProgressRing\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UpdateNotesProgressRing\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ProgressRing", xaml, StringComparison.Ordinal);
        Assert.Contains("var checking = info.State == AppUpdateState.Checking", code, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetProgressRing.IsActive = checking", code, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesProgressRing.IsActive = checking", code, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetProgressRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed", code, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesProgressRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReleaseNotesRequestUsesAbsoluteGitHubPagesUriAndRejectsVersionMismatch()
    {
        var handler = new ReleaseNotesHandler();
        var service = new LocalizedReleaseNotesService(new HttpClient(handler));
        var notes = await service.GetAsync("1.5.9", "en-US");
        Assert.NotNull(notes);
        Assert.Equal("https://kiyoujyo.github.io/UrbanPlanToolbox/release-notes/1.5.9.json", handler.RequestUri?.ToString());

        handler.Payload = "{\"schemaVersion\":1,\"version\":\"1.5.8\",\"notes\":{\"en-US\":{\"title\":\"x\",\"items\":[\"y\"]}}}";
        Assert.Null(await service.GetAsync("1.5.9", "en-US"));
    }

    [Fact]
    public async Task GitHubUpToDateRetainsValidatedRemoteVersion()
    {
        var localVersion = AppVersionProvider.GetCurrentVersion();
        var expectedVersion = $"{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}";
        var service = new GitHubAppUpdateService(new GitHubUpdateService(new HttpClient(new GitHubReleaseHandler(expectedVersion))));

        var info = await service.CheckForUpdatesAsync();

        Assert.Equal(AppUpdateState.UpToDate, info.State);
        Assert.Equal(expectedVersion, info.AvailableVersion);
        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task UpToDateVersionLoadsLocalizedReleaseNotesWithoutEnablingInstall()
    {
        var viewModel = new UpdateViewModel(new FixedUpdateService(AppUpdateState.UpToDate, "1.5.9"));
        var notes = new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler()));

        await viewModel.CheckAsync();
        await viewModel.SetLocalizedNotesAsync(notes, "en-US");

        Assert.NotNull(viewModel.Info.LocalizedReleaseNotes);
        Assert.False(viewModel.CanInstall);
        Assert.False(viewModel.ShouldShowUpdateDialog);
    }

    [Fact]
    public async Task UpdateAvailableStillLoadsLocalizedReleaseNotesAndEnablesInstall()
    {
        var viewModel = new UpdateViewModel(new FixedUpdateService(AppUpdateState.UpdateAvailable, "1.5.9"));
        var notes = new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler()));

        await viewModel.CheckAsync();
        await viewModel.SetLocalizedNotesAsync(notes, "en-US");

        Assert.Equal("1.5.9", viewModel.Info.AvailableVersion);
        Assert.NotNull(viewModel.Info.LocalizedReleaseNotes);
        Assert.True(viewModel.CanInstall);
        Assert.True(viewModel.ShouldShowUpdateDialog);
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
            Assert.False(resources["Update_ProgressPercent"].StartsWith("!", StringComparison.Ordinal));
            Assert.True(resources.ContainsKey("Update_State_Downloading"));
            Assert.True(resources.ContainsKey("Update_State_Installing"));
            Assert.True(resources.ContainsKey("Update_State_Failed"));
        }
    }

    [Fact]
    public void AboutCopyrightResourceUsesTheExactUtf8Text()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        Assert.DoesNotContain("Copyright 漏 2026 KiYouJyo", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"About_CopyrightText\"", xaml, StringComparison.Ordinal);
        foreach (var language in ReswCatalog.Languages)
            Assert.Equal("Copyright © 2026 KiYouJyo", ReswCatalog.Load(language)["About_CopyrightText.Text"]);
    }

    [Theory]
    [InlineData("1.5.6", AppUpdateState.UpToDate, false)]
    [InlineData("1.5.7", AppUpdateState.UpToDate, false)]
    [InlineData("1.5.8", AppUpdateState.UpdateAvailable, true)]
    public async Task UpdateCardAlwaysUsesLocalVersionAndOnlyAvailableUpdatesShowDialog(string availableVersion, AppUpdateState expectedState, bool expectedDialog)
    {
        var viewModel = new UpdateViewModel(new FixedUpdateService(expectedState, availableVersion));

        await viewModel.CheckAsync();

        Assert.Equal("v1.6.1", viewModel.CurrentVersion);
        Assert.Equal(expectedState, viewModel.Info.State);
        Assert.Equal(expectedDialog, viewModel.ShouldShowUpdateDialog);
        Assert.Equal(availableVersion, viewModel.Info.AvailableVersion);
    }

    [Fact]
    public void AboutUpdateCardBindsCurrentVersionToLocalVersionOnly()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.Contains("UpdateVersionText.Text = AppVersionProvider.DisplayVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateVersionText.Text = info.AvailableVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateVersionText.Text = info.Version", source, StringComparison.Ordinal);
        Assert.Contains("info.State == AppUpdateState.UpdateAvailable && !string.IsNullOrWhiteSpace(info.AvailableVersion)", source, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetLabel.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesLabel.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesContainer.Visibility", source, StringComparison.Ordinal);
        Assert.DoesNotContain("info.AvailableVersion is null ? unavailable", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class ReleaseNotesHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Payload { get; set; } = "{\"schemaVersion\":1,\"version\":\"1.5.9\",\"notes\":{\"en-US\":{\"title\":\"x\",\"items\":[\"y\"]}}}";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Payload, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    private sealed class GitHubReleaseHandler(string version) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"tag_name\":\"v{version}\",\"name\":\"UrbanPlanToolbox v{version}\",\"body\":\"notes\",\"html_url\":\"https://github.com/KiYouJyo/UrbanPlanToolbox/releases/tag/v{version}\",\"assets\":[]}}", System.Text.Encoding.UTF8, "application/json")
            });
    }

}

internal sealed class ReadyToInstallUpdateService : IAppUpdateService
{
    public int DownloadCalls { get; private set; }
    public int InstallCalls { get; private set; }

    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.6.1", ReleaseNotes: "notes"));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        DownloadCalls++;
        progress?.Report(new(AppUpdateState.ReadyToInstall));
        return Task.FromResult(new AppUpdateResult(AppUpdateState.ReadyToInstall));
    }

    public Task<AppUpdateResult> InstallPendingAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        InstallCalls++;
        return Task.FromResult(new AppUpdateResult(AppUpdateState.Restarting));
    }
}

internal sealed class PendingProgressService : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.5.7"));

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new(AppUpdateState.Downloading, 0.15));
        progress?.Report(new(AppUpdateState.Downloading));
        await Task.Delay(25, cancellationToken);
        progress?.Report(new(AppUpdateState.Installing));
        return new AppUpdateResult(AppUpdateState.Installing);
    }
}

internal sealed class FixedUpdateService(AppUpdateState resultState, string availableVersion) : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(resultState, AvailableVersion: availableVersion));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(resultState));
}

internal sealed class InstallResultUpdateService(AppUpdateState resultState) : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.5.8"));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(resultState));
}

internal sealed class UpdateRestartStub(bool result) : IApplicationRestartService
{
    public int CallCount { get; private set; }
    public bool TryRestart() => TryRestart(out _);
    public bool TryRestart(out string? failureReason) { CallCount++; failureReason = result ? null : "StubFailure"; return result; }
}
