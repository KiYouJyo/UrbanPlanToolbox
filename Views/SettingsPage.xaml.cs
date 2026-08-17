using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using UrbanPlanToolbox.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.Storage.Pickers;

namespace UrbanPlanToolbox.Views;
public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private bool _isApplying;

    public SettingsPage()
    {
        InitializeComponent();
        TitleText.Text = _localization.GetString("Navigation_Settings");
        AppearanceLanguageTitle.Text = _localization.GetString("Settings_AppearanceLanguageTitle");
        AppearanceLanguageDescription.Text = _localization.GetString("Settings_AppearanceLanguageDescription");
        ThemeLabel.Text = _localization.GetString("Settings_ThemeLabel");
        ThemeDescription.Text = _localization.GetString("Settings_ThemeDescription");
        LanguageLabel.Text = _localization.GetString("Settings_LanguageLabel");
        LanguageDescription.Text = _localization.GetString("Settings_LanguageDescription_Runtime");

        ApplicationSettingsTitle.Text = L("应用维护", "アプリのメンテナンス", "App maintenance");
        ApplicationSettingsDescription.Text = L(
            "低频维护操作统一放在页面底部，避免与日常设置混杂。",
            "低頻度のメンテナンス操作をページ下部にまとめ、日常設定と分離します。",
            "Keep infrequent maintenance actions at the bottom, separate from everyday settings.");
        RestoreDefaultsLabel.Text = _localization.GetString("Settings_RestoreDefaultsTitle");
        RestoreDefaultsDescription.Text = _localization.GetString("Settings_RestoreDefaultsScopeDescription");
        FirstRunGuideLabel.Text = _localization.GetString("FirstRunGuide_SettingsTitle");
        FirstRunGuideDescription.Text = _localization.GetString("FirstRunGuide_SettingsDescription");
        ReopenFirstRunGuideButton.Content = _localization.GetString("FirstRunGuide_SettingsAction");

        DataManagementTitle.Text = _localization.GetString("Settings_DataManagementTitle");
        DataManagementDescription.Text = L(
            "把本地备份与 WebDAV 云存档并列呈现：本地数据仍是唯一主数据源。",
            "ローカルバックアップと WebDAV クラウドアーカイブを並べて表示します。ローカルデータが引き続き唯一の主データです。",
            "Local backup and WebDAV cloud archive are shown side by side; local data remains the single source of truth.");
        LocalBackupTitle.Text = L("本地备份", "ローカルバックアップ", "Local backup");
        LocalBackupDescription.Text = L(
            "导出或恢复完整 .uptbackup；危险清理操作保持独立。",
            "完全な .uptbackup を書き出し・復元できます。危険な消去操作は独立して扱います。",
            "Export or restore a complete .uptbackup; destructive cleanup stays separate.");
        LocalBackupStatusLabel.Text = L("状态", "状態", "Status");
        LocalBackupStatus.Text = L("本地数据正常", "ローカルデータは正常です", "Local data ready");

        MilestoneNotificationsTitle.Text = _localization.GetString("Settings_MilestoneNotificationsTitle");
        MilestoneNotificationsDescription.Text = L(
            "在项目关键节点到达时发送本机通知，并可设置默认重复提醒。",
            "プロジェクトの重要な時点でローカル通知を送り、既定の再通知間隔を設定できます。",
            "Send local notifications for project milestones and choose a default repeat interval.");
        MilestoneNotificationsLabel.Text = _localization.GetString("Settings_MilestoneNotificationsLabel");
        MilestoneNotificationsRowDescription.Text = L(
            "根据项目中的时间节点发送 Windows 本机通知。",
            "プロジェクトのマイルストーンに基づいて Windows のローカル通知を送信します。",
            "Send Windows local notifications from project milestones.");
        MilestoneNotificationsRepeatLabel.Text = _localization.GetString("Settings_MilestoneNotificationsRepeatLabel");
        MilestoneRepeatDescription.Text = L(
            "首次提醒后按所选间隔最多再提醒 3 次。",
            "最初の通知後、選択した間隔で最大 3 回まで再通知します。",
            "After the first reminder, repeat up to three times at the selected interval.");

        ResidencyTitle.Text = _localization.GetString("Residency_Title");
        ResidencySectionDescription.Text = L(
            "控制后台驻留、登录启动与灵感记录器的显示方式。",
            "バックグラウンド常駐、ログイン時起動、インスピレーションレコーダーの表示方法を管理します。",
            "Control background residency, sign-in startup, and how the inspiration recorder appears.");
        BackgroundResidencyToggle.Header = _localization.GetString("Residency_BackgroundRecorder");
        BackgroundResidencyDescription.Text = _localization.GetString("Residency_BackgroundRecorderDescription");
        SilentStartupToggle.Header = _localization.GetString("Residency_SilentStartupRecorder");
        SilentStartupDescription.Text = _localization.GetString("Residency_SilentStartupRecorderDescription");

        ConfigureAccessibility(ThemeBox, ThemeLabel.Text, ThemeDescription.Text);
        ConfigureAccessibility(LanguageBox, LanguageLabel.Text, LanguageDescription.Text);
        ConfigureAccessibility(MilestoneNotificationsToggle, MilestoneNotificationsLabel.Text, MilestoneNotificationsRowDescription.Text);
        ConfigureAccessibility(MilestoneNotificationsRepeatBox, MilestoneNotificationsRepeatLabel.Text, MilestoneRepeatDescription.Text);
        Apply(_settingsService.Load());
        ClearDataButton.Content = _localization.GetString("DataManagement_Clear");
        Loaded += OnLoaded;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var settings = await MilestoneReminderService.Default.GetSettingsAsync();
        _isApplying = true;
        ApplyMilestoneReminderSettings(settings);
        _isApplying = false;
    }
    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Update(current => { current.Theme = "System"; current.DecimalPlaces = 2; current.AutoCalculate = false; current.Language = LanguagePreference.SystemValue; current.ProjectMilestoneNotificationsEnabled = AppSettings.DefaultProjectMilestoneNotificationsEnabled; current.ProjectMilestoneReminderRepeatInterval = MilestoneReminderRepeatInterval.None; });
        Apply(settings); StatusText.Text = _localization.GetString("Status_RestoredDefaults");
        await MilestoneReminderService.Default.RefreshAsync();
        if (!string.Equals(_localization.CurrentLanguage, LanguagePreference.ResolveEffectiveLanguage(settings.Language, Windows.System.UserProfile.GlobalizationPreferences.Languages), StringComparison.OrdinalIgnoreCase))
            await _localization.SwitchLanguageAsync(settings.Language);
    }
    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplying) return;
        var settings = _settingsService.Update(current => current.Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System");
        ApplyTheme(settings.Theme); StatusText.Text = _localization.GetString("Status_SettingsSaved");
    }
    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplying) return;
        var selectedLanguage = LanguagePreference.Normalize((LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        if (string.Equals(LanguagePreference.ResolveEffectiveLanguage(selectedLanguage, Windows.System.UserProfile.GlobalizationPreferences.Languages), _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return;
        LanguageBox.IsEnabled = false;
        var switched = await _localization.SwitchLanguageAsync(selectedLanguage);
        LanguageBox.IsEnabled = true;
        if (!switched)
        {
            _isApplying = true;
            ApplyLanguageSelection(_localization.CurrentLanguage);
            _isApplying = false;
            StatusText.Text = _localization.GetString("Setting_Language_SwitchFailed");
        }
    }
    private void Apply(AppSettings settings)
    {
        _isApplying = true;
        ThemeBox.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        var language = LanguagePreference.Normalize(settings.Language); ApplyLanguageSelection(language);
        MilestoneNotificationsToggle.IsOn = settings.IsProjectMilestoneNotificationsEnabled;
        BackgroundResidencyToggle.IsOn = settings.BackgroundResidencyEnabled; SilentStartupToggle.IsOn = settings.SilentStartupShowRecorder;
        ApplyMilestoneReminderSettings(settings);
        _isApplying = false;
        ApplyTheme(settings.Theme);
    }
    private async void OnBackgroundResidencyToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplying) return;
        _isApplying = true;
        try
        {
            var enabled = BackgroundResidencyToggle.IsOn;
            if (!enabled) await WindowsStartupService.Default.SetEnabledAsync(false);

            var settings = _settingsService.Update(s =>
            {
                s.BackgroundResidencyEnabled = enabled;
                if (!enabled) s.SilentStartupShowRecorder = false;
            });

            App.ApplyBackgroundResidency(settings.BackgroundResidencyEnabled);
            if (settings.BackgroundResidencyEnabled) await App.ShowInspirationRecorderAsync(moveToPrimaryWorkAreaTopRight: true);

            SilentStartupToggle.IsOn = settings.SilentStartupShowRecorder;
            StatusText.Text = _localization.GetString("Status_SettingsSaved");
        }
        finally
        {
            _isApplying = false;
        }
    }
    private async void OnSilentStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplying) return; SilentStartupToggle.IsEnabled = false; var requested = SilentStartupToggle.IsOn; var enabled = await WindowsStartupService.Default.SetEnabledAsync(requested); SilentStartupToggle.IsEnabled = true;
        if (enabled) { var settings = _settingsService.Update(s => { s.SilentStartupShowRecorder = requested; if (requested) s.BackgroundResidencyEnabled = true; }); App.ApplyBackgroundResidency(settings.BackgroundResidencyEnabled); if (requested) await App.ShowInspirationRecorderAsync(moveToPrimaryWorkAreaTopRight: true); _isApplying = true; BackgroundResidencyToggle.IsOn = settings.BackgroundResidencyEnabled; SilentStartupToggle.IsOn = settings.SilentStartupShowRecorder; _isApplying = false; StatusText.Text = _localization.GetString("Status_SettingsSaved"); }
        else { _isApplying = true; SilentStartupToggle.IsOn = _settingsService.Load().SilentStartupShowRecorder; _isApplying = false; }
    }
    private void ApplyLanguageSelection(string language) => LanguageBox.SelectedIndex = language switch { "zh-CN" => 1, "ja-JP" => 2, "en-US" => 3, _ => 0 };
    private void OnReopenFirstRunGuide(object sender, RoutedEventArgs e) => App.MainWindow?.ShowFirstRunGuideFromSettings();
    private static void ConfigureAccessibility(FrameworkElement control, string name, string helpText) { AutomationProperties.SetName(control, name); AutomationProperties.SetHelpText(control, helpText); }
    private static void ApplyTheme(string theme) => App.MainWindow?.ApplyTheme(theme);
    private async void OnMilestoneNotificationsToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplying) return;
        var enabled = MilestoneNotificationsToggle.IsOn;
        MilestoneNotificationsToggle.IsEnabled = false;
        var result = await MilestoneReminderService.Default.SetEnabledAsync(enabled);
        MilestoneNotificationsToggle.IsEnabled = true;
        MilestoneNotificationsRepeatBox.IsEnabled = enabled;
        if (result.Succeeded)
        {
            StatusText.Text = _localization.GetString("Status_SettingsSaved");
            return;
        }

        _isApplying = true;
        ApplyMilestoneReminderSettings(_settingsService.Load());
        _isApplying = false;
        StatusText.Text = _localization.GetString("Milestone_Reminder_SchedulingFailed");
    }
    private async void OnMilestoneNotificationsRepeatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplying || MilestoneNotificationsRepeatBox.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<MilestoneReminderRepeatInterval>(item.Tag?.ToString(), out var interval)) interval = MilestoneReminderRepeatInterval.None;
        MilestoneNotificationsRepeatBox.IsEnabled = false;
        var result = await MilestoneReminderService.Default.UpdateRepeatIntervalAsync(interval);
        MilestoneNotificationsRepeatBox.IsEnabled = MilestoneNotificationsToggle.IsOn;
        if (result.Succeeded)
        {
            StatusText.Text = _localization.GetString("Status_SettingsSaved");
            return;
        }

        _isApplying = true;
        SelectRepeatInterval(_settingsService.Load().NormalizedProjectMilestoneReminderRepeatInterval);
        _isApplying = false;
        StatusText.Text = _localization.GetString("Milestone_Reminder_SchedulingFailed");
    }
    private void ApplyMilestoneReminderSettings(AppSettings settings)
    {
        MilestoneNotificationsToggle.IsOn = settings.IsProjectMilestoneNotificationsEnabled;
        SelectRepeatInterval(settings.NormalizedProjectMilestoneReminderRepeatInterval);
        MilestoneNotificationsRepeatBox.IsEnabled = settings.IsProjectMilestoneNotificationsEnabled;
    }
    private void SelectRepeatInterval(MilestoneReminderRepeatInterval interval)
    {
        var tag = interval.ToString();
        MilestoneNotificationsRepeatBox.SelectedItem = MilestoneNotificationsRepeatBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            ?? MilestoneNotificationsRepeatBox.Items[0];
    }
    private async void OnExport(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedFileName = $"UrbanPlanToolbox-{DateTime.Now:yyyyMMdd-HHmmss}" };
        picker.FileTypeChoices.Add(_localization.GetString("DataManagement_BackupFileType"), [".uptbackup"]);
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync(); if (file is null) return;
        SetDataBusy(true);
        try
        {
            var result = await new BackupDataService(AppDataPathProvider.Default, AppVersionProvider.DisplayVersion).ExportAsync(file.Path);
            if (!result.Succeeded) await file.DeleteAsync(Windows.Storage.StorageDeleteOption.PermanentDelete);
            DataStatusBar.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            DataStatusBar.Message = result.Succeeded
                ? _localization.GetFormattedString("DataManagement_ExportSuccess", result.Manifest!.ProjectCount, FormatBytes(result.FileSize))
                : _localization.GetFormattedString("DataManagement_ExportFailed", result.FailureType ?? string.Empty);
            DataStatusBar.IsOpen = true;
        }
        finally { SetDataBusy(false); }
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".uptbackup"); InitializePicker(picker);
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        var service = new BackupDataService(AppDataPathProvider.Default, AppVersionProvider.DisplayVersion);
        SetDataBusy(true);
        try
        {
            var inspection = await service.InspectAsync(file.Path);
            if (!inspection.Succeeded)
            {
                DataStatusBar.Severity = InfoBarSeverity.Error; DataStatusBar.Message = _localization.GetFormattedString("DataManagement_ValidationFailed", inspection.FailureType ?? string.Empty); DataStatusBar.IsOpen = true; return;
            }
            var manifest = inspection.Manifest!;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = _localization.GetString("DataManagement_ImportConfirmTitle"),
                Content = _localization.GetFormattedString("DataManagement_ImportConfirmMessage", manifest.ProjectCount, manifest.ActiveProjectCount, manifest.ArchivedProjectCount),
                PrimaryButtonText = _localization.GetString("DataManagement_ImportConfirmAction"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close
            };
            if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return;
            var result = await service.ImportAsync(file.Path);
            if (result.Succeeded)
            {
                var reminders = await MilestoneReminderService.Default.RefreshAsync();
                if (!reminders.Succeeded) DataStatusBar.Message = _localization.GetString("Milestone_Reminder_SchedulingFailed");
            }
            DataStatusBar.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            DataStatusBar.Message = result.Succeeded ? _localization.GetString("DataManagement_ImportSuccess") : _localization.GetFormattedString(result.RollbackSucceeded ? "DataManagement_ImportFailedRolledBack" : "DataManagement_ImportFailed", result.FailureType ?? string.Empty);
            DataStatusBar.IsOpen = true;
        }
        finally { SetDataBusy(false); }
    }

    private async void OnClearData(object sender, RoutedEventArgs e)
    {
        var first = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("DataManagement_ClearTitle"), Content = _localization.GetString("DataManagement_ClearMessage"), PrimaryButtonText = _localization.GetString("DataManagement_ClearContinue"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await AppDialogService.Default.ShowAsync(first) != ContentDialogResult.Primary) return;
        var second = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("DataManagement_ClearConfirmTitle"), Content = _localization.GetString("DataManagement_ClearConfirmMessage"), PrimaryButtonText = _localization.GetString("DataManagement_Clear"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await AppDialogService.Default.ShowAsync(second) != ContentDialogResult.Primary) return;
        SetDataBusy(true);
        var success = await new LocalDataResetService(AppDataPathProvider.Default).ResetAsync();
        if (success)
        {
            WebDavCredentialStore.Default.DeleteAll();
            await WebDavProfileService.Default.DeleteAsync();
            await WebDavControl.RefreshConfigurationAsync();
            MilestoneReminderService.Default.ClearOwnedSchedules();
            Apply(new AppSettings());
        }
        DataStatusBar.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        DataStatusBar.Message = _localization.GetString(success ? "DataManagement_ClearSuccess" : "DataManagement_ClearFailed");
        DataStatusBar.IsOpen = true;
        SetDataBusy(false);
    }

    private static void InitializePicker(object picker)
    {
        if (App.MainWindow is null) return;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
    }
    private void SetDataBusy(bool busy)
    {
        ExportButton.IsEnabled = ImportButton.IsEnabled = ClearDataButton.IsEnabled = !busy;
        WebDavControl.SetExternalBusy(busy);
    }
    private string L(string zh, string ja, string en)
    {
        var language = _localization.CurrentLanguage;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return en;
        return zh;
    }
    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.##} MB" : $"{bytes / 1024d:0.##} KB";

}
