using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

/// <summary>The full, project-aware editor. The recorder deliberately remains only a compact view.</summary>
public sealed partial class InspirationManagementPage : Page
{
    private readonly InspirationService _service = InspirationService.Default;
    private InspirationCategory _category = InspirationCategory.Design;
    private Inspiration? _editing;
    private bool _isNew;
    private bool _loading;

    public InspirationManagementPage()
    {
        InitializeComponent();
        BackButton.Content = T("Action_Back"); NewButton.Content = T("Inspiration_New");
        SaveButton.Content = T("Inspiration_Save"); DeleteButton.Content = T("Inspiration_Delete");
        NameBox.Header = T("Inspiration_Title"); DetailsBox.Header = T("Inspiration_Details");
        CategoryBox.Header = T("Inspiration_Category"); ProjectBox.Header = T("Inspiration_LinkedProject");
        CategoryBox.Items.Add(new Choice(InspirationCategory.Design, T("Inspiration_CategoryDesign")));
        CategoryBox.Items.Add(new Choice(InspirationCategory.Research, T("Inspiration_CategoryResearch")));
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _category = string.Equals(e.Parameter as string, ToolIds.ResearchInspiration, StringComparison.Ordinal)
            ? InspirationCategory.Research : InspirationCategory.Design;
        TitleText.Text = T(_category == InspirationCategory.Design ? "Tool_DesignInspiration_Name" : "Tool_ResearchInspiration_Name");
        DescriptionText.Text = T(_category == InspirationCategory.Design ? "Tool_DesignInspiration_Description" : "Tool_ResearchInspiration_Description");
        await RefreshAsync();
        base.OnNavigatedTo(e);
    }

    private string T(string key) => LocalizationService.Default.GetString(key);
    private string T(string key, params object[] values) => string.Format(T(key), values);

    private async Task RefreshAsync()
    {
        var items = (await _service.ListAsync()).Where(item => item.Category == _category).OrderByDescending(item => item.UpdatedAt).ToArray();
        CardsPanel.Children.Clear();
        if (items.Length == 0)
        {
            CardsPanel.Children.Add(new TextBlock { Text = T("Inspiration_Empty"), TextWrapping = TextWrapping.Wrap });
            return;
        }
        var projects = await ProjectStorageService.Default.ListAsync(false);
        var names = projects.Projects.ToDictionary(project => project.Id, project => project.Name);
        foreach (var item in items) CardsPanel.Children.Add(CreateCard(item, names));
    }

    private Button CreateCard(Inspiration item, IReadOnlyDictionary<Guid, string> projectNames)
    {
        var button = new Button { Tag = item, Style = (Style)Application.Current.Resources["CardActionButtonStyle"], HorizontalContentAlignment = HorizontalAlignment.Stretch };
        button.Click += OnCardClick;
        var row = new Grid { ColumnSpacing = 16 }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(new TextBlock { Text = item.Title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = item.Content, MaxLines = 2, TextWrapping = TextWrapping.Wrap, Opacity = .78 });
        content.Children.Add(new TextBlock { Text = T("Inspiration_UpdatedAt", item.UpdatedAt.LocalDateTime.ToString("g")), Opacity = .65, TextWrapping = TextWrapping.Wrap });
        row.Children.Add(content);
        var association = item.LinkedProjectId is Guid id && projectNames.TryGetValue(id, out var name) ? name : T("Inspiration_Unlinked");
        var associationText = new TextBlock { Text = association, MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .65, VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(associationText, association); Grid.SetColumn(associationText, 1); row.Children.Add(associationText); button.Content = row;
        return button;
    }

    private void OnCardClick(object sender, RoutedEventArgs e) => OpenEditor(((Button)sender).Tag as Inspiration);
    private void OnNew(object sender, RoutedEventArgs e) => OpenEditor(new Inspiration { Id = Guid.NewGuid(), Category = _category, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }, true);

    private async void OpenEditor(Inspiration? item, bool isNew = false)
    {
        if (item is null) return;
        _editing = new Inspiration { Id = item.Id, Category = item.Category, Title = item.Title, Content = item.Content, LinkedProjectId = item.LinkedProjectId, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt };
        _isNew = isNew; _loading = true; ListPanel.Visibility = Visibility.Collapsed; EditorPanel.Visibility = Visibility.Visible;
        EditorTitle.Text = isNew ? T("Inspiration_New") : item.Title;
        BackButton.Content = T(item.Category == InspirationCategory.Design ? "Inspiration_ReturnDesign" : "Inspiration_ReturnResearch");
        NameBox.Text = item.Title; DetailsBox.Text = item.Content;
        CategoryBox.SelectedItem = CategoryBox.Items.OfType<Choice>().First(choice => choice.Category == item.Category);
        await LoadProjectsAsync(item.LinkedProjectId);
        CreatedText.Text = T("Inspiration_CreatedAt", item.CreatedAt.LocalDateTime.ToString("g"));
        UpdatedText.Text = T("Inspiration_UpdatedAt", item.UpdatedAt.LocalDateTime.ToString("g"));
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        _loading = false;
    }

    private async Task LoadProjectsAsync(Guid? selectedId)
    {
        ProjectBox.Items.Clear(); ProjectBox.Items.Add(new ProjectChoice(null, T("Inspiration_Unlinked")));
        var projects = await ProjectStorageService.Default.ListAsync(false);
        var kind = _editing?.Category == InspirationCategory.Research ? ProjectKindCodes.Research : ProjectKindCodes.Design;
        foreach (var project in projects.Projects.Where(project => project.Kind == kind)) ProjectBox.Items.Add(new ProjectChoice(project.Id, project.Name));
        ProjectBox.SelectedItem = ProjectBox.Items.OfType<ProjectChoice>().FirstOrDefault(choice => choice.Id == selectedId) ?? ProjectBox.Items[0];
    }

    private void OnChanged(object sender, object e)
    {
        if (_loading || _editing is null) return;
        _editing.Title = NameBox.Text; _editing.Content = DetailsBox.Text; _editing.LinkedProjectId = (ProjectBox.SelectedItem as ProjectChoice)?.Id;
    }

    private async void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _editing is null || CategoryBox.SelectedItem is not Choice choice) return;
        _editing.Category = choice.Category; _editing.LinkedProjectId = null; await LoadProjectsAsync(null);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        _editing.Title = NameBox.Text; _editing.Content = DetailsBox.Text; _editing.LinkedProjectId = (ProjectBox.SelectedItem as ProjectChoice)?.Id;
        if (string.IsNullOrWhiteSpace(_editing.Title)) { EditorStatus.Title = T("Inspiration_SaveFailed"); EditorStatus.IsOpen = true; return; }
        var saved = _isNew ? await _service.CreateAsync(_editing) : await _service.SaveAsync(_editing);
        if (!saved) { EditorStatus.Title = T("Inspiration_SaveFailed"); EditorStatus.IsOpen = true; return; }
        // Keep the current management page scoped to its original category. If the
        // category changed, the saved item moves to the other page on refresh.
        CloseEditor(); await RefreshAsync();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_editing is null || _isNew) return;
        await _service.DeleteAsync(_editing.Id); CloseEditor(); await RefreshAsync();
    }

    private async void OnBack(object sender, RoutedEventArgs e)
    {
        var targetCategory = _editing?.Category ?? _category;
        CloseEditor();
        if (targetCategory != _category) { App.OpenInspirationManagement(targetCategory); return; }
        await RefreshAsync();
    }
    private void CloseEditor() { _editing = null; _isNew = false; EditorPanel.Visibility = Visibility.Collapsed; ListPanel.Visibility = Visibility.Visible; }
    private sealed record Choice(InspirationCategory Category, string Name) { public override string ToString() => Name; }
    private sealed record ProjectChoice(Guid? Id, string Name) { public override string ToString() => Name; }
}
