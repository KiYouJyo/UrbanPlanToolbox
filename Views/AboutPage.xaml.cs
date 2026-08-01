using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.System;

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
        CheckUpdateButton.Content = _localization.GetString("Action_CheckForUpdates");
        Unloaded += (_, _) => _pageLifetime.Cancel();
    }

    private async void OnOpenRepository(object sender, RoutedEventArgs e)
    {
        if (!await Launcher.LaunchUriAsync(RepositoryLinks.Repository)) await ShowMessageAsync(_localization.GetString("Error_OpenRepositoryFailed"), _localization.GetString("Dialog_OpenFailedTitle"));
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
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
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !await Launcher.LaunchUriAsync(result.Release.HtmlUrl)) await ShowMessageAsync(_localization.GetString("Error_OpenReleaseFailed"), _localization.GetString("Dialog_OpenFailedTitle"));
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

    private Task ShowMessageAsync(string message, string title) => new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = _localization.GetString("Dialog_Ok") }.ShowAsync().AsTask();
    private static string ToDisplayVersion(Version version) => $"{version.Major}.{version.Minor}.{version.Build}";
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}
