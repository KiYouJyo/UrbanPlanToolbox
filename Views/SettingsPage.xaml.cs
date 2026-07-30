using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;
public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private bool _isApplying;
    public SettingsPage() { InitializeComponent(); Apply(_settingsService.Load()); }
    private void OnSave(object sender, RoutedEventArgs e) => SaveCurrentSettings();
    private void OnRestore(object sender, RoutedEventArgs e) { var settings = new AppSettings(); _settingsService.Save(settings); Apply(settings); StatusText.Text = "已恢复默认设置。"; }
    private void OnSettingChanged(object sender, object e) { if (!_isApplying) SaveCurrentSettings(); }
    private void SaveCurrentSettings() { var settings = new AppSettings { Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System", DecimalPlaces = DecimalBox.SelectedIndex < 0 ? 2 : DecimalBox.SelectedIndex, AutoCalculate = AutoCalculateToggle.IsOn }; _settingsService.Save(settings); ApplyTheme(settings.Theme); StatusText.Text = "设置已保存。"; }
    private void Apply(AppSettings settings) { _isApplying = true; ThemeBox.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 }; DecimalBox.SelectedIndex = settings.DecimalPlaces; AutoCalculateToggle.IsOn = settings.AutoCalculate; _isApplying = false; ApplyTheme(settings.Theme); }
    private static void ApplyTheme(string theme) { if (App.MainWindow?.Content is FrameworkElement root) root.RequestedTheme = theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default }; }
}
