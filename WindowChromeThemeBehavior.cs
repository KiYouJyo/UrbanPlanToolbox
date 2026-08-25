using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UrbanPlanToolbox;

/// <summary>
/// Keeps the custom title-bar surface and native caption-button chrome on the same
/// semantic active/inactive shell colors as the NavigationView pane. This avoids
/// leaving title-bar color resolution to machine-dependent Windows/DWM defaults.
/// </summary>
public static class WindowChromeThemeBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(WindowChromeThemeBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly ConditionalWeakTable<FrameworkElement, Subscription> Subscriptions = new();

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        if (e.NewValue is true)
        {
            element.Loaded += OnElementLoaded;
            element.Unloaded += OnElementUnloaded;
            if (element.IsLoaded) Attach(element);
        }
        else
        {
            element.Loaded -= OnElementLoaded;
            element.Unloaded -= OnElementUnloaded;
            Detach(element);
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) Attach(element);
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element) Detach(element);
    }

    private static void Attach(FrameworkElement element)
    {
        var state = Subscriptions.GetOrCreateValue(element);
        if (state.IsAttached) return;

        state.IsAttached = true;
        state.IsWindowActive = true;
        state.WindowActivatedHandler = (_, args) =>
        {
            state.IsWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
            Apply(element, state);
        };
        state.ThemeChangedHandler = (_, _) => Apply(element, state);

        App.MainWindow.Activated += state.WindowActivatedHandler;
        element.ActualThemeChanged += state.ThemeChangedHandler;
        Apply(element, state);
    }

    private static void Detach(FrameworkElement element)
    {
        if (!Subscriptions.TryGetValue(element, out var state) || !state.IsAttached) return;

        if (state.WindowActivatedHandler is not null)
            App.MainWindow.Activated -= state.WindowActivatedHandler;
        if (state.ThemeChangedHandler is not null)
            element.ActualThemeChanged -= state.ThemeChangedHandler;

        state.IsAttached = false;
        state.WindowActivatedHandler = null;
        state.ThemeChangedHandler = null;
    }

    private static void Apply(FrameworkElement element, Subscription state)
    {
        var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        var themeKey = highContrast
            ? "HighContrast"
            : element.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        var themeResources = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (themeResources is null) return;

        var brushKey = state.IsWindowActive
            ? "ShellNavigationPaneBackgroundBrush"
            : "ShellNavigationPaneInactiveBackgroundBrush";
        if (element is Panel panel && themeResources[brushKey] is Brush surfaceBrush)
            panel.Background = surfaceBrush;

        if (!AppWindowTitleBar.IsCustomizationSupported()) return;
        var titleBar = App.MainWindow.AppWindow.TitleBar;

        if (highContrast)
        {
            // High Contrast remains wholly system-driven.
            titleBar.BackgroundColor = null;
            titleBar.InactiveBackgroundColor = null;
            titleBar.ButtonBackgroundColor = null;
            titleBar.ButtonInactiveBackgroundColor = null;
            return;
        }

        if (themeResources["ShellNavigationPaneBackgroundBrush"] is not SolidColorBrush activeBrush ||
            themeResources["ShellNavigationPaneInactiveBackgroundBrush"] is not SolidColorBrush inactiveBrush)
            return;

        titleBar.BackgroundColor = activeBrush.Color;
        titleBar.InactiveBackgroundColor = inactiveBrush.Color;
        titleBar.ButtonBackgroundColor = activeBrush.Color;
        titleBar.ButtonInactiveBackgroundColor = inactiveBrush.Color;
    }

    private sealed class Subscription
    {
        public bool IsAttached { get; set; }
        public bool IsWindowActive { get; set; } = true;
        public TypedEventHandler<object, WindowActivatedEventArgs>? WindowActivatedHandler { get; set; }
        public TypedEventHandler<FrameworkElement, object>? ThemeChangedHandler { get; set; }
    }
}
