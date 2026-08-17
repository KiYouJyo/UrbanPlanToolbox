using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;
public sealed partial class InspirationRecorderWindow : Window
{
    private readonly InspirationService _service = InspirationService.Default; private List<Inspiration> _items=[]; private InspirationDraft? _draft; private int _index; private bool _loading,_allowClose,_isTextComposing;
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    private string _theme = "System";
    public bool IsVisible { get; private set; }
    public InspirationRecorderWindow() { InitializeComponent(); ExtendsContentIntoTitleBar=true; SetTitleBar(RecorderTitleBar); CategoryBox.Items.Add(new Choice(InspirationCategory.Design,"")); CategoryBox.Items.Add(new Choice(InspirationCategory.Research,"")); OpenFullButton.Content=new FontIcon{Glyph="\uE8A7",FontSize=12}; PreviousButton.Content="‹"; NextButton.Content="›"; NewButton.Content="+"; AppWindow.Resize(new Windows.Graphics.SizeInt32(560,560)); AppWindow.IsShownInSwitchers=false; if(AppWindow.Presenter is OverlappedPresenter p){p.IsResizable=false;p.IsMaximizable=false;p.IsMinimizable=false;p.IsAlwaysOnTop=true;} AppWindow.Closing+=OnClosing; Activated+=async(_,_)=>await RefreshAsync(); _uiSettings.ColorValuesChanged+=OnSystemColorValuesChanged; LocalizationService.Default.LanguageChanged+=OnLanguageChanged; SettingsService.SettingsChanged+=OnSettingsChanged; RefreshLocalizedStrings(); ApplyTheme(new SettingsService().Load().Theme); }
    public async Task RefreshAsync(){_items=(await _service.ListAsync()).ToList();_draft=await _service.GetDraftAsync()??new InspirationDraft();_index=Math.Min(_index,_items.Count);ShowCurrent();}
    public async Task OpenInspirationAsync(Guid id){_items=(await _service.ListAsync()).ToList();_draft=await _service.GetDraftAsync()??new InspirationDraft();_index=_items.FindIndex(x=>x.Id==id);if(_index<0)_index=_items.Count;ShowRecorder();ShowCurrent();}
    public void HideRecorder(){AppWindow.Hide();IsVisible=false;} public void ShowRecorder(bool moveToPrimaryWorkAreaTopRight=false){if(moveToPrimaryWorkAreaTopRight)MoveToPrimaryWorkAreaTopRight();AppWindow.Show();Activate();IsVisible=true;} public void CloseForExit(){_allowClose=true;Close();}
    private void MoveToPrimaryWorkAreaTopRight(){var display=DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(int.MinValue,int.MinValue),DisplayAreaFallback.Primary);AppWindow.Move(RecorderPlacement.CalculatePrimaryWorkAreaTopRight(display.WorkArea,AppWindow.Size));}
    private void OnClosing(AppWindow s,AppWindowClosingEventArgs e){if(_allowClose)return;e.Cancel=true;HideRecorder();App.HideInspirationRecorder();}
    private void OnLanguageChanged(object? s,LanguageChangedEventArgs e)=>DispatcherQueue.TryEnqueue(()=>{RefreshLocalizedStrings();ShowCurrent();});
    private void OnSettingsChanged(object? s,AppSettings settings)=>DispatcherQueue.TryEnqueue(()=>ApplyTheme(settings.Theme));
    private void ApplyTheme(string theme)
    {
        _theme = theme;
        var preference = SettingsService.NormalizeTheme(theme);
        var resolved = WindowIconTheme.Resolve(theme, SystemUsesLightTheme());
        RootLayout.RequestedTheme = preference == AppTheme.Dark ? ElementTheme.Dark : preference == AppTheme.Light ? ElementTheme.Light : ElementTheme.Default;
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.PreferredTheme = resolved == AppTheme.Dark ? TitleBarTheme.Dark : TitleBarTheme.Light;
            ApplyCaptionButtonColors(resolved);
        }
    }

    private void ApplyCaptionButtonColors(AppTheme resolvedTheme)
    {
        var lightGlyph = resolvedTheme == AppTheme.Dark;
        AppWindow.TitleBar.ButtonForegroundColor = lightGlyph ? Colors.White : Colors.Black;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = lightGlyph
            ? ColorHelper.FromArgb(110, 255, 255, 255)
            : ColorHelper.FromArgb(110, 0, 0, 0);
        // Keep the native close hover/pressed glyph white on the Fluent red
        // background in both themes.
        AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
    }

    private void OnSystemColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        if (SettingsService.NormalizeTheme(_theme) != AppTheme.System) return;
        DispatcherQueue.TryEnqueue(() => ApplyTheme(_theme));
    }

    private bool SystemUsesLightTheme()
    {
        var background = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        return (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B) >= 128;
    }
    private void RefreshLocalizedStrings(){var t=LocalizationService.Default;var selected=(CategoryBox.SelectedItem as Choice)?.Category??_draft?.Category??InspirationCategory.Design;var wasLoading=_loading;_loading=true;Title=t.GetString("Inspiration_RecorderTitle");TitleBox.Header=t.GetString("Inspiration_Title");ContentBox.Header=t.GetString("Inspiration_Details");CategoryBox.Header=t.GetString("Inspiration_Category");CategoryBox.Items.Clear();CategoryBox.Items.Add(new Choice(InspirationCategory.Design,t.GetString("Inspiration_CategoryDesign")));CategoryBox.Items.Add(new Choice(InspirationCategory.Research,t.GetString("Inspiration_CategoryResearch")));CategoryBox.SelectedItem=CategoryBox.Items.OfType<Choice>().First(x=>x.Category==selected);DeleteButton.Content=t.GetString("Inspiration_Delete");SaveButton.Content=t.GetString("Inspiration_Save");Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(OpenFullButton,t.GetString("Inspiration_OpenFull"));_loading=wasLoading;}
    private void ShowCurrent(){if(_draft is null)return;_loading=true;var draft=_index==_items.Count;var item=draft?null:_items[_index];TitleBox.Text=item?.Title??_draft.Title;ContentBox.Text=item?.Content??_draft.Content;CategoryBox.SelectedItem=CategoryBox.Items.OfType<Choice>().First(x=>x.Category==(item?.Category??_draft.Category));var t=LocalizationService.Default;var detail=draft?t.GetString("Inspiration_StatusNew")+(_draft.IsDirty?" · "+t.GetString("Inspiration_StatusUnsaved"):""):string.Format(t.GetString("Inspiration_Position"),_index+1,_items.Count);StatusText.Text=$"{t.GetString("Inspiration_RecorderTitle")} · {detail}";PreviousButton.IsEnabled=_index>0;NextButton.IsEnabled=_index<_items.Count;NewButton.IsEnabled=!draft;DeleteButton.IsEnabled=!draft;SaveButton.IsEnabled=!string.IsNullOrWhiteSpace(TitleBox.Text);_loading=false;}
    private async void OnChanged(object s,object e){if(_loading)return;var c=(CategoryBox.SelectedItem as Choice)?.Category??InspirationCategory.Design;if(_index==_items.Count){_draft!.Title=TitleBox.Text;_draft.Content=ContentBox.Text;_draft.Category=c;await _service.SaveDraftAsync(_draft);}else{var i=_items[_index];i.Title=TitleBox.Text;i.Content=ContentBox.Text;i.Category=c;}if(s is TextBox){SaveButton.IsEnabled=!string.IsNullOrWhiteSpace(TitleBox.Text);return;}ShowCurrent();}
    private async void OnSave(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(TitleBox.Text))return;if(_index==_items.Count)await _service.SaveDraftAsInspirationAsync();else await _service.SaveAsync(_items[_index]);await RefreshAsync();_index=Math.Max(0,_items.Count-1);ShowCurrent();}
    private async Task PersistAsync(){if(_index<_items.Count&&!string.IsNullOrWhiteSpace(TitleBox.Text))await _service.SaveAsync(_items[_index]);} private async void OnPrevious(object s,RoutedEventArgs e){if(_index>0){await PersistAsync();_index--;ShowCurrent();}} private async void OnNext(object s,RoutedEventArgs e){if(_index<_items.Count){await PersistAsync();_index++;ShowCurrent();}} private async void OnNew(object s,RoutedEventArgs e){if(_index!=_items.Count){await PersistAsync();_draft=new InspirationDraft{Category=(CategoryBox.SelectedItem as Choice)?.Category??InspirationCategory.Design};await _service.SaveDraftAsync(_draft);_index=_items.Count;ShowCurrent();}}
    private async void OnDelete(object s,RoutedEventArgs e){if(_index>=_items.Count)return;await _service.DeleteAsync(_items[_index].Id);await RefreshAsync();} private void OnOpenFull(object s,RoutedEventArgs e)=>App.OpenInspirationManagement((CategoryBox.SelectedItem as Choice)?.Category??InspirationCategory.Design); private void OnTextCompositionStarted(TextBox s,TextCompositionStartedEventArgs e)=>_isTextComposing=true; private void OnTextCompositionEnded(TextBox s,TextCompositionEndedEventArgs e)=>_isTextComposing=false; private void OnKeyDown(object s,KeyRoutedEventArgs e){if(!_isTextComposing&&e.Key==Windows.System.VirtualKey.Enter&&Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)){e.Handled=true;OnSave(s,e);}} private sealed class Choice(InspirationCategory category,string name){public InspirationCategory Category{get;}=category;public string Name{get;set;}=name;public override string ToString()=>Name;}
}
