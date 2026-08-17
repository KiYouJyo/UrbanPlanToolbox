using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Controls;

public sealed partial class WebDavDataManagementControl : UserControl
{
    private readonly CloudBackupService _cloudBackupService = CloudBackupService.Default;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private bool _busy;
    private bool _externalBusy;
    private bool _configured;

    public WebDavDataManagementControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ApplyLocalizedText();
        UpdateButtons();
    }

    public void SetExternalBusy(bool busy)
    {
        _externalBusy = busy;
        UpdateButtons();
    }

    public async Task RefreshConfigurationAsync()
    {
        var profile = await _cloudBackupService.GetProfileAsync();
        _configured = profile is not null && _cloudBackupService.HasCredential(profile);
        if (profile is null)
        {
            WebDavStatusValue.Text = Text("NotConfigured");
        }
        else if (!_configured)
        {
            WebDavStatusValue.Text = Text("CredentialMissing");
        }
        else if (profile.LastBackupAtUtc is null)
        {
            WebDavStatusValue.Text = Text("ConnectedNoBackup");
        }
        else
        {
            WebDavStatusValue.Text = Format("ConnectedLastBackup", CompactBackupStamp(profile.LastBackupAtUtc.Value));
        }
        UpdateButtons();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyLocalizedText();
        await RefreshConfigurationAsync();
    }

    private void ApplyLocalizedText()
    {
        WebDavTitle.Text = Text("Title");
        WebDavDescription.Text = Text("Description");
        WebDavStatusLabel.Text = Text("StatusLabel");
        WebDavBackupButton.Content = Text("BackupNow");
        WebDavRestoreButton.Content = Text("RestoreFromCloud");
        WebDavManageButton.Content = Text("Manage");
        WebDavConfigureButton.Content = Text("Configure");
    }

    private async void OnConfigureWebDav(object sender, RoutedEventArgs e)
    {
        var existing = await _cloudBackupService.GetProfileAsync();
        var serverBox = new TextBox { Header = Text("ServerUrl"), Text = existing?.ServerUrl ?? string.Empty };
        var usernameBox = new TextBox { Header = Text("Username"), Text = existing?.Username ?? string.Empty };
        var passwordBox = new PasswordBox { Header = Text("Password") };
        var remotePathBox = new TextBox { Header = Text("RemotePath"), Text = existing?.RemotePath ?? "/UrbanPlanToolbox/Backups" };
        var panel = new StackPanel { Spacing = 10, MinWidth = 500 };
        panel.Children.Add(serverBox);
        panel.Children.Add(usernameBox);
        panel.Children.Add(passwordBox);
        if (existing is not null) panel.Children.Add(new TextBlock { Text = Text("PasswordExisting"), Style = (Style)Application.Current.Resources["SettingsDescriptionStyle"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(remotePathBox);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("ConfigureTitle"),
            Content = panel,
            PrimaryButtonText = Text("TestSave"),
            CloseButtonText = Text("Close"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (existing is not null) dialog.SecondaryButtonText = Text("Disconnect");
        var dialogResult = await AppDialogService.Default.ShowAsync(dialog);
        if (dialogResult == ContentDialogResult.Secondary)
        {
            SetBusy(true);
            try
            {
                await _cloudBackupService.DisconnectAsync();
                ShowStatus(InfoBarSeverity.Success, Text("NotConfigured"));
                await RefreshConfigurationAsync();
            }
            finally { SetBusy(false); }
            return;
        }
        if (dialogResult != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            var requested = new WebDavProfile
            {
                ServerUrl = serverBox.Text,
                Username = usernameBox.Text,
                RemotePath = remotePathBox.Text,
                LastBackupAtUtc = existing?.LastBackupAtUtc
            };
            var result = await _cloudBackupService.TestAndSaveAsync(requested, passwordBox.Password);
            if (result.Succeeded)
            {
                var profile = await _cloudBackupService.GetProfileAsync();
                var insecureHttp = profile is not null && profile.ServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                ShowStatus(insecureHttp ? InfoBarSeverity.Warning : InfoBarSeverity.Success, insecureHttp ? Text("HttpWarning") : Text("TestSuccess"));
            }
            else ShowStatus(InfoBarSeverity.Error, WebDavLocalization.StatusText(_localization.CurrentLanguage, result.Status, result.ErrorCode));
            await RefreshConfigurationAsync();
        }
        finally { SetBusy(false); }
    }

    private async void OnCreateCloudBackup(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var result = await _cloudBackupService.CreateAsync();
            ShowStatus(result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                result.Succeeded ? Text("BackupSuccess") : WebDavLocalization.StatusText(_localization.CurrentLanguage, result.Status, result.ErrorCode));
            await RefreshConfigurationAsync();
        }
        finally { SetBusy(false); }
    }

    private async void OnRestoreFromCloud(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var result = await _cloudBackupService.ListAsync();
            if (!result.Succeeded)
            {
                ShowStatus(InfoBarSeverity.Error, WebDavLocalization.StatusText(_localization.CurrentLanguage, result.Status, result.ErrorCode));
                return;
            }
            if (result.Items.Count == 0)
            {
                await ShowNoBackupsStatusAsync();
                return;
            }

            var selected = await SelectBackupAsync(result.Items, Text("RestorePickerTitle"), Text("Restore"));
            if (selected is null) return;
            _ = await RestoreBackupAsync(selected);
        }
        finally { SetBusy(false); }
    }

    private async void OnManageCloudBackups(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            while (true)
            {
                var result = await _cloudBackupService.ListAsync();
                if (!result.Succeeded)
                {
                    ShowStatus(InfoBarSeverity.Error, WebDavLocalization.StatusText(_localization.CurrentLanguage, result.Status, result.ErrorCode));
                    return;
                }
                if (result.Items.Count == 0)
                {
                    await ShowNoBackupsStatusAsync();
                    return;
                }

                var list = CreateBackupList(result.Items);
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = Text("ManageTitle"),
                    Content = list,
                    PrimaryButtonText = Text("Restore"),
                    SecondaryButtonText = Text("Delete"),
                    CloseButtonText = Text("Close"),
                    DefaultButton = ContentDialogButton.Close,
                    IsPrimaryButtonEnabled = false,
                    IsSecondaryButtonEnabled = false
                };
                list.SelectionChanged += (_, _) =>
                {
                    var hasSelection = list.SelectedItem is ListViewItem { Tag: CloudBackupItem };
                    dialog.IsPrimaryButtonEnabled = hasSelection;
                    dialog.IsSecondaryButtonEnabled = hasSelection;
                };
                var action = await AppDialogService.Default.ShowAsync(dialog);
                if (action == ContentDialogResult.None) return;
                if (list.SelectedItem is not ListViewItem { Tag: CloudBackupItem selected }) continue;

                if (action == ContentDialogResult.Primary)
                {
                    if (await RestoreBackupAsync(selected)) return;
                    continue;
                }

                var deleteConfirm = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = Text("DeleteConfirmTitle"),
                    Content = Format("DeleteConfirm", selected.FileName),
                    PrimaryButtonText = Text("Delete"),
                    CloseButtonText = Text("Close"),
                    DefaultButton = ContentDialogButton.Close
                };
                if (await AppDialogService.Default.ShowAsync(deleteConfirm) != ContentDialogResult.Primary) continue;
                var deletion = await _cloudBackupService.DeleteAsync(selected);
                if (!deletion.Succeeded)
                {
                    ShowStatus(InfoBarSeverity.Error, WebDavLocalization.StatusText(_localization.CurrentLanguage, deletion.Status, deletion.ErrorCode));
                    return;
                }
                ShowStatus(InfoBarSeverity.Success, Text("DeleteSuccess"));
            }
        }
        finally { SetBusy(false); }
    }

    private async Task<CloudBackupItem?> SelectBackupAsync(IReadOnlyList<CloudBackupItem> items, string title, string primaryButtonText)
    {
        var list = CreateBackupList(items);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = list,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = Text("Close"),
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false
        };
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItem is ListViewItem { Tag: CloudBackupItem };
        var action = await AppDialogService.Default.ShowAsync(dialog);
        return action == ContentDialogResult.Primary && list.SelectedItem is ListViewItem { Tag: CloudBackupItem selected }
            ? selected
            : null;
    }

    private async Task<bool> RestoreBackupAsync(CloudBackupItem selected)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Text("RestoreConfirmTitle"),
            Content = Text("RestoreConfirm"),
            PrimaryButtonText = Text("Restore"),
            CloseButtonText = Text("Close"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await AppDialogService.Default.ShowAsync(confirm) != ContentDialogResult.Primary) return false;

        var restore = await _cloudBackupService.RestoreAsync(selected);
        if (restore.Succeeded)
        {
            var reminders = await MilestoneReminderService.Default.RefreshAsync();
            ShowStatus(reminders.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                reminders.Succeeded ? Text("RestoreSuccess") : _localization.GetString("Milestone_Reminder_SchedulingFailed"));
        }
        else ShowStatus(InfoBarSeverity.Error, WebDavLocalization.StatusText(_localization.CurrentLanguage, restore.Status, restore.ErrorCode));
        return true;
    }

    private async Task ShowNoBackupsStatusAsync()
    {
        var profile = await _cloudBackupService.GetProfileAsync();
        var hadSuccessfulBackup = profile?.LastBackupAtUtc is not null;
        ShowStatus(
            hadSuccessfulBackup ? InfoBarSeverity.Warning : InfoBarSeverity.Informational,
            Text(hadSuccessfulBackup ? "NoBackupsAfterCreate" : "NoBackups"));
    }

    private ListView CreateBackupList(IEnumerable<CloudBackupItem> items)
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MinWidth = 520, MaxHeight = 360 };
        foreach (var item in items)
            list.Items.Add(new ListViewItem { Content = FormatBackupItem(item), Tag = item });
        return list;
    }

    private string FormatBackupItem(CloudBackupItem item)
    {
        var timestamp = item.SortTimeUtc == DateTimeOffset.MinValue ? "—" : item.SortTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var version = string.IsNullOrWhiteSpace(item.AppVersion) ? "—" : $"v{item.AppVersion}";
        return $"{timestamp}   {version}   {FormatBytes(item.Size)}\n{item.FileName}";
    }

    private static string CompactBackupStamp(DateTimeOffset backupAtUtc)
    {
        var local = backupAtUtc.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? local.ToString("HH:mm") : local.ToString("yyyy-MM-dd HH:mm");
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var available = !_busy && !_externalBusy;
        WebDavConfigureButton.IsEnabled = available;
        WebDavBackupButton.IsEnabled = available && _configured;
        WebDavRestoreButton.IsEnabled = available && _configured;
        WebDavManageButton.IsEnabled = available && _configured;
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        WebDavStatusBar.Severity = severity;
        WebDavStatusBar.Message = message;
        WebDavStatusBar.IsOpen = true;
    }

    private string Text(string key) => WebDavLocalization.Get(_localization.CurrentLanguage, key);
    private string Format(string key, params object[] args) => WebDavLocalization.Format(_localization.CurrentLanguage, key, args);
    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.##} MB" : $"{bytes / 1024d:0.##} KB";
}