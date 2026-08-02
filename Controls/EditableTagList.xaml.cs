using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;

namespace UrbanPlanToolbox.Controls;

public sealed partial class EditableTagList : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(EditableTagList), new PropertyMetadata(string.Empty, OnLabelChanged));
    public static readonly DependencyProperty AddButtonTextProperty = DependencyProperty.Register(nameof(AddButtonText), typeof(string), typeof(EditableTagList), new PropertyMetadata(string.Empty, OnAddButtonTextChanged));
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(EditableTagList), new PropertyMetadata(string.Empty, OnPlaceholderChanged));
    public ObservableCollection<string> Values { get; } = [];
    public event EventHandler? ValuesChanged;
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string AddButtonText { get => (string)GetValue(AddButtonTextProperty); set => SetValue(AddButtonTextProperty, value); }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    public EditableTagList()
    {
        InitializeComponent();
        ItemsView.ItemsSource = Values;
    }

    public void SetValues(IEnumerable<string> values)
    {
        Values.Clear();
        foreach (var value in values ?? []) AddValue(value, notify: false);
    }

    private static void OnLabelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((EditableTagList)sender).LabelTextBlock.Text = (string)args.NewValue;
    private static void OnAddButtonTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((EditableTagList)sender).AddButton.Content = (string)args.NewValue;
    private static void OnPlaceholderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((EditableTagList)sender).InputBox.PlaceholderText = (string)args.NewValue;
    private void OnAddClick(object sender, RoutedEventArgs e) => AddFromInput();
    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; AddFromInput(); } }
    private void AddFromInput() { if (AddValue(InputBox.Text, notify: true)) InputBox.Text = string.Empty; }
    private bool AddValue(string? value, bool notify)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || Values.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return false;
        Values.Add(normalized);
        if (notify) ValuesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } && Values.Remove(value)) ValuesChanged?.Invoke(this, EventArgs.Empty);
    }
}
