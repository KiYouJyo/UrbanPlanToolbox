using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using UrbanPlanToolbox.Services;
using UrbanPlanToolbox.Models.Interaction;
using Windows.System;
using Windows.Storage;

namespace UrbanPlanToolbox.Views;

public sealed partial class FirstRunGuideHost : UserControl
{
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly IFirstRunExperienceService _stateService;
    private int _step;
    private bool _isBusy;

    public event EventHandler? Closed;

    public FirstRunGuideHost()
        : this(new FirstRunExperienceService())
    {
    }

    public FirstRunGuideHost(IFirstRunExperienceService stateService)
    {
        _stateService = stateService;
        InitializeComponent();
        _localization.LanguageChanged += OnLanguageChanged;
        Visibility = Visibility.Collapsed;
        RefreshText();
    }

    public void Show(bool manual)
    {
        if (Visibility == Visibility.Visible) return;
        _step = 0;
        _isBusy = false;
        RefreshText();
        Visibility = Visibility.Visible;
        Focus(FocusState.Programmatic);
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
        CloseButton.Content = _localization.GetString("FirstRunGuide_Close");
        PrivacyButton.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Content = _localization.GetString("FirstRunGuide_Skip");
        BackButton.Content = _localization.GetString("FirstRunGuide_Back");
        NextButton.Content = _localization.GetString(_step == 3 ? "FirstRunGuide_Start" : "FirstRunGuide_Next");
        BackButton.IsEnabled = _step > 0;
        SkipButton.Visibility = _step < 3 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(GuideTitle, GuideTitle.Text);
        AutomationProperties.SetName(GuideBody, GuideBody.Text);
    }

    private string[] FeatureKeys(string prefix, int count) => Enumerable.Range(1, count).Select(i => _localization.GetString($"{prefix}{i}")).ToArray();

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        if (_step < 3) { _step++; RefreshText(); GuideTitle.Focus(FocusState.Programmatic); return; }
        CompleteAndClose();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _step == 0) return;
        _step--; RefreshText(); GuideTitle.Focus(FocusState.Programmatic);
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        CompleteAndClose();
    }

    private void CompleteAndClose()
    {
        if (_isBusy) return;
        _isBusy = true;
        if (_stateService.TryMarkCompleted(out _)) CloseGuide();
        else _isBusy = false;
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseGuide();

    private async void OnPrivacy(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PRIVACY.md");
        var launched = File.Exists(path) && await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
        if (!launched)
            AppNotificationService.Default.Notify(new(AppNotificationKind.Error, _localization.GetString("Dialog_OpenFailedTitle"), _localization.GetString("Error_OpenDocumentFailed")));
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { CloseGuide(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space) { OnNext(NextButton, new RoutedEventArgs()); e.Handled = true; }
    }

    private void CloseGuide()
    {
        Visibility = Visibility.Collapsed;
        _isBusy = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshText);
    }
}
