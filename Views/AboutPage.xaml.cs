using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace UrbanPlanToolbox.Views;
public sealed partial class AboutPage : Page
{
    private readonly GitHubUpdateService _updateService = new();
    private readonly CancellationTokenSource _pageLifetime = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;

    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = _localization.GetFormattedString("About_Version", AppVersionProvider.DisplayVersion);
        ChannelText.Text = _localization.GetFormattedString("About_Channel", DiagnosticsInfoService.GetChannelLabel(DistributionChannelProvider.Current));
        SupportButton.Content = _localization.GetString("Action_Support");
        PrivacyButton.Content = _localization.GetString("Action_Privacy");
        NoticesButton.Content = _localization.GetString("Action_ThirdPartyNotices");
        DiagnosticsButton.Content = _localization.GetString("Action_CopyDiagnostics");
        CheckUpdateButton.Content = _localization.GetString("Action_CheckForUpdates");
        Unloaded += (_, _) => _pageLifetime.Cancel();
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e)
    {
        if (!await Launcher.LaunchUriAsync(RepositoryLinks.Repository)) await ShowMessageAsync(_localization.GetString("Error_OpenRepositoryFailed"), _localization.GetString("Dialog_OpenFailedTitle"));
    }

    private async void OnOpenSupport(object sender, RoutedEventArgs e) => await OpenDocumentAsync("SUPPORT.md");
    private async void OnOpenPrivacy(object sender, RoutedEventArgs e) => await OpenDocumentAsync("PRIVACY.md");
    private async void OnOpenNotices(object sender, RoutedEventArgs e) => await OpenDocumentAsync("THIRD-PARTY-NOTICES.md");

    private async void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(DiagnosticsInfoService.Create());
        Clipboard.SetContent(data);
        await ShowMessageAsync(_localization.GetString("Diagnostics_Copied"), _localization.GetString("Dialog_Ok"));
    }

    private async Task OpenDocumentAsync(string fileName)
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///{fileName}"));
            var text = await FileIO.ReadTextAsync(file);
            await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = fileName, Content = new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } }, CloseButtonText = _localization.GetString("Dialog_Ok") }, _pageLifetime.Token);
        }
        catch (Exception) { await ShowMessageAsync(_localization.GetString("Error_OpenDocumentFailed"), _localization.GetString("Dialog_OpenFailedTitle")); }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        if (!DistributionChannelProvider.UsesGitHubUpdates)
        {
            var message = RepositoryLinks.StoreProductUri is null ? _localization.GetString("Update_StoreManagedNoProduct") : _localization.GetString("Update_StoreManaged");
            if (RepositoryLinks.StoreProductUri is not null && await AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("Dialog_UpdateTitle"), Content = message, PrimaryButtonText = _localization.GetString("Action_OpenStore"), CloseButtonText = _localization.GetString("Action_Later") }, _pageLifetime.Token) == ContentDialogResult.Primary)
            {
                if (!await OpenStoreProductAsync()) await ShowMessageAsync(_localization.GetString("Error_OpenRepositoryFailed"), _localization.GetString("Dialog_OpenFailedTitle"));
            }
            else await ShowMessageAsync(message, _localization.GetString("Dialog_UpdateTitle"));
            return;
        }
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = _localization.GetString("Update_Checking");
        try { await ShowUpdateResultAsync(await _updateService.CheckForUpdatesAsync(AppVersionProvider.GetCurrentVersion(), _pageLifetime.Token)); }
        catch (OperationCanceledException) { }
        finally { if (!_pageLifetime.IsCancellationRequested) { CheckUpdateButton.IsEnabled = true; CheckUpdateButton.Content = _localization.GetString("Action_CheckForUpdates"); } }
    }

    private async Task ShowUpdateResultAsync(UpdateCheckResult result)
    {
        var local = ToDisplayVersion(result.LocalVersion);
        if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Release is not null && result.RemoteVersion is not null)
        {
            var notes = string.IsNullOrWhiteSpace(result.Release.Body) ? _localization.GetString("Update_NoNotes") : Truncate(result.Release.Body, 800);
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetFormattedString("Update_AvailableTitle", ToDisplayVersion(result.RemoteVersion)), Content = _localization.GetFormattedString("Update_DialogContent", local, ToDisplayVersion(result.RemoteVersion), notes), PrimaryButtonText = _localization.GetString("Action_GoToDownload"), CloseButtonText = _localization.GetString("Action_Later") };
            if (await AppDialogService.Default.ShowAsync(dialog, _pageLifetime.Token) == ContentDialogResult.Primary && !await Launcher.LaunchUriAsync(result.Release.HtmlUrl)) await ShowMessageAsync(_localization.GetString("Error_OpenReleaseFailed"), _localization.GetString("Dialog_OpenFailedTitle"));
            return;
        }

        var message = result.Status switch
        {
            UpdateCheckStatus.UpToDate => _localization.GetFormattedString("Update_UpToDate", local, ToDisplayVersion(result.RemoteVersion!)),
            UpdateCheckStatus.LocalVersionNewer => _localization.GetString("Update_LocalVersionNewer"),
            UpdateCheckStatus.NoRelease => _localization.GetString("Update_NoRelease"),
            UpdateCheckStatus.ConnectionFailed => _localization.GetString("Update_ConnectionFailed"),
            UpdateCheckStatus.TimedOut => _localization.GetString("Update_TimedOut"),
            UpdateCheckStatus.RateLimited => _localization.GetString("Update_RateLimited"),
            UpdateCheckStatus.InvalidRemoteVersion => _localization.GetString("Update_InvalidRemoteVersion"),
            _ => _localization.GetString("Update_GenericFailure")
        };
        await ShowMessageAsync(message, _localization.GetString("Dialog_UpdateTitle"));
    }

    private Task ShowMessageAsync(string message, string title) => AppDialogService.Default.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = _localization.GetString("Dialog_Ok") }, _pageLifetime.Token);
    private static async Task<bool> OpenStoreProductAsync()
    {
        if (RepositoryLinks.StoreProductUri is not null && await Launcher.LaunchUriAsync(RepositoryLinks.StoreProductUri)) return true;
        return RepositoryLinks.StoreWebUri is not null && await Launcher.LaunchUriAsync(RepositoryLinks.StoreWebUri);
    }
    private static string ToDisplayVersion(Version version) => $"{version.Major}.{version.Minor}.{version.Build}";
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}
