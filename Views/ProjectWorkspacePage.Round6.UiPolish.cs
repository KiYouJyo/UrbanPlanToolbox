using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private readonly Dictionary<Guid, Border> _round6HookedTiles = [];
    private bool _round6Initialized;
    private bool _round6Applying;

    private void OnRound6WorkspaceLoaded(object sender, RoutedEventArgs e)
    {
        // Keep the existing repair / Round4 / Round5 initialization chain intact.
        OnRound1WorkspaceLoaded(sender, e);

        if (_round6Initialized)
        {
            ApplyRound6Polish();
            return;
        }

        _round6Initialized = true;
        TileCanvas.LayoutUpdated -= OnRound6CanvasLayoutUpdated;
        TileCanvas.LayoutUpdated += OnRound6CanvasLayoutUpdated;
        ActualThemeChanged -= OnRound6ActualThemeChanged;
        ActualThemeChanged += OnRound6ActualThemeChanged;
        DispatcherQueue.TryEnqueue(ApplyRound6Polish);
    }

    private void OnRound6CanvasLayoutUpdated(object? sender, object e) => ApplyRound6Polish();

    private void OnRound6ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyRound6StateBadge();
        ApplyRound6WorkspaceCardBackgrounds();
    }

    private void ApplyRound6Polish()
    {
        if (_round6Applying || _project is null) return;
        _round6Applying = true;
        try
        {
            ApplyRound6StateBadge();
            ApplyRound6WorkspaceCardBackgrounds();

            foreach (var stale in _round6HookedTiles.Keys.Where(id => !_tileViews.ContainsKey(id)).ToArray())
                _round6HookedTiles.Remove(stale);

            foreach (var pair in _tileViews.ToArray())
            {
                var panel = _project.WorkspaceLayout?.Panels.FirstOrDefault(item => item.Id == pair.Key);
                if (panel is null) continue;

                var tile = pair.Value;
                var isNewTile = !_round6HookedTiles.TryGetValue(panel.Id, out var hooked) || !ReferenceEquals(hooked, tile);
                if (!isNewTile) continue;

                switch (panel.Kind)
                {
                    case ProjectWorkspacePanelKinds.Description:
                        ReplaceRound4TileBody(tile, BuildRound6Description());
                        break;
                    case ProjectWorkspacePanelKinds.ResearchQuestion:
                        ReplaceRound4TileBody(tile, BuildRound6ResearchQuestion());
                        tile.ContextFlyout = CreateRound6ResearchQuestionMenu(panel);
                        break;
                }

                _round6HookedTiles[panel.Id] = tile;
            }
        }
        finally
        {
            _round6Applying = false;
        }
    }

    private void ApplyRound6WorkspaceCardBackgrounds()
    {
        // Dark mode has already been accepted visually. Preserve its exact runtime brush
        // assignment and only change the light-mode path below.
        if (ActualTheme != ElementTheme.Light)
        {
            var background = ResourceBrush("CardBackgroundFillColorDefaultBrush");
            OverviewCard.Background = background;
            foreach (var tile in _tileViews.Values)
                tile.Background = background;
            return;
        }

        // About and Settings first-level cards do not receive a code-behind brush override;
        // they are rendered through SettingsSectionCardStyle. In light mode the previous
        // ResourceBrush assignment became a local value and produced a visibly different
        // composited surface. Remove that local value and let the exact same style/theme
        // resource pipeline render the project cards.
        OverviewCard.ClearValue(Border.BackgroundProperty);
        var firstLevelStyle = (Style)Application.Current.Resources["SettingsSectionCardStyle"];
        foreach (var tile in _tileViews.Values)
        {
            tile.Style = firstLevelStyle;
            tile.ClearValue(Border.BackgroundProperty);
        }
    }

    private void ApplyRound6StateBadge()
    {
        if (_project is null) return;
        if (_project.IsArchived)
        {
            StateBadge.Background = ResourceBrush("SystemFillColorCautionBackgroundBrush");
            StateText.Foreground = ResourceBrush("SystemFillColorCautionBrush");
            return;
        }

        // Fluent's stock success-background token is intentionally strong. A project
        // state chip is persistent UI, so use a softer semantic green in light mode
        // while retaining sufficient contrast in dark mode.
        var dark = ActualTheme == ElementTheme.Dark;
        StateBadge.Background = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 38, 64, 45)
            : Windows.UI.Color.FromArgb(255, 229, 246, 232));
        StateText.Foreground = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 143, 221, 158)
            : Windows.UI.Color.FromArgb(255, 30, 122, 55));
    }

    private UIElement BuildRound6Description()
    {
        var text = new TextBlock
        {
            Text = _project?.Description ?? W(
                "点击“编辑项目”添加项目说明。",
                "Use Edit project to add a description.",
                "「プロジェクトを編集」から説明を追加できます。"),
            TextWrapping = TextWrapping.Wrap,
            Opacity = .8
        };

        return new ScrollViewer
        {
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = text
        };
    }

    private UIElement BuildRound6ResearchQuestion()
    {
        var stack = new StackPanel { Spacing = 7 };
        var items = ParseRound6ResearchQuestions(_project?.ResearchDetails?.ResearchSubject);

        if (items.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = W("右键添加核心研究问题。", "Right-click to add core research questions.", "右クリックして中心研究課題を追加できます。"),
                Opacity = .62,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var item in items)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new TextBlock { Text = "•", Opacity = .55 });
                var text = new TextBlock { Text = item, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                stack.Children.Add(row);
            }
        }

        return new ScrollViewer
        {
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = stack
        };
    }

    private MenuFlyout CreateRound6ResearchQuestionMenu(ProjectWorkspacePanel panel)
    {
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem
        {
            Text = W("编辑内容", "Edit content", "内容を編集"),
            IsEnabled = _project is { IsArchived: false }
        };
        edit.Click += (_, _) => OpenRound6ResearchQuestionEditor();
        menu.Items.Add(edit);

        if (_project is { IsArchived: false })
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var remove = new MenuFlyoutItem
            {
                Text = W("删除面板", "Remove panel", "パネルを削除"),
                Tag = panel.Id
            };
            remove.Click += OnRemovePanel;
            menu.Items.Add(remove);
        }
        return menu;
    }

    private void OpenRound6ResearchQuestionEditor()
    {
        if (_project is null || _project.Kind != ProjectKindCodes.Research || _project.IsArchived) return;

        DrawerTitle.Text = W("编辑核心研究问题", "Edit core research questions", "中心研究課題を編集");
        DrawerSubtitle.Text = W(
            "一条一条记录研究问题，与重点策略使用相同的编辑方式。",
            "Record research questions as separate items, using the same editor pattern as key strategies.",
            "重点戦略と同じ編集方式で、研究課題を1件ずつ記録します。");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        var questions = new StrategyListEditor(
            ParseRound6ResearchQuestions(_project.ResearchDetails?.ResearchSubject),
            W("研究问题", "Research question", "研究課題"),
            W("删除研究问题", "Remove research question", "研究課題を削除"),
            W("＋ 添加研究问题", "+ Add research question", "＋ 研究課題を追加"));
        DrawerContent.Children.Add(questions.Root);

        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer();
        DrawerFooter.Children.Add(cancel);

        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
            if (_project is null) return;
            var candidate = CloneProject(_project);
            candidate.ResearchDetails ??= new ResearchProjectDetails();
            candidate.ResearchDetails.ResearchSubject = SerializeRound6ResearchQuestions(questions.Values);
            candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await SaveCandidateAsync(candidate);
            CloseDrawer();
            ApplyProject();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private static IReadOnlyList<string> ParseRound6ResearchQuestions(string? source) =>
        string.IsNullOrWhiteSpace(source)
            ? []
            : source.Split(['\r', '\n', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();

    private static string SerializeRound6ResearchQuestions(IEnumerable<string> values) =>
        string.Join(Environment.NewLine, values.Select(value => value.Trim()).Where(value => value.Length > 0));
}
