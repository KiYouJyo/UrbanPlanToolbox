using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using System.ComponentModel;
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
    public async Task RestartRequiredUsesApplicationRestartAndKeepsRetryOnFailure()
    {
        var service = new FixedUpdateService(AppUpdateState.RestartRequired, "1.6.9");
        var restart = new UpdateRestartStub(false);
        var viewModel = new UpdateViewModel(service, restart);

        await viewModel.CheckAsync();
        await viewModel.RestartAndUpdateAsync();

        Assert.Equal(AppUpdateState.RestartRequired, viewModel.Info.State);
        Assert.Equal("StubFailure", viewModel.RestartFailureReason);
        Assert.Equal(1, restart.CallCount);
        Assert.True(viewModel.CanInstall);
    }

    [Fact]
    public void StoreUsesDownloadThenExplicitInstallAndGitHubLogicRemainsSeparate()
    {
        var root = FindRepositoryRoot();
        var store = File.ReadAllText(Path.Combine(root, "Services", "StoreAppUpdateService.cs"));
        var github = File.ReadAllText(Path.Combine(root, "Services", "GitHubAppUpdateService.cs"));
        var about = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("RequestDownloadStorePackageUpdatesAsync", store);
        Assert.Contains("RequestDownloadAndInstallStorePackageUpdatesAsync", store);
        Assert.Contains("StoreDownloadCompleted", store);
        Assert.Contains("new(AppUpdateState.ReadyToInstall)", store);
        Assert.Contains("StoreInstallCompleted", store);
        Assert.Contains("new(AppUpdateState.Completed)", store);
        Assert.Contains("AppUpdateState.ReadyToInstall", github);
        Assert.Contains("info.NeedsFinalRestart ? T(\"Action_RestartAndUpdate\")", about);
    }

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
        Assert.Contains("HandleStoreProgress(status, progress, isInstallOperation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreUpdateIsNotBoundToAboutPageUnloadToken()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));
        Assert.Contains("_updates.DownloadAndInstallAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_updates.DownloadAndInstallAsync(_pageLifetime.Token)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_updateLifetime", source, StringComparison.Ordinal);
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
    public void AboutPageUsesOnlyTheStatusProgressRingAndNoDashPlaceholders()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "AboutPage.xaml.cs"));

        Assert.Contains("x:Name=\"UpdateStatusProgressRing\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateTargetProgressRing", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateNotesProgressRing", xaml, StringComparison.Ordinal);
        Assert.Contains("ProgressRing", xaml, StringComparison.Ordinal);
        Assert.Contains("var checking = info.State == AppUpdateState.Checking", code, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusProgressRing.IsActive = checking", code, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetLabel.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetText.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesLabel.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesContainer.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesText.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusProgressRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed", code, StringComparison.Ordinal);
        Assert.DoesNotContain("unavailableValue", code, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u2014", code, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("zh-CN", "首次使用向导")]
    [InlineData("ja-JP", "初回起動ガイド")]
    [InlineData("en-US", "first-run guide")]
    public async Task RemoteReleaseNotes168CanBeConsumedByProductionModelForEveryLocale(string locale, string expectedItem)
    {
        var root = FindRepositoryRoot();
        var payload = File.ReadAllText(Path.Combine(root, "docs", "release-notes", "1.6.8.json"));
        var service = new LocalizedReleaseNotesService(
            new HttpClient(new ReleaseNotesHandler { Payload = payload }),
            (_, _) => Task.FromResult<string?>(null));

        var notes = await service.GetAsync("1.6.8", locale);

        Assert.NotNull(notes);
        Assert.Equal("1.6.8", notes!.Version);
        Assert.False(string.IsNullOrWhiteSpace(notes.Notes[locale].Title));
        Assert.NotEmpty(notes.Notes[locale].Items);
        Assert.Contains(notes.Notes[locale].Items, item => item.Contains(expectedItem, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BundledAndRemoteReleaseNotes168UseTheSameRuntimeContract()
    {
        var root = FindRepositoryRoot();
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var bundled = System.Text.Json.JsonSerializer.Deserialize<LocalizedReleaseNotes>(File.ReadAllText(Path.Combine(root, "Assets", "Data", "ReleaseNotes", "1.6.8.json")), options);
        var remote = System.Text.Json.JsonSerializer.Deserialize<LocalizedReleaseNotes>(File.ReadAllText(Path.Combine(root, "docs", "release-notes", "1.6.8.json")), options);

        Assert.NotNull(bundled);
        Assert.NotNull(remote);
        Assert.Equal(bundled!.SchemaVersion, remote!.SchemaVersion);
        Assert.Equal(bundled.Version, remote.Version);
        Assert.Equal(bundled.Notes.Keys.Order(StringComparer.Ordinal), remote.Notes.Keys.Order(StringComparer.Ordinal));
        foreach (var locale in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            Assert.Equal(bundled.Notes[locale].Title, remote.Notes[locale].Title);
            Assert.Equal(bundled.Notes[locale].Items, remote.Notes[locale].Items);
            Assert.Contains(bundled.Notes[locale].Items, item => item.Contains(locale == "zh-CN" ? "首次" : locale == "ja-JP" ? "初回" : "first-run", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(bundled.Notes[locale].Items, item => item.Contains(locale == "zh-CN" ? "浮层" : locale == "ja-JP" ? "フローティング" : "transient UI", StringComparison.OrdinalIgnoreCase));
        }
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
    public async Task CheckRetainsReleaseMetadataWhileChecking()
    {
        var service = new BlockingUpdateService();
        var viewModel = new UpdateViewModel(service);
        await viewModel.CheckAsync();
        var check = viewModel.CheckAsync();

        await service.Started;
        Assert.Equal(AppUpdateState.Checking, viewModel.Info.State);
        Assert.Equal("1.6.2", viewModel.Info.AvailableVersion);
        Assert.Equal("latest notes", viewModel.Info.ReleaseNotes);

        service.Release();
        await check;
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

    [Theory]
    [InlineData("zh-CN", "中文更新说明")]
    [InlineData("ja-JP", "日本語の更新内容")]
    [InlineData("en-US", "English release notes")]
    public async Task ReleaseNotesUseTheRequestedLanguage(string locale, string expectedItem)
    {
        var payload = "{\"schemaVersion\":1,\"version\":\"1.6.2\",\"notes\":{\"zh-CN\":{\"title\":\"中文\",\"items\":[\"中文更新说明\"]},\"ja-JP\":{\"title\":\"日本語\",\"items\":[\"日本語の更新内容\"]},\"en-US\":{\"title\":\"English\",\"items\":[\"English release notes\"]}}}";
        var notes = await new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler { Payload = payload })).GetAsync("1.6.2", locale);

        Assert.NotNull(notes);
        Assert.Contains(expectedItem, notes!.Notes[locale].Items);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    public async Task MissingRequestedLanguageDoesNotReturnEnglishReleaseNotes(string locale)
    {
        var payload = "{\"schemaVersion\":1,\"version\":\"1.6.2\",\"notes\":{\"en-US\":{\"title\":\"English\",\"items\":[\"English release notes\"]}}}";
        var notes = await new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler { Payload = payload })).GetAsync("1.6.2", locale);

        Assert.Null(notes);
    }

    [Theory]
    [InlineData("1.6.2")]
    [InlineData("v1.6.2")]
    [InlineData("1.6.2.0")]
    public async Task PackagedReleaseNotesNormalizeVersionStrings(string version)
    {
        var service = new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler()), BundledNotes);
        var notes = await service.GetAsync(version, "zh-Hans-CN");

        Assert.NotNull(notes);
        Assert.Contains("中文说明", notes!.Notes["zh-CN"].Items);
    }

    [Theory]
    [InlineData("zh-CN", "中文说明")]
    [InlineData("ja-JP", "日本語の説明")]
    [InlineData("en-US", "English notes")]
    public async Task LocalVersionNewerUsesPackagedNotesForTheCurrentLocale(string locale, string expected)
    {
        var notes = new LocalizedReleaseNotesService(new HttpClient(new ReleaseNotesHandler()), BundledNotes);
        var viewModel = new UpdateViewModel(new FixedUpdateService(AppUpdateState.UpToDate, "1.6.2", "English release body"));
        await viewModel.CheckAsync();
        await viewModel.SetLocalizedNotesAsync(notes, locale);

        var display = ReleaseNotesPresentation.Resolve(viewModel.Info, locale, "no notes");
        Assert.Equal(ReleaseNotesDisplaySource.LocalizedPackage, display.Source);
        Assert.Contains(expected, display.Text);
        Assert.DoesNotContain("English release body", display.Text);
    }

    [Theory]
    [InlineData("zh-CN", "暂无更新说明")]
    [InlineData("ja-JP", "更新内容はありません")]
    public void MissingNonEnglishNotesDoNotFallBackToEnglishReleaseBody(string locale, string fallback)
    {
        var display = ReleaseNotesPresentation.Resolve(new AppUpdateInfo(AppUpdateState.UpToDate, "1.6.2", ReleaseNotes: "English release body"), locale, fallback);
        Assert.Equal(ReleaseNotesDisplaySource.LocalizedEmptyFallback, display.Source);
        Assert.Equal(fallback, display.Text);
    }

    [Fact]
    public void PackagedReleaseNotes162Through164ContainAllLocales()
    {
        var root = FindRepositoryRoot();
        foreach (var version in new[] { "1.6.2", "1.6.3", "1.6.4" })
        {
            var document = System.Text.Json.JsonSerializer.Deserialize<LocalizedReleaseNotes>(File.ReadAllText(Path.Combine(root, "Assets", "Data", "ReleaseNotes", $"{version}.json")), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(document);
            Assert.Equal(new[] { "en-US", "ja-JP", "zh-CN" }, document!.Notes.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.All(document.Notes.Values, note => Assert.NotEmpty(note.Items));
        }
    }

    [Theory]
    [InlineData(AppUpdateState.ReadyToInstall)]
    [InlineData(AppUpdateState.Failed)]
    public async Task LateDownloadingProgressCannotOverwriteAnAuthoritativeFinalResult(AppUpdateState finalState)
    {
        var service = new LateDownloadingCallbackService(finalState);
        var viewModel = new UpdateViewModel(service);
        await viewModel.CheckAsync();
        await viewModel.DownloadAndInstallAsync();

        service.ReportLateDownloading();
        await Task.Delay(50);

        Assert.Equal(finalState, viewModel.Info.State);
    }

    [Theory]
    [InlineData(AppUpdateState.ReadyToInstall)]
    [InlineData(AppUpdateState.Installing)]
    public async Task DownloadProgressCannotRegressReadyOrInstallingState(AppUpdateState finalState)
    {
        var service = new LateDownloadingCallbackService(finalState);
        var viewModel = new UpdateViewModel(service);
        await viewModel.CheckAsync();
        await viewModel.DownloadAndInstallAsync();

        service.ReportLateDownloading();
        await Task.Delay(50);

        Assert.Equal(finalState, viewModel.Info.State);
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
    public void StoreUpdatePresentationKeepsVersionAndSourceWhenNoPackageUpdateExists()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Services", "StoreAppUpdateService.cs"));
        Assert.Contains("AppUpdateState.UpToDate, AvailableVersion: AppVersionProvider.Version", source, StringComparison.Ordinal);
        Assert.Contains("Source: UpdateInstallSource.Store", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppUpdateState.UpToDate, Source: UpdateInstallSource.Unknown", source, StringComparison.Ordinal);
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

        Assert.Equal("v1.7.2", viewModel.CurrentVersion);
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
        Assert.Contains("string.IsNullOrWhiteSpace(info.AvailableVersion)", source, StringComparison.Ordinal);
        Assert.Contains("UpdateTargetLabel.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesLabel.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("UpdateNotesContainer.Visibility", source, StringComparison.Ordinal);
        Assert.DoesNotContain("info.AvailableVersion is null ? unavailable", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckContinuesWhenAboutPageDetaches()
    {
        var service = new DeferredUpdateService(AppUpdateState.UpdateAvailable);
        var session = new UpdateViewModel(service);

        var operation = session.CheckAsync();
        await service.CheckStarted;
        Assert.Equal(AppUpdateState.Checking, session.Info.State);
        // A presenter detach does not supply a cancellation token to the application session.
        Assert.False(service.OperationCancellationRequested);

        service.CompleteCheck();
        await operation;

        Assert.Equal(AppUpdateState.UpdateAvailable, session.Info.State);
        Assert.Equal("1.7.2", session.Info.AvailableVersion);
    }

    [Fact]
    public async Task DownloadProgressSurvivesNavigationAndDoesNotDuplicateTheOperation()
    {
        var service = new DeferredUpdateService(AppUpdateState.ReadyToInstall);
        var session = new UpdateViewModel(service);
        var check = session.CheckAsync();
        await service.CheckStarted;
        service.CompleteCheck();
        await check;

        var operation = session.DownloadAndInstallAsync();
        await service.DownloadStarted;
        var progress25 = WaitForProgressAsync(session, 0.25);
        service.ReportDownload(0.25);
        await progress25;
        Assert.Equal(0.25, session.Progress);

        // Simulated detach/reattach only observes the same session state.
        var progress55 = WaitForProgressAsync(session, 0.55);
        service.ReportDownload(0.55);
        await progress55;
        Assert.Equal(0.55, session.Progress);
        await session.DownloadAndInstallAsync();
        Assert.Equal(1, service.DownloadCalls);

        service.CompleteDownload();
        await operation;
        Assert.Equal(AppUpdateState.ReadyToInstall, session.Info.State);
        Assert.Equal("1.7.2", session.Info.AvailableVersion);
    }

    private static async Task WaitForProgressAsync(UpdateViewModel session, double expected)
    {
        if (session.Progress == expected) return;

        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateViewModel.Progress) && session.Progress == expected)
                observed.TrySetResult();
        };

        session.PropertyChanged += handler;
        try
        {
            if (session.Progress == expected) return;
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            session.PropertyChanged -= handler;
        }
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

    private static Task<string?> BundledNotes(string version, CancellationToken cancellationToken) => Task.FromResult<string?>(version == "1.6.2"
        ? "{\"schemaVersion\":1,\"version\":\"1.6.2\",\"notes\":{\"zh-CN\":{\"title\":\"中文\",\"items\":[\"中文说明\"]},\"ja-JP\":{\"title\":\"日本語\",\"items\":[\"日本語の説明\"]},\"en-US\":{\"title\":\"English\",\"items\":[\"English notes\"]}}}"
        : null);

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

internal sealed class FixedUpdateService(AppUpdateState resultState, string availableVersion, string? releaseNotes = null) : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(resultState, AvailableVersion: availableVersion, ReleaseNotes: releaseNotes));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(resultState));
}

internal sealed class BlockingUpdateService : IAppUpdateService
{
    private int _calls;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Started => _started.Task;

    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _calls) == 1)
            return Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.6.2", ReleaseNotes: "latest notes"));
        _started.TrySetResult();
        return WaitAsync(cancellationToken);
    }

    private async Task<AppUpdateInfo> WaitAsync(CancellationToken cancellationToken)
    {
        await _release.Task.WaitAsync(cancellationToken);
        return new(AppUpdateState.UpToDate, AvailableVersion: "1.6.2", ReleaseNotes: "latest notes");
    }

    public void Release() => _release.TrySetResult();

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(AppUpdateState.Failed));
}

internal sealed class InstallResultUpdateService(AppUpdateState resultState) : IAppUpdateService
{
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.5.8"));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateResult(resultState));
}

internal sealed class LateDownloadingCallbackService(AppUpdateState finalState) : IAppUpdateService
{
    private IProgress<AppUpdateProgress>? _progress;
    public Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppUpdateInfo(AppUpdateState.UpdateAvailable, AvailableVersion: "1.6.4"));

    public Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        _progress = progress;
        progress?.Report(new(AppUpdateState.Downloading, 1d));
        return Task.FromResult(new AppUpdateResult(finalState));
    }


    public void ReportLateDownloading() => _progress?.Report(new(AppUpdateState.Downloading, 1d));
}

internal sealed class UpdateRestartStub(bool result) : IApplicationRestartService
{
    public int CallCount { get; private set; }
    public bool TryRestart() => TryRestart(out _);
    public bool TryRestart(out string? failureReason) { CallCount++; failureReason = result ? null : "StubFailure"; return result; }
}

internal sealed class DeferredUpdateService(AppUpdateState downloadResult) : IAppUpdateService
{
    private readonly TaskCompletionSource _checkStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _downloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _checkCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _downloadCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IProgress<AppUpdateProgress>? _progress;
    private CancellationToken _operationToken;
    public int DownloadCalls { get; private set; }
    public bool OperationCancellationRequested => _operationToken.IsCancellationRequested;
    public Task CheckStarted => _checkStarted.Task;
    public Task DownloadStarted => _downloadStarted.Task;

    public async Task<AppUpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _operationToken = cancellationToken; _checkStarted.TrySetResult();
        await _checkCompletion.Task.WaitAsync(cancellationToken);
        return new(AppUpdateState.UpdateAvailable, "1.7.2", "notes", Source: UpdateInstallSource.GitHub);
    }

    public async Task<AppUpdateResult> DownloadAndInstallAsync(IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        DownloadCalls++; _operationToken = cancellationToken; _progress = progress; _downloadStarted.TrySetResult();
        await _downloadCompletion.Task.WaitAsync(cancellationToken);
        return new(downloadResult);
    }

    public void CompleteCheck() => _checkCompletion.TrySetResult();
    public void ReportDownload(double value) => _progress?.Report(new(AppUpdateState.Downloading, value));
    public void CompleteDownload() => _downloadCompletion.TrySetResult();
}
