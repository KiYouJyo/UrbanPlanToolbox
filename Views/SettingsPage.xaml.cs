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
    private readonly IApplicationRestartService _restartService = new ApplicationRestartService();
    private readonly LanguageRestartPromptCoordinator _languageRestartPrompt = new();
    private bool _isApplying;
    private string _currentLanguage = LanguagePreference.SystemValue;

    public SettingsPage()
    {
        InitializeComponent();
        TitleText.Text = _localization.GetString("Navigation_Settings");
        AppearanceLanguageTitle.Text = _localization.GetString("Settings_AppearanceLanguageTitle"); AppearanceLanguageDescription.Text = _localization.GetString("Settings_AppearanceLanguageDescription");
        ThemeLabel.Text = _localization.GetString("Settings_ThemeLabel"); ThemeDescription.Text = _localization.GetString("Settings_ThemeDescription");
        LanguageLabel.Text = _localization.GetString("Settings_LanguageLabel"); LanguageDescription.Text = _localization.GetString("Settings_LanguageDescription");
        ApplicationSettingsTitle.Text = _localization.GetString("Settings_ApplicationSettingsTitle"); ApplicationSettingsDescription.Text = _localization.GetString("Settings_ApplicationSettingsDescription");
        RestoreDefaultsLabel.Text = _localization.GetString("Settings_RestoreDefaultsTitle"); RestoreDefaultsDescription.Text = _localization.GetString("Settings_RestoreDefaultsScopeDescription");
        DataManagementTitle.Text = _localization.GetString("Settings_DataManagementTitle"); DataManagementDescription.Text = _localization.GetString("Settings_DataManagementDescription");
        ConfigureAccessibility(ThemeBox, ThemeLabel.Text, ThemeDescription.Text); ConfigureAccessibility(LanguageBox, LanguageLabel.Text, LanguageDescription.Text);
        Apply(_settingsService.Load());
        ClearDataButton.Content = _localization.GetString("DataManagement_Clear");
    }
    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var previousLanguage = _currentLanguage;
        var settings = _settingsService.Update(current => { current.Theme = "System"; current.DecimalPlaces = 2; current.AutoCalculate = false; current.Language = LanguagePreference.SystemValue; });
        Apply(settings); StatusText.Text = _localization.GetString("Status_RestoredDefaults");
        var restoredLanguage = LanguagePreference.Normalize(settings.Language);
        if (!string.Equals(previousLanguage, restoredLanguage, StringComparison.OrdinalIgnoreCase) && _languageRestartPrompt.TryBegin(previousLanguage, restoredLanguage))
            await ShowLanguageRestartDialogAsync();
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
        if (string.Equals(_currentLanguage, selectedLanguage, StringComparison.OrdinalIgnoreCase)) return;
        if (!_languageRestartPrompt.TryBegin(_currentLanguage, selectedLanguage)) { ApplyLanguageSelection(_currentLanguage); return; }
        _settingsService.Update(current => current.Language = selectedLanguage);
        _currentLanguage = selectedLanguage;
        await ShowLanguageRestartDialogAsync();
    }
    private async Task ShowLanguageRestartDialogAsync()
    {
        LanguageRestartHint.Text = _localization.GetString("Setting_Language_SavedRestartHint"); LanguageRestartHint.Visibility = Visibility.Visible;
        var restartRequested = false;
        try
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = _localization.GetString("Setting_Language_RestartTitle"), Content = _localization.GetString("Setting_Language_RestartMessage"), PrimaryButtonText = _localization.GetString("Setting_Language_RestartNow"), CloseButtonText = _localization.GetString("Setting_Language_Later"), DefaultButton = ContentDialogButton.Close };
            restartRequested = await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary;
        }
        finally { if (!_languageRestartPrompt.Complete(restartRequested, _restartService) && restartRequested) StatusText.Text = _localization.GetString("Setting_Language_RestartFailed"); }
    }
    private void Apply(AppSettings settings)
    {
        _isApplying = true;
        ThemeBox.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        var language = LanguagePreference.Normalize(settings.Language); ApplyLanguageSelection(language);
        _currentLanguage = language;
        LanguageRestartHint.Visibility = Visibility.Collapsed;
        _isApplying = false;
        ApplyTheme(settings.Theme);
    }
    private void ApplyLanguageSelection(string language) => LanguageBox.SelectedIndex = language switch { "zh-CN" => 1, "ja-JP" => 2, "en-US" => 3, _ => 0 };
    private static void ConfigureAccessibility(FrameworkElement control, string name, string helpText) { AutomationProperties.SetName(control, name); AutomationProperties.SetHelpText(control, helpText); }
    private static void ApplyTheme(string theme) => ThemePreference.Apply(App.MainWindow?.Content as FrameworkElement, theme);
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
