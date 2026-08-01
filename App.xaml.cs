using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using UrbanPlanToolbox.Services;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UrbanPlanToolbox;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindow { get; private set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var settings = new SettingsService().Load();
        ApplyLanguagePreference(settings);
        _window = MainWindow = new MainWindow();
        ThemePreference.Apply((FrameworkElement)_window.Content, settings.Theme);
        _window.Activate();
        // Defer disk and notification work until a complete first frame is available.
        _window.DispatcherQueue.TryEnqueue(async () =>
        {
            AppDataPathProvider.Default.EnsureInfrastructureDirectories();
            await MilestoneReminderService.Default.RefreshAsync();
        });
    }

    /// <summary>
    /// Applies the persisted language preference before the MainWindow and its
    /// localized resources are created. An empty override means "follow system".
    /// </summary>
    private static void ApplyLanguagePreference(Models.AppSettings settings)
    {
        ApplicationLanguages.PrimaryLanguageOverride = LanguagePreference.ResolveEffectiveLanguage(
            settings.Language,
            GlobalizationPreferences.Languages);
    }
}
