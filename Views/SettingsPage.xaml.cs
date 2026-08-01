using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

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
    }
    private void OnSave(object sender, RoutedEventArgs e) => SaveCurrentSettings();
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
    private static void ApplyTheme(string theme) { if (App.MainWindow?.Content is FrameworkElement root) root.RequestedTheme = theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default }; }
}
