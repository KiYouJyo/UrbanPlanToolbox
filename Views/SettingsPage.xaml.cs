using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Windows.Storage.Pickers;

namespace UrbanPlanToolbox.Views;
public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private bool _isApplying;
    private string _currentLanguage = LanguagePreference.SystemValue;

    public SettingsPage()
    {
        InitializeComponent();
        TitleText.Text = _localization.GetString("Navigation_Settings");
        Apply(_settingsService.Load());
        ClearDataButton.Content = _localization.GetString("DataManagement_Clear");
    }
    private void OnRestore(object sender, RoutedEventArgs e) { var settings = _settingsService.Update(current => { current.Theme = "System"; current.DecimalPlaces = 2; current.AutoCalculate = false; current.Language = LanguagePreference.SystemValue; }); Apply(settings); StatusText.Text = _localization.GetString("Status_RestoredDefaults"); }
    private void OnSettingChanged(object sender, object e) { if (!_isApplying) SaveCurrentSettings(); }
    private void SaveCurrentSettings()
    {
        var previousLanguage = _currentLanguage;
        var settings = _settingsService.Update(current =>
        {
            current.Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
            current.DecimalPlaces = DecimalBox.SelectedIndex < 0 ? 2 : DecimalBox.SelectedIndex;
            current.AutoCalculate = AutoCalculateToggle.IsOn;
            current.Language = LanguagePreference.Normalize((LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        });
        _currentLanguage = settings.Language;
        ApplyTheme(settings.Theme);
        StatusText.Text = string.Equals(previousLanguage, settings.Language, StringComparison.Ordinal)
            ? _localization.GetString("Status_SettingsSaved")
            : _localization.GetString("Setting_Language_RestartHint");
    }
    private void Apply(AppSettings settings)
    {
        _isApplying = true;
        ThemeBox.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        DecimalBox.SelectedIndex = settings.DecimalPlaces;
        AutoCalculateToggle.IsOn = settings.AutoCalculate;
        var language = LanguagePreference.Normalize(settings.Language);
        LanguageBox.SelectedIndex = language switch { "zh-CN" => 1, "ja-JP" => 2, "en-US" => 3, _ => 0 };
        _currentLanguage = language;
        _isApplying = false;
        ApplyTheme(settings.Theme);
    }
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
