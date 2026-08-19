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
        foreach (var tile in _tileViews.Values)
            tile.Background = ResourceBrush("CardBackgroundFillColorDefaultBrush");
    }

    private void ApplyRound6Polish()
    {
        if (_round6Applying || _project is null) return;
        _round6Applying = true;
        try
        {
            ApplyRound6StateBadge();

            foreach (var stale in _round6HookedTiles.Keys.Where(id => !_tileViews.ContainsKey(id)).ToArray())
                _round6HookedTiles.Remove(stale);

            foreach (var pair in _tileViews.ToArray())
            {
                var panel = _project.WorkspaceLayout?.Panels.FirstOrDefault(item => item.Id == pair.Key);
                if (panel is null) continue;

                var tile = pair.Value;
                // Match the project overview card in both light and dark themes.
                tile.Background = ResourceBrush("CardBackgroundFillColorDefaultBrush");

                var isNewTile = !_round6HookedTiles.TryGetValue(panel.Id, out var hooked) || !ReferenceEquals(hooked, tile);
                if (!isNewTile) continue;

                switch (panel.Kind)
                {
                    case ProjectWorkspacePanelKinds.Description:
                        ReplaceRound4TileBody(tile, BuildRound6Description());
                        break;
                    case ProjectWorkspacePanelKinds.ResearchQuestion:
                        ReplaceRound4TileBody(tile, BuildRound6ResearchQuestion(panel));
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

    private UIElement BuildRound6ResearchQuestion(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 7 };
        var source = _project?.ResearchDetails?.ResearchSubject;
        var items = string.IsNullOrWhiteSpace(source)
            ? Array.Empty<string>()
            : source.Split(['\r', '\n', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(panel.Height <= 1 ? 3 : 8)
                .ToArray();

        if (items.Length == 0)
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
}
