using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class AboutPage : Page
{
    private const string PublisherDisplayName = "Jo Kiyō";
    private readonly CancellationTokenSource _pageLifetime = new();
    private readonly CancellationTokenSource _updateLifetime = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly DistributionChannelContext _channel = new AppDistributionChannelService().GetContext();
    private readonly UpdateViewModel _updates = new(AppUpdateServiceFactory.CreateDefault());

    public AboutPage()
    {
        InitializeComponent();
        PopulateApplicationInfo();
        PrivacyButton.Content = T("Action_Privacy"); NoticesButton.Content = T("Action_ThirdPartyNotices");
        RepositoryButton.Content = T("Action_GitHubRepository"); ReleasesButton.Content = T("Action_Releases"); IssuesButton.Content = T("Action_SubmitIssue"); LicenseButton.Content = T("Action_ViewMitLicense");
        CheckUpdateButton.Content = T("Action_CheckForUpdates"); InstallUpdateButton.Content = T("Action_DownloadAndInstall"); OpenReleasesButton.Content = T("Action_OpenReleases");
        CopyDiagnosticsButton.Content = T("Action_CopyDiagnostics"); OpenLogsButton.Content = T("Action_OpenLogsFolder");
        _updates.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RenderUpdate);
        RenderUpdate(); Unloaded += (_, _) => _pageLifetime.Cancel();
    }

    private void PopulateApplicationInfo()
    {
        DisplayVersionText.Text = AppVersionProvider.DisplayVersion; PackageVersionText.Text = AppVersionProvider.GetPackageVersion();
        ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();
        ChannelText.Text = ChannelLabel(_channel); UpdateSourceText.Text = ChannelLabel(_channel); UpdateVersionText.Text = AppVersionProvider.DisplayVersion;
        PublisherText.Text = PublisherDisplayName;
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Repository);
    private async void OnOpenReleases(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Releases);
    private async void OnOpenIssues(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Issues);
    private async void OnOpenLicense(object sender, RoutedEventArgs e) => await OpenDocumentAsync("LICENSE");
    private async void OnOpenPrivacy(object sender, RoutedEventArgs e) => await OpenDocumentAsync("PRIVACY.md");
    private async void OnOpenNotices(object sender, RoutedEventArgs e) => await OpenDocumentAsync("THIRD-PARTY-NOTICES.md");
    private async void OnCheckUpdate(object sender, RoutedEventArgs e) => await _updates.CheckAsync(_pageLifetime.Token);
    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        try { var package = new DataPackage(); package.SetText(DiagnosticsInfoService.Create()); Clipboard.SetContent(package); AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Success, T("About_UpdateTitle.Text"), T("Diagnostics_Copied"))); }
        catch (Exception exception) { AppLogger.Default.Error("About", "CopyDiagnosticsFailed", exception, "Copying diagnostics failed."); }
    }
    private async void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        try { AppDataPathProvider.Default.EnsureInfrastructureDirectories(); if (!await Launcher.LaunchFolderPathAsync(AppDataPathProvider.Default.Paths.LogsDirectory)) throw new InvalidOperationException(); }
        catch (Exception exception) { AppLogger.Default.Error("About", "OpenLogsFailed", exception, "Opening the log folder failed."); AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenLogsFolderFailed"))); }
    }
    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = T("Dialog_UpdateAvailableTitle"), Content = T("Update_SaveBeforeInstall"), PrimaryButtonText = T("Action_DownloadAndInstall"), CloseButtonText = T("Action_Later") }, _pageLifetime.Token) == ContentDialogResult.Primary)
            await _updates.DownloadAndInstallAsync(_updateLifetime.Token);
    }

    private void RenderUpdate()
    {
        var info = _updates.Info; CheckUpdateButton.IsEnabled = _channel.CanCheckForUpdates && _updates.CanCheck; InstallUpdateButton.Visibility = _channel.CanSelfUpdate && info.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed; InstallUpdateButton.IsEnabled = _updates.CanInstall;
        OpenReleasesButton.Visibility = _channel.CanOpenReleases || info.State == AppUpdateState.UnsupportedChannel && _channel.Channel == DistributionChannel.GitHub ? Visibility.Visible : Visibility.Collapsed;
        var isProgressState = info.State is AppUpdateState.Downloading or AppUpdateState.Installing;
        UpdateProgress.Visibility = isProgressState ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.IsIndeterminate = info.State == AppUpdateState.Installing && _updates.Progress is null;
        if (_updates.Progress is double progress) UpdateProgress.Value = progress;
        UpdateStatusText.Text = T($"Update_State_{info.State}");
        UpdateDetailText.Text = info.State == AppUpdateState.Failed ? $"{T(AppUpdateErrorMapper.ToResourceKey(info.ErrorCode))}{(string.IsNullOrWhiteSpace(info.ErrorCode) ? string.Empty : $" ({info.ErrorCode})")}" : _updates.Progress is double progressValue ? TFormatted("Update_ProgressPercent", progressValue) : info.Detail ?? string.Empty;
    }

    private async Task OpenLinkAsync(Uri uri)
    {
        if (!await ExternalLinkService.OpenAsync(uri.ToString())) AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenExternalLinkFailed")));
    }
    private async Task OpenDocumentAsync(string fileName)
    {
        try { var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///{fileName}")); var text = await FileIO.ReadTextAsync(file); await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = fileName, Content = new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } }, CloseButtonText = T("Dialog_Ok") }, _pageLifetime.Token); }
        catch (Exception) { AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Error, T("Dialog_OpenFailedTitle"), T("Error_OpenDocumentFailed"))); }
    }
    private string T(string key) => _localization.GetString(key);
    private string TFormatted(string key, params object[] arguments) => _localization.GetFormattedString(key, arguments);
    private string ChannelLabel(DistributionChannelContext channel) => T(channel.DisplayResourceKey);
}
