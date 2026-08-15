using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;
public sealed partial class InspirationRecorderWindow : Window
{
    private readonly InspirationService _service = InspirationService.Default; private List<Inspiration> _items = []; private InspirationDraft? _draft; private int _index; private bool _loading; private bool _topmost; private bool _allowClose;
    public bool IsVisible { get; private set; }
    public InspirationRecorderWindow()
    {
        InitializeComponent(); var text = LocalizationService.Default; TitleBox.Header = text.GetString("Inspiration_Title"); ContentBox.Header = text.GetString("Inspiration_Details"); ProjectBox.Header = text.GetString("Inspiration_LinkedProject"); NewButton.Content = text.GetString("Inspiration_New"); DeleteButton.Content = text.GetString("Inspiration_Delete"); SaveButton.Content = text.GetString("Inspiration_Save"); AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 520)); if (AppWindow.Presenter is OverlappedPresenter p) { p.IsResizable = true; _topmost = new SettingsService().Load().InspirationRecorderAlwaysOnTop; p.IsAlwaysOnTop = _topmost; } AppWindow.Closing += OnClosing;
        Activated += async (_, _) => await RefreshAsync();
    }
    public async Task RefreshAsync() { _items = (await _service.ListAsync()).ToList(); _draft = await _service.GetDraftAsync() ?? new InspirationDraft(); _index = _items.Count; await LoadProjectsAsync(); ShowCurrent(); }
    public async Task OpenInspirationAsync(Guid id)
    {
        _items = (await _service.ListAsync()).ToList(); _draft = await _service.GetDraftAsync() ?? new InspirationDraft();
        _index = _items.FindIndex(item => item.Id == id); if (_index < 0) _index = _items.Count; await LoadProjectsAsync(); ShowRecorder(); ShowCurrent();
    }
    public void HideRecorder() { AppWindow.Hide(); IsVisible = false; }
    public void ShowRecorder() { AppWindow.Show(); Activate(); IsVisible = true; }
    public void CloseForExit() { _allowClose = true; Close(); }
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args) { if (_allowClose) return; args.Cancel = true; HideRecorder(); App.HideInspirationRecorder(); }
    private void ShowCurrent() { _loading = true; var isDraft = _index == _items.Count; var item = isDraft ? null : _items[_index]; TitleBox.Text = item?.Title ?? _draft!.Title; ContentBox.Text = item?.Content ?? _draft!.Content; CategoryBox.SelectedIndex = (int)(item?.Category ?? _draft!.Category); ProjectBox.Visibility = isDraft ? Visibility.Collapsed : Visibility.Visible; ProjectBox.SelectedItem = ProjectBox.Items.OfType<ProjectChoice>().FirstOrDefault(choice => choice.Id == item?.LinkedProjectId) ?? ProjectBox.Items[0]; var text = LocalizationService.Default; StatusText.Text = isDraft ? $"◈ {text.GetString("Inspiration_StatusNew")}{(_draft!.IsDirty ? " · " + text.GetString("Inspiration_StatusUnsaved") : string.Empty)}" : $"◈ {_index + 1} / {_items.Count}"; PreviousButton.IsEnabled = _index > 0; NextButton.IsEnabled = _index < _items.Count; NewButton.IsEnabled = !isDraft; DeleteButton.IsEnabled = !isDraft; SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text); _loading = false; }
    private async void OnChanged(object sender, object e) { if (_loading) return; if (_index == _items.Count) { _draft!.Title = TitleBox.Text; _draft.Content = ContentBox.Text; _draft.Category = (InspirationCategory)CategoryBox.SelectedIndex; await _service.SaveDraftAsync(_draft); } else { var item = _items[_index]; item.Title = TitleBox.Text; item.Content = ContentBox.Text; item.Category = (InspirationCategory)CategoryBox.SelectedIndex; item.LinkedProjectId = (ProjectBox.SelectedItem as ProjectChoice)?.Id; } ShowCurrent(); }
    private async void OnSave(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(TitleBox.Text)) return; if (_index == _items.Count) await _service.SaveDraftAsInspirationAsync(); else { var item = _items[_index]; item.Title = TitleBox.Text; item.Content = ContentBox.Text; item.Category = (InspirationCategory)CategoryBox.SelectedIndex; item.LinkedProjectId = (ProjectBox.SelectedItem as ProjectChoice)?.Id; await _service.SaveAsync(item); } await RefreshAsync(); _index = Math.Max(0, _items.Count - 1); ShowCurrent(); }
    private async void OnPrevious(object sender, RoutedEventArgs e) { if (_index > 0) { await SaveCurrentIfNeededAsync(); _index--; ShowCurrent(); } }
    private async void OnNext(object sender, RoutedEventArgs e) { if (_index < _items.Count) { await SaveCurrentIfNeededAsync(); _index++; ShowCurrent(); } }
    private async void OnNew(object sender, RoutedEventArgs e) { if (_index != _items.Count) { await SaveCurrentIfNeededAsync(); _draft = new InspirationDraft { Category = (InspirationCategory)CategoryBox.SelectedIndex }; await _service.SaveDraftAsync(_draft); _index = _items.Count; ShowCurrent(); } }
    private void OnHide(object sender, RoutedEventArgs e) => App.HideInspirationRecorder();
    private void OnPin(object sender, RoutedEventArgs e) { _topmost = !_topmost; if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = _topmost; new SettingsService().Update(s => s.InspirationRecorderAlwaysOnTop = _topmost); }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) OnSave(sender, e); }
    private async Task SaveCurrentIfNeededAsync() { if (_index < _items.Count && !string.IsNullOrWhiteSpace(TitleBox.Text)) await _service.SaveAsync(_items[_index]); }
    private async void OnDelete(object sender, RoutedEventArgs e) { if (_index >= _items.Count) return; await _service.DeleteAsync(_items[_index].Id); await RefreshAsync(); }
    private async Task LoadProjectsAsync()
    {
        var projects = await ProjectStorageService.Default.ListAsync(false); ProjectBox.Items.Clear(); ProjectBox.Items.Add(new ProjectChoice(null, LocalizationService.Default.GetString("Inspiration_Unlinked")));
        var kind = CategoryBox.SelectedIndex == (int)InspirationCategory.Research ? ProjectKindCodes.Research : ProjectKindCodes.Design;
        foreach (var project in projects.Projects.Where(project => project.Kind == kind)) ProjectBox.Items.Add(new ProjectChoice(project.Id, project.Name));
    }
    private sealed record ProjectChoice(Guid? Id, string Name) { public override string ToString() => Name; }
}
