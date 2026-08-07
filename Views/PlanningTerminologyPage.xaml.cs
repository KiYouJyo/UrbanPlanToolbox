using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

public sealed partial class PlanningTerminologyPage : Page
{
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly IPlanningTerminologyService _service = PlanningTerminologyService.Default;
    private PlanningTerm? _selected;
    private bool _isNarrow;

    public PlanningTerminologyPage()
    {
        InitializeComponent();
        TitleText.Text = T("Tool_PlanningTerminology_Name"); DescriptionText.Text = T("Terminology_Description"); SearchBox.PlaceholderText = T("Terminology_SearchPlaceholder"); ResultsHeader.Text = T("Terminology_TermList"); DefinitionHeader.Text = T("Terminology_Definition"); RelatedHeader.Text = T("Terminology_Related"); ComparisonHeader.Text = T("Terminology_Comparison"); SourcesHeader.Text = T("Terminology_Sources"); CopyButton.Content = T("Terminology_Copy");
        DatasetText.Text = $"Dataset v{_service.Dataset.DataVersion}\n{_service.Dataset.Counts.Terms} terms"; JurisdictionBox.PlaceholderText = T("Terminology_Jurisdiction"); CategoryBox.PlaceholderText = T("Terminology_Category");
        JurisdictionBox.ItemsSource = new[] { new Choice(string.Empty, T("Terminology_All")), new Choice("通用", T("Terminology_JurisdictionGeneral")), new Choice("中国", T("Terminology_JurisdictionChina")), new Choice("日本", T("Terminology_JurisdictionJapan")) }; CategoryBox.ItemsSource = new[] { new Choice(string.Empty, T("Terminology_All")) }.Concat(_service.Dataset.Terms.Select(term => term.Category).Distinct(StringComparer.Ordinal).OrderBy(value => value).Select(value => new Choice(value, CategoryLabel(value)))).ToArray(); JurisdictionBox.SelectedIndex = 0; CategoryBox.SelectedIndex = 0;
        DefinitionLanguageBox.ItemsSource = new[] { new Choice("zh", T("Terminology_DefinitionZh")), new Choice("ja", T("Terminology_DefinitionJa")), new Choice("en", T("Terminology_DefinitionEn")) }; DefinitionLanguageBox.SelectedValue = _localization.CurrentLanguage switch { "ja-JP" => "ja", "en-US" => "en", _ => "zh" }; Root.SizeChanged += OnRootSizeChanged; Root.Loaded += (_, _) => ApplyResponsiveLayout(Root.ActualWidth); ErrorBar.Message = T("Terminology_LoadFailed"); ErrorBar.IsOpen = !_service.IsAvailable; DetailScroll.Visibility = Visibility.Collapsed; RenderResults();
    }
    private string T(string key) => _localization.GetString(key);
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderResults();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => RenderResults();
    private void RenderResults() { var results = _service.Search(SearchBox.Text, (JurisdictionBox.SelectedItem as Choice)?.Value, (CategoryBox.SelectedItem as Choice)?.Value); ResultsList.ItemsSource = results.Select(result => new ResultCard(result.Term, FormatEquivalence(result.Term.Equivalence))).ToArray(); EmptyText.Text = results.Count == 0 ? T("Terminology_NoResults") : string.Empty; }
    private void OnResultClick(object sender, RoutedEventArgs e) { if (sender is Button { Tag: ResultCard card }) ShowTerm(card.Term.Id); }
    private void ShowTerm(int id) { var term = _service.GetTerm(id); if (term is null) return; _selected = term; RenderDetail(); DetailScroll.Visibility = Visibility.Visible; if (_isNarrow) DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, BringDetailIntoView); }
    private void BringDetailIntoView() { DetailScroll.UpdateLayout(); DetailScroll.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0 }); }
    private void RenderDetail() { if (_selected is null) return; var term = _selected; DetailZh.Text = term.ZhCN; DetailJa.Text = term.JaJP; DetailReading.Text = string.IsNullOrWhiteSpace(term.JaReading) ? string.Empty : $"（{term.JaReading}）"; DetailEn.Text = term.EnUS; JurisdictionText.Text = term.Jurisdiction; EquivalenceText.Text = FormatEquivalence(term.Equivalence); DefinitionText.Text = CurrentDefinition(term); var reviewMessage = GetReviewMessage(term); ReviewBar.Message = reviewMessage; ReviewBar.Title = string.Empty; ReviewBar.IsOpen = !string.IsNullOrWhiteSpace(reviewMessage); RelatedList.ItemsSource = _service.GetRelatedTerms(term.Id).Select(pair => new RelatedCard(FormatRelation(pair.Relation.RelationType), pair.Term, pair.Relation)).ToArray(); ComparisonList.ItemsSource = _service.GetHighRiskEquivalences(term.Id).Select(item => $"{FormatEquivalence(item.Equivalence)}\n{FindTerm(item.TermA)?.ZhCN} ↔ {FindTerm(item.TermB)?.ZhCN}\n{item.NoteZh}").ToArray(); SourcesList.ItemsSource = _service.GetSources(term).Select(source => new SourceCard(source, T("Terminology_OpenSource"))).ToArray(); ComparisonHeader.Visibility = ComparisonList.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible; SourcesHeader.Visibility = SourcesList.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible; }
    private string CurrentDefinition(PlanningTerm term) => DefinitionLanguageBox.SelectedValue?.ToString() switch { "ja" => term.DefinitionJa, "en" => term.DefinitionEn, _ => term.DefinitionZh };
    private void OnDefinitionLanguageChanged(object sender, SelectionChangedEventArgs e) { if (_selected is not null) DefinitionText.Text = CurrentDefinition(_selected); }
    private void OnCopyClick(object sender, RoutedEventArgs e) { if (_selected is null) return; var data = new DataPackage(); data.SetText($"{_selected.ZhCN}\n{_selected.JaJP}{(string.IsNullOrWhiteSpace(_selected.JaReading) ? string.Empty : $"\n（{_selected.JaReading}）")}\n{_selected.EnUS}"); Clipboard.SetContent(data); AppNotificationService.Default.Notify(new(Models.Interaction.AppNotificationKind.Success, T("Terminology_Copied"), string.Empty)); }
    private void OnRelatedClick(object sender, RoutedEventArgs e) { if (sender is Button { Tag: RelatedCard card }) ShowTerm(card.Term.Id); }
    private async void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SourceCard { Source.Url: var url } }) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try
        {
            if (!await Launcher.LaunchUriAsync(uri)) AppLogger.Default.Warning(nameof(PlanningTerminologyPage), "source_launch_failed", uri.ToString());
        }
        catch (Exception exception)
        {
            AppLogger.Default.Warning(nameof(PlanningTerminologyPage), "source_launch_failed", exception.Message);
        }
    }
    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout(e.NewSize.Width);
    private void ApplyUnifiedContentHeight()
    {
        if (_isNarrow)
        {
            ContentGrid.ClearValue(FrameworkElement.HeightProperty);
            ResultsPanel.ClearValue(FrameworkElement.HeightProperty);
            ResultsPanel.ClearValue(FrameworkElement.MaxHeightProperty);
            ResultsList.ClearValue(FrameworkElement.HeightProperty);
            ResultsList.ClearValue(FrameworkElement.MaxHeightProperty);
            return;
        }

        var availableHeight = Root.ActualHeight - Root.Padding.Top - Root.Padding.Bottom - ContentGrid.Margin.Top;
        for (var index = 0; index < 4; index++) availableHeight -= Root.RowDefinitions[index].ActualHeight;
        if (availableHeight > 0)
        {
            ContentGrid.Height = availableHeight;
            ResultsList.Height = availableHeight;
        }
    }

    private void ApplyResponsiveLayout(double width)
    {
        var narrow = width > 0 && width < 800;
        var medium = width > 0 && width < 1100;
        _isNarrow = narrow;
        TitleText.FontSize = narrow ? 32 : medium ? 38 : 44;
        DescriptionText.FontSize = narrow ? 16 : medium ? 18 : 20;
        DatasetText.FontSize = narrow ? 13 : 14;
        if (narrow)
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            var availableHeight = Root.ActualHeight > 0 ? Root.ActualHeight : 700;
            var listHeight = Math.Clamp(availableHeight * 0.34, 260, 340);
            ContentGrid.RowDefinitions[0].Height = new GridLength(listHeight, GridUnitType.Pixel);
            ContentGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ResultsPanel, 0); Grid.SetColumn(ResultsPanel, 0); Grid.SetColumnSpan(ResultsPanel, 2);
            Grid.SetRow(DetailScroll, 1); Grid.SetColumn(DetailScroll, 0); Grid.SetColumnSpan(DetailScroll, 2);
            FilterGrid.RowDefinitions[0].Height = GridLength.Auto; FilterGrid.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(CategoryBox, 0); Grid.SetColumn(CategoryBox, 1);
            ResultsPanel.Visibility = Visibility.Visible;
            DetailScroll.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible;
            ApplyUnifiedContentHeight();
        }
        else
        {
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(medium ? 0.67 : 0.62, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star); ContentGrid.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(ResultsPanel, 0); Grid.SetColumn(ResultsPanel, 0); Grid.SetColumnSpan(ResultsPanel, 1);
            Grid.SetRow(DetailScroll, 0); Grid.SetColumn(DetailScroll, 1); Grid.SetColumnSpan(DetailScroll, 1);
            FilterGrid.RowDefinitions[0].Height = GridLength.Auto; FilterGrid.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(CategoryBox, 0); Grid.SetColumn(CategoryBox, 1);
            ResultsPanel.Visibility = Visibility.Visible;
            DetailScroll.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible;
            ApplyUnifiedContentHeight();
        }
    }
    private string FormatEquivalence(string value) => T(value switch { "exact" => "Terminology_Exact", "approximate" => "Terminology_Approximate", "translation-only" => "Terminology_TranslationOnly", "none" => "Terminology_None", _ => "Terminology_None" });
    private string FormatRelation(string value) => _localization.GetString($"Terminology_Relation_{value}") is { } label && !label.StartsWith("!Terminology_Relation_", StringComparison.Ordinal) ? label : value;
    private string GetReviewMessage(PlanningTerm term) => !string.IsNullOrWhiteSpace(term.ReviewNote) ? term.ReviewNote : term.ReleaseStatus.Contains("标准化", StringComparison.Ordinal) ? T("Terminology_StandardizationWarning") : term.ReleaseStatus.Contains("警告", StringComparison.Ordinal) ? T("Terminology_ReviewWarning") : string.Empty;
    private string CategoryLabel(string value) => T(value switch { "规划基础" => "Terminology_Category_PlanningBasics", "中国国土空间制度" => "Terminology_Category_ChinaSystem", "日本国土与都市计划制度" => "Terminology_Category_JapanSystem", "开发指标" => "Terminology_Category_DevelopmentMetrics", "城市设计与更新" => "Terminology_Category_DesignRenewal", "交通与街道" => "Terminology_Category_Transport", "生态韧性防灾" => "Terminology_Category_EcologyDisaster", "GIS 与空间研究" => "Terminology_Category_GIS", _ => "Terminology_Category_PlanningBasics" });
    private PlanningTerm? FindTerm(string name) => _service.Dataset.Terms.FirstOrDefault(term => term.ZhCN == name || term.JaJP == name);
    private sealed record Choice(string Value, string Display); private sealed record ResultCard(PlanningTerm Term, string Badge) { public string ZhCN => Term.ZhCN; public string JaJP => Term.JaJP; public string EnUS => Term.EnUS; } private sealed record RelatedCard(string Label, PlanningTerm Term, TerminologyRelation Relation) { public string Name => $"{Term.ZhCN} · {Term.JaJP} · {Term.EnUS}"; } private sealed record SourceCard(TerminologySource Source, string OpenText) { public Visibility OpenVisibility => ExternalLinkService.IsSafeHttpUri(Source.Url, out _) ? Visibility.Visible : Visibility.Collapsed; }
}
