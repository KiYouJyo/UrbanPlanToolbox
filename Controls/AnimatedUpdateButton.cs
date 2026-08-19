using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UrbanPlanToolbox.Controls;

/// <summary>
/// Button that preserves the normal Button API while replacing its text content with
/// an About-page style progress ring only while a click-driven update operation is busy.
/// External disabling (for example while another management action is running) does not
/// start the animation.
/// </summary>
public sealed class AnimatedUpdateButton : Button
{
    private StackPanel? _contentPanel;
    private ProgressRing? _progressRing;
    private TextBlock? _label;
    private bool _updatingContent;
    private bool _clickedBusy;

    public AnimatedUpdateButton()
    {
        Click += OnOwnClick;
        RegisterPropertyChangedCallback(ContentProperty, OnContentChanged);
        RegisterPropertyChangedCallback(IsEnabledProperty, OnIsEnabledChanged);
    }

    private void OnOwnClick(object sender, RoutedEventArgs e)
    {
        _clickedBusy = true;
        UpdateProgressVisual();
    }

    private void OnIsEnabledChanged(DependencyObject sender, DependencyProperty property)
    {
        if (IsEnabled)
        {
            _clickedBusy = false;
        }

        UpdateProgressVisual();
    }

    private void OnContentChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_updatingContent || ReferenceEquals(Content, _contentPanel)) return;

        var text = Content?.ToString() ?? string.Empty;
        EnsureContentPanel();
        _label!.Text = text;

        _updatingContent = true;
        try
        {
            Content = _contentPanel;
        }
        finally
        {
            _updatingContent = false;
        }
    }

    private void EnsureContentPanel()
    {
        if (_contentPanel is not null) return;

        _progressRing = new ProgressRing
        {
            Width = 16,
            Height = 16,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        _contentPanel.Children.Add(_progressRing);
        _contentPanel.Children.Add(_label);
    }

    private void UpdateProgressVisual()
    {
        EnsureContentPanel();
        var active = _clickedBusy && !IsEnabled;
        _progressRing!.IsActive = active;
        _progressRing.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }
}
