using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
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
        AppearanceLanguageTitle.Text = _localization.GetString("Settings_AppearanceLanguageTitle"); AppearanceLanguageDescription.Text = _localization.GetString("Settings_AppearanceLanguageDescription");
        ThemeLabel.Text = _localization.GetString("Settings_ThemeLabel"); ThemeDescription.Text = _localization.GetString("Settings_ThemeDescription");
        LanguageLabel.Text = _localization.GetString("Settings_LanguageLabel"); LanguageDescription.Text = _localization.GetString("Settings_LanguageDescription_Runtime");
        ApplicationSettingsTitle.Text = _localization.GetString("Settings_ApplicationSettingsTitle"); ApplicationSettingsDescription.Text = _localization.GetString("Settings_ApplicationSettingsDescription");
        RestoreDefaultsLabel.Text = _localization.GetString("Settings_RestoreDefaultsTitle"); RestoreDefaultsDescription.Text = _localization.GetString("Settings_RestoreDefaultsScopeDescription");
        FirstRunGuideLabel.Text = _localization.GetString("FirstRunGuide_SettingsTitle"); FirstRunGuideDescription.Text = _localization.GetString("FirstRunGuide_SettingsDescription"); ReopenFirstRunGuideButton.Content = _localization.GetString("FirstRunGuide_SettingsAction");
        DataManagementTitle.Text = _localization.GetString("Settings_DataManagementTitle"); DataManagementDescription.Text = _localization.GetString("Settings_DataManagementDescription");
        MilestoneNotificationsTitle.Text = _localization.GetString("Settings_MilestoneNotificationsTitle");
        MilestoneNotificationsDescription.Text = _localization.GetString("Settings_MilestoneNotificationsDescription");
        MilestoneNotificationsLabel.Text = _localization.GetString("Settings_MilestoneNotificationsLabel");
        MilestoneNotificationsRepeatLabel.Text = _localization.GetString("Settings_MilestoneNotificationsRepeatLabel");
        ResidencyTitle.Text = _localization.GetString("Residency_Title"); BackgroundResidencyToggle.Header = _localization.GetString("Residency_BackgroundRecorder"); BackgroundResidencyDescription.Text = _localization.GetString("Residency_BackgroundRecorderDescription"); SilentStartupToggle.Header = _localization.GetString("Residency_SilentStartupRecorder"); SilentStartupDescription.Text = _localization.GetString("Residency_SilentStartupRecorderDescription");
        MilestoneNotificationsToggle.OnContent = _localization.GetString("Settings_MilestoneNotificationsOn");
        MilestoneNotificationsToggle.OffContent = _localization.GetString("Settings_MilestoneNotificationsOff");
        ConfigureAccessibility(ThemeBox, ThemeLabel.Text, ThemeDescription.Text); ConfigureAccessibility(LanguageBox, LanguageLabel.Text, LanguageDescription.Text);
        ConfigureAccessibility(MilestoneNotificationsToggle, MilestoneNotificationsLabel.Text, MilestoneNotificationsDescription.Text);
        ConfigureAccessibility(MilestoneNotificationsRepeatBox, MilestoneNotificationsRepeatLabel.Text, MilestoneNotificationsDescription.Text);
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
        var enabled = BackgroundResidencyToggle.IsOn;
        if (!enabled && !await WindowsStartupService.Default.SetEnabledAsync(false))
        {
            _isApplying = true;
            BackgroundResidencyToggle.IsOn = _settingsService.Load().BackgroundResidencyEnabled;
            _isApplying = false;
            return;
        }
        _settingsService.Update(s => { s.BackgroundResidencyEnabled = enabled; if (!enabled) s.SilentStartupShowRecorder = false; });
        App.ApplyBackgroundResidency(enabled);
        _isApplying = true; SilentStartupToggle.IsOn = _settingsService.Load().SilentStartupShowRecorder; _isApplying = false;
        StatusText.Text = _localization.GetString("Status_SettingsSaved");
    }
    private async void OnSilentStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplying) return; SilentStartupToggle.IsEnabled = false; var requested = SilentStartupToggle.IsOn; var enabled = await WindowsStartupService.Default.SetEnabledAsync(requested); SilentStartupToggle.IsEnabled = true;
        if (enabled) { var settings = _settingsService.Update(s => { s.SilentStartupShowRecorder = requested; if (requested) s.BackgroundResidencyEnabled = true; }); App.ApplyBackgroundResidency(settings.BackgroundResidencyEnabled); _isApplying = true; BackgroundResidencyToggle.IsOn = settings.BackgroundResidencyEnabled; SilentStartupToggle.IsOn = settings.SilentStartupShowRecorder; _isApplying = false; StatusText.Text = _localization.GetString("Status_SettingsSaved"); }
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
        if (success) { MilestoneReminderService.Default.ClearOwnedSchedules(); Apply(new AppSettings()); }
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
    private void SetDataBusy(bool busy) { ExportButton.IsEnabled = ImportButton.IsEnabled = ClearDataButton.IsEnabled = !busy; }
    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024d):0.##} MB" : $"{bytes / 1024d:0.##} KB";

}
