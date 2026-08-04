using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.ViewModels;
using Windows.ApplicationModel;
using Windows.Storage;

namespace UrbanPlanToolbox.Views;

public sealed partial class AboutPage : Page
{
    private readonly CancellationTokenSource _pageLifetime = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly UpdateViewModel _updates = new(AppUpdateServiceFactory.CreateDefault());

    public AboutPage()
    {
        InitializeComponent();
        PopulateApplicationInfo();
        PrivacyButton.Content = T("Action_Privacy"); NoticesButton.Content = T("Action_ThirdPartyNotices");
        RepositoryButton.Content = T("Action_GitHubRepository"); ReleasesButton.Content = T("Action_Releases"); IssuesButton.Content = T("Action_SubmitIssue"); LicenseButton.Content = T("Action_ViewMitLicense");
        CheckUpdateButton.Content = T("Action_CheckForUpdates"); InstallUpdateButton.Content = T("Action_DownloadAndInstall"); OpenReleasesButton.Content = T("Action_OpenReleases");
        _updates.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(RenderUpdate);
        RenderUpdate(); Unloaded += (_, _) => _pageLifetime.Cancel();
    }

    private void PopulateApplicationInfo()
    {
        DisplayVersionText.Text = AppVersionProvider.DisplayVersion; PackageVersionText.Text = AppVersionProvider.GetPackageVersion();
        ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();
        var channel = new AppDistributionChannelService().GetCurrentChannel(); ChannelText.Text = ChannelLabel(channel); UpdateSourceText.Text = ChannelLabel(channel); UpdateVersionText.Text = AppVersionProvider.DisplayVersion;
        try { PackageIdentityText.Text = Package.Current.Id.FullName; PublisherText.Text = Package.Current.Id.Publisher; }
        catch (Exception) when (OperatingSystem.IsWindows()) { PackageIdentityText.Text = T("About_Unavailable"); PublisherText.Text = T("About_Unavailable"); }
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Repository);
    private async void OnOpenReleases(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Releases);
    private async void OnOpenIssues(object sender, RoutedEventArgs e) => await OpenLinkAsync(RepositoryLinks.Issues);
    private async void OnOpenLicense(object sender, RoutedEventArgs e) => await OpenDocumentAsync("LICENSE");
    private async void OnOpenPrivacy(object sender, RoutedEventArgs e) => await OpenDocumentAsync("PRIVACY.md");
    private async void OnOpenNotices(object sender, RoutedEventArgs e) => await OpenDocumentAsync("THIRD-PARTY-NOTICES.md");
    private async void OnCheckUpdate(object sender, RoutedEventArgs e) => await _updates.CheckAsync(_pageLifetime.Token);
    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = T("About_UpdateTitle"), Content = T("Update_SaveBeforeInstall"), PrimaryButtonText = T("Action_DownloadAndInstall"), CloseButtonText = T("Action_Later") }, _pageLifetime.Token) == ContentDialogResult.Primary)
            await _updates.DownloadAndInstallAsync(_pageLifetime.Token);
    }

    private void RenderUpdate()
    {
        var info = _updates.Info; CheckUpdateButton.IsEnabled = _updates.CanCheck; InstallUpdateButton.Visibility = info.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed; InstallUpdateButton.IsEnabled = _updates.CanInstall;
        OpenReleasesButton.Visibility = info.State == AppUpdateState.UnsupportedChannel ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Visibility = _updates.Progress is null ? Visibility.Collapsed : Visibility.Visible;
        if (_updates.Progress is double progress) UpdateProgress.Value = progress;
        UpdateStatusText.Text = T($"Update_State_{info.State}");
        UpdateDetailText.Text = info.State == AppUpdateState.Failed ? $"{T(AppUpdateErrorMapper.ToResourceKey(info.ErrorCode))}{(string.IsNullOrWhiteSpace(info.ErrorCode) ? string.Empty : $" ({info.ErrorCode})")}" : info.Detail ?? string.Empty;
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
    private string ChannelLabel(DistributionChannel channel) => T(channel == DistributionChannel.Store ? "About_ChannelStore" : "About_ChannelGitHub");
}
