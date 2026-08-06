using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Interaction;
using UrbanPlanToolbox.Services;
using Windows.Storage;
using Windows.System;

namespace UrbanPlanToolbox.Views;

/// <summary>
/// The single reusable visual surface hosted by MainWindow. It does not own
/// navigation or create windows; the window coordinates its lifetime.
/// </summary>
public sealed partial class FirstRunGuideHost : UserControl
{
    private const double PreferredWidth = 760;
    private const double PreferredHeight = 520;
    private const double MinimumWidth = 320;
    private const double MinimumHeight = 360;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly IFirstRunExperienceService _stateService;
    private int _step;
    private bool _isBusy;

    public event EventHandler? Closed;

    public FirstRunGuideHost() : this(FirstRunExperienceService.Default) { }

    public FirstRunGuideHost(IFirstRunExperienceService stateService)
    {
        _stateService = stateService;
        InitializeComponent();
        _localization.LanguageChanged += OnLanguageChanged;
        Visibility = Visibility.Collapsed;
        RefreshText();
    }

    public int CurrentStep => _step;

    public void Show(FirstRunGuideLaunchMode mode)
    {
        if (Visibility == Visibility.Visible) return;

        _step = 0;
        _isBusy = false;
        RefreshText();
        Visibility = Visibility.Visible;
        UpdateGuideSize();
        DispatcherQueue.TryEnqueue(FocusFirstControl);
    }

    private void RefreshText()
    {
        var suffix = (_step + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        StepText.Text = _localization.GetFormattedString("FirstRunGuide_Step", suffix);
        GuideTitle.Text = _localization.GetString($"FirstRunGuide_Step{_step + 1}Title");
        GuideBody.Text = _localization.GetString($"FirstRunGuide_Step{_step + 1}Body");
        FeatureList.ItemsSource = _step switch
        {
            1 => FeatureKeys("FirstRunGuide_ProjectFeature", 5),
            2 => FeatureKeys("FirstRunGuide_ToolFeature", 3),
            _ => Array.Empty<string>()
        };
        PrivacyButton.Content = _localization.GetString("FirstRunGuide_PrivacyLink");
        PrivacyButton.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Content = _localization.GetString("FirstRunGuide_Skip");
        BackButton.Content = _localization.GetString("FirstRunGuide_Back");
        NextButton.Content = _localization.GetString(_step == 3 ? "FirstRunGuide_Start" : "FirstRunGuide_Next");
        BackButton.IsEnabled = _step > 0;
        SkipButton.Visibility = _step < 3 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(GuideTitle, GuideTitle.Text);
        AutomationProperties.SetName(GuideBody, GuideBody.Text);
        AutomationProperties.SetName(OverlayRoot, GuideTitle.Text);
    }

    private string[] FeatureKeys(string prefix, int count) =>
        Enumerable.Range(1, count).Select(i => _localization.GetString($"{prefix}{i}")).ToArray();

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (_step < 3)
        {
            _step++;
            RefreshText();
            DispatcherQueue.TryEnqueue(FocusFirstControl);
            return;
        }

        CompleteAndClose();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _step == 0) return;
        _step--;
        RefreshText();
        DispatcherQueue.TryEnqueue(FocusFirstControl);
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if (!_isBusy) CompleteAndClose();
    }

    private void CompleteAndClose()
    {
        if (_isBusy) return;
        _isBusy = true;
        if (_stateService.TryMarkCompleted(out _)) CloseGuide();
        else _isBusy = false;
    }

    private async void OnPrivacy(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PRIVACY.md");
        var launched = File.Exists(path) && await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
        if (!launched)
        {
            AppNotificationService.Default.Notify(new(
                AppNotificationKind.Error,
                _localization.GetString("Dialog_OpenFailedTitle"),
                _localization.GetString("Error_OpenDocumentFailed")));
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            // Escape dismisses without completing, so an interrupted automatic
            // experience is offered again on the next launch.
            CloseGuide();
            e.Handled = true;
        }
    }

    private void OnGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (Visibility != Visibility.Visible || args.NewFocusedElement is null) return;
        if (args.NewFocusedElement is DependencyObject element && !IsDescendantOfOverlay(element))
        {
            args.TryCancel();
            FocusFirstControl();
        }
    }

    private bool IsDescendantOfOverlay(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, OverlayRoot)) return true;
        return false;
    }

    private void FocusFirstControl()
    {
        if (Visibility != Visibility.Visible) return;
        var target = PrivacyButton.Visibility == Visibility.Visible ? PrivacyButton : NextButton;
        target.Focus(FocusState.Programmatic);
    }

    private void CloseGuide()
    {
        Visibility = Visibility.Collapsed;
        _isBusy = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateGuideSize();

    private void UpdateGuideSize()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        GuideCard.Width = Math.Min(PreferredWidth, Math.Max(MinimumWidth, ActualWidth - 48));
        GuideCard.Height = Math.Min(PreferredHeight, Math.Max(MinimumHeight, ActualHeight - 48));
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        // Refresh in place: keep the current step, mode and visibility. The
        // fixed card dimensions are not recalculated from content.
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshText();
            if (Visibility == Visibility.Visible) DispatcherQueue.TryEnqueue(FocusFirstControl);
        });
    }
}
