using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private readonly Dictionary<Guid, Border> _round5HookedTiles = [];
    private bool _round5Loaded;
    private bool _round5ApplyingTileUpgrades;

    private void OnWorkspaceLoaded(object sender, RoutedEventArgs e)
    {
        OnRound4Loaded(sender, e);
        OverviewPhaseBadge.Visibility = Visibility.Collapsed;
        if (_round5Loaded) return;
        _round5Loaded = true;

        ResetLayoutButton.Click -= OnResetLayout;
        ResetLayoutButton.Click += OnRound5ResetLayout;
        TileCanvas.LayoutUpdated += OnRound5CanvasLayoutUpdated;
        DispatcherQueue.TryEnqueue(ApplyRound5TileUpgrades);
    }

    private void OnRound5CanvasLayoutUpdated(object? sender, object e) => ApplyRound5TileUpgrades();

    private void ApplyRound5TileUpgrades()
    {
        if (_round5ApplyingTileUpgrades || _project?.WorkspaceLayout is null) return;
        _round5ApplyingTileUpgrades = true;
        try
        {
            foreach (var stale in _round5HookedTiles.Keys.Where(id => !_tileViews.ContainsKey(id)).ToArray())
                _round5HookedTiles.Remove(stale);

            foreach (var pair in _tileViews.ToArray())
            {
                var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == pair.Key);
                if (panel is null) continue;
                var tile = pair.Value;
                var isNewTile = !_round5HookedTiles.TryGetValue(panel.Id, out var hooked) || !ReferenceEquals(hooked, tile);
                if (!isNewTile) continue;

                tile.ContextFlyout = CreateRound5PanelMenu(panel);
                switch (panel.Kind)
                {
                    case ProjectWorkspacePanelKinds.Milestones:
                    case ProjectWorkspacePanelKinds.ResearchProgress:
                        ReplaceRound4TileBody(tile, BuildRound5Milestones());
                        break;
                    case ProjectWorkspacePanelKinds.ImageShowcase:
                        ReplaceRound4TileBody(tile, BuildRound5ImageShowcase(panel, tile.Width, tile.Height));
                        break;
                    case ProjectWorkspacePanelKinds.Files:
                        ReplaceRound4TileBody(tile, BuildRound5Files());
                        break;
                }
                _round5HookedTiles[panel.Id] = tile;
            }
        }
        finally
        {
            _round5ApplyingTileUpgrades = false;
        }
    }

    private FlyoutBase CreateRound5PanelMenu(ProjectWorkspacePanel panel)
    {
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem
        {
            Text = W("编辑内容", "Edit content", "内容を編集"),
            IsEnabled = _project is { IsArchived: false }
        };
        edit.Click += (_, _) =>
        {
            if (panel.Kind == ProjectWorkspacePanelKinds.ImageShowcase)
                OpenRound4ImageShowcaseEditor(panel);
            else if (panel.Kind == ProjectWorkspacePanelKinds.Files)
                OpenRound4FileDrawer();
            else
                OpenPanelContentEditor(panel);
        };
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

    private UIElement BuildRound5Milestones()
    {
        var stack = new StackPanel { Spacing = 10 };
        if (_project is null) return stack;
        var ordered = _project.Milestones.OrderBy(item => item.Date).ToArray();
        if (ordered.Length == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = W("暂无时间节点。", "No milestones yet.", "時間ノードはまだありません。"),
                Opacity = .62
            });
        }

        foreach (var milestone in ordered)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Background = ResourceBrush("AccentFillColorDefaultBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock
            {
                Text = milestone.Date.ToString("MM/dd", CultureInfo.CurrentCulture),
                FontSize = 11,
                Opacity = .58
            });
            text.Children.Add(new TextBlock
            {
                Text = milestone.Title,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            if (!string.IsNullOrWhiteSpace(milestone.Notes))
            {
                text.Children.Add(new TextBlock
                {
                    Text = milestone.Notes,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = .66
                });
            }
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            stack.Children.Add(row);
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

    private UIElement BuildRound5Files()
    {
        var folders = GetLinkedFolders();
        if (folders.Count == 0)
        {
            return new TextBlock
            {
                Text = W("右键此面板添加项目文件夹。", "Right-click this panel to add project folders.", "このパネルを右クリックしてプロジェクトフォルダーを追加できます。"),
                Opacity = .62,
                TextWrapping = TextWrapping.Wrap
            };
        }

        var stack = new StackPanel { Spacing = 8 };
        foreach (var folder in folders)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new FontIcon
            {
                Glyph = "\uE8B7",
                FontSize = 15,
                Opacity = .62,
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock { Text = folder.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock
            {
                Text = folder.DisplayPath,
                FontSize = 11,
                Opacity = .55,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var open = new Button
            {
                Content = W("打开", "Open", "開く"),
                Tag = folder,
                VerticalAlignment = VerticalAlignment.Center
            };
            open.Click += OnOpenLinkedFolder;
            Grid.SetColumn(open, 2);
            row.Children.Add(open);
            stack.Children.Add(row);
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

    private UIElement BuildRound5ImageShowcase(ProjectWorkspacePanel panel, double tileWidth, double tileHeight)
    {
        var items = GetRound4ShowcaseItems(panel);
        if (items.Count == 0)
        {
            return new Grid
            {
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new FontIcon { Glyph = "\uEB9F", FontSize = 26, Opacity = .5 },
                            new TextBlock
                            {
                                Text = W("右键此面板添加展示图片", "Right-click this panel to add showcase images", "このパネルを右クリックして画像を追加"),
                                TextAlignment = TextAlignment.Center,
                                TextWrapping = TextWrapping.Wrap,
                                Opacity = .62
                            }
                        }
                    }
                }
            };
        }

        var host = new Grid();
        var scroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled
        };
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var cardWidth = Math.Max(180, tileWidth - 34);
        var cardHeight = Math.Max(110, tileHeight - 62);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemIndex = index;
            var visual = new Grid { Background = ResourceBrush("CardBackgroundFillColorDefaultBrush") };
            var bitmap = CreateBitmapImage(item.Source);
            visual.Children.Add(bitmap is null
                ? new FontIcon
                {
                    Glyph = "\uEB9F",
                    FontSize = 30,
                    Opacity = .4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                });
            visual.Children.Add(new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(168, 18, 22, 22)),
                Padding = new Thickness(12, 8, 12, 8),
                Child = new TextBlock
                {
                    Text = item.Title,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            });

            var card = new Border
            {
                Width = cardWidth,
                Height = cardHeight,
                CornerRadius = new CornerRadius(8),
                BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                Child = visual
            };
            card.Tapped += (_, args) =>
            {
                args.Handled = true;
                if (!_layoutEditing) ShowRound5ImageViewer(items, itemIndex);
            };
            strip.Children.Add(card);
        }
        scroll.Content = strip;

        void PageBy(int direction)
        {
            var step = cardWidth + 12;
            var current = (int)Math.Round(scroll.HorizontalOffset / step);
            var target = Math.Clamp(current + direction, 0, items.Count - 1);
            scroll.ChangeView(target * step, null, null, true);
        }
        scroll.PointerWheelChanged += (_, args) =>
        {
            var delta = args.GetCurrentPoint(scroll).Properties.MouseWheelDelta;
            if (delta == 0) return;
            PageBy(delta > 0 ? -1 : 1);
            args.Handled = true;
        };
        host.Children.Add(scroll);
        if (items.Count > 1)
        {
            var previous = CreateCarouselButton("‹", HorizontalAlignment.Left);
            previous.Click += (_, _) => PageBy(-1);
            host.Children.Add(previous);
            var next = CreateCarouselButton("›", HorizontalAlignment.Right);
            next.Click += (_, _) => PageBy(1);
            host.Children.Add(next);
        }
        return host;
    }

    private void ShowRound5ImageViewer(IReadOnlyList<ShowcaseItem> items, int initialIndex)
    {
        if (items.Count == 0 || Content is not Grid pageRoot) return;
        var index = Math.Clamp(initialIndex, 0, items.Count - 1);

        var overlay = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(224, 8, 12, 13)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var shell = new Border
        {
            Margin = new Thickness(24),
            MaxWidth = 1440,
            MaxHeight = Math.Max(520, (XamlRoot?.Size.Height ?? 900) - 48),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = ResourceBrush("AppTransientSurfaceBrush"),
            BorderBrush = ResourceBrush("AppTransientSurfaceBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16)
        };
        overlay.Children.Add(shell);

        var layout = new Grid { RowSpacing = 12 };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.Child = layout;

        var toolbar = new Grid { ColumnSpacing = 8 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 5; i++) toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var counter = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = .65, MinWidth = 46 };
        var title = new TextBlock
        {
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var zoomOut = new Button { Content = "−", MinWidth = 38, Width = 38, Height = 36 };
        var zoomText = new TextBlock { MinWidth = 52, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = .72 };
        var zoomIn = new Button { Content = "+", MinWidth = 38, Width = 38, Height = 36 };
        var fit = new Button { Content = W("适应", "Fit", "全体表示"), Height = 36 };
        var close = new Button { Content = "×", MinWidth = 38, Width = 38, Height = 36 };
        toolbar.Children.Add(counter);
        Grid.SetColumn(title, 1); toolbar.Children.Add(title);
        Grid.SetColumn(zoomOut, 2); toolbar.Children.Add(zoomOut);
        Grid.SetColumn(zoomText, 3); toolbar.Children.Add(zoomText);
        Grid.SetColumn(zoomIn, 4); toolbar.Children.Add(zoomIn);
        Grid.SetColumn(fit, 5); toolbar.Children.Add(fit);
        Grid.SetColumn(close, 6); toolbar.Children.Add(close);
        layout.Children.Add(toolbar);

        var media = new Grid
        {
            MinHeight = 360,
            ColumnSpacing = 12,
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush")
        };
        media.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        media.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        media.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        Grid.SetRow(media, 1);
        layout.Children.Add(media);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var viewport = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ZoomMode = ZoomMode.Enabled,
            MinZoomFactor = .25f,
            MaxZoomFactor = 6f,
            Content = image
        };
        Grid.SetColumn(viewport, 1);
        media.Children.Add(viewport);

        var placeholder = new FontIcon
        {
            Glyph = "\uEB9F",
            FontSize = 48,
            Opacity = .4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(placeholder, 1);
        media.Children.Add(placeholder);

        var previous = new Button
        {
            Content = "‹",
            Width = 48,
            Height = 56,
            MinWidth = 48,
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        previous.Click += (_, _) => ChangeImage(-1);
        media.Children.Add(previous);
        var next = new Button
        {
            Content = "›",
            Width = 48,
            Height = 56,
            MinWidth = 48,
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        next.Click += (_, _) => ChangeImage(1);
        Grid.SetColumn(next, 2);
        media.Children.Add(next);

        void FitImageToViewport()
        {
            var width = Math.Max(120, viewport.ActualWidth - 24);
            var height = Math.Max(120, viewport.ActualHeight - 24);
            image.Width = width;
            image.Height = height;
            viewport.ChangeView(0, 0, 1f, true);
        }

        void RefreshViewer()
        {
            var item = items[index];
            counter.Text = $"{index + 1} / {items.Count}";
            title.Text = item.Title;
            var bitmap = CreateBitmapImage(item.Source);
            image.Source = bitmap;
            image.Visibility = bitmap is null ? Visibility.Collapsed : Visibility.Visible;
            placeholder.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
            previous.IsEnabled = index > 0;
            next.IsEnabled = index < items.Count - 1;
            DispatcherQueue.TryEnqueue(FitImageToViewport);
        }

        void ChangeImage(int delta)
        {
            var target = Math.Clamp(index + delta, 0, items.Count - 1);
            if (target == index) return;
            index = target;
            RefreshViewer();
        }

        void SetZoom(float factor)
        {
            var target = Math.Clamp(factor, viewport.MinZoomFactor, viewport.MaxZoomFactor);
            viewport.ChangeView(null, null, target, false);
        }

        zoomOut.Click += (_, _) => SetZoom(viewport.ZoomFactor / 1.25f);
        zoomIn.Click += (_, _) => SetZoom(viewport.ZoomFactor * 1.25f);
        fit.Click += (_, _) => FitImageToViewport();
        close.Click += (_, _) => pageRoot.Children.Remove(overlay);
        viewport.ViewChanged += (_, _) => zoomText.Text = $"{viewport.ZoomFactor * 100:0}%";
        viewport.SizeChanged += (_, _) =>
        {
            if (Math.Abs(viewport.ZoomFactor - 1f) < .01f) FitImageToViewport();
        };
        overlay.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape)
            {
                pageRoot.Children.Remove(overlay);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Left)
            {
                ChangeImage(-1);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Right)
            {
                ChangeImage(1);
                args.Handled = true;
            }
        };

        pageRoot.Children.Add(overlay);
        RefreshViewer();
        close.Focus(FocusState.Programmatic);
    }

    private async void OnRound5ResetLayout(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived) return;
        if (!await ConfirmAsync(
                W("恢复默认布局", "Restore default layout", "既定レイアウトを復元"),
                W("只会重置面板的位置和大小，不会删除项目内容或面板数据。", "Only panel positions and sizes will be reset. Project content and panel data are preserved.", "パネルの位置とサイズだけをリセットし、プロジェクト内容やパネルデータは削除しません。")))
            return;

        RememberLayoutForUndo();
        _project.WorkspaceLayout = ProjectWorkspaceResetService.CreateDefaultPreservingPanelData(_project.WorkspaceLayout, _project.Kind);
        _selectedPanelId = null;
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
    }
}
