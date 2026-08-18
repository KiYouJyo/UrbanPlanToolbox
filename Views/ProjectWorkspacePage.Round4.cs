using System.Globalization;
using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using Windows.Foundation;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private readonly Dictionary<Guid, Border> _round4HookedTiles = [];
    private bool _round4ApplyingTileUpgrades;

    private DispatcherQueueTimer? _round4HoldTimer;
    private Border? _round4PointerTile;
    private ProjectWorkspacePanel? _round4PointerPanel;
    private FrameworkElement? _round4PointerHandle;
    private uint _round4PointerId;
    private Point _round4PointerStart;
    private double _round4StartLeft;
    private double _round4StartTop;
    private double _round4StartWidth;
    private double _round4StartHeight;
    private Round4PointerOperation _round4PointerOperation;
    private bool _round4HoldActivated;

    private void OnRound4Loaded(object sender, RoutedEventArgs e)
    {
        TileCanvas.LayoutUpdated -= OnRound4CanvasLayoutUpdated;
        TileCanvas.LayoutUpdated += OnRound4CanvasLayoutUpdated;
        DispatcherQueue.TryEnqueue(ApplyRound4TileUpgrades);
    }

    private void OnRound4CanvasLayoutUpdated(object? sender, object e) => ApplyRound4TileUpgrades();

    private void ApplyRound4TileUpgrades()
    {
        if (_round4ApplyingTileUpgrades || _project?.WorkspaceLayout is null) return;
        _round4ApplyingTileUpgrades = true;
        try
        {
            foreach (var stale in _round4HookedTiles.Keys.Where(id => !_tileViews.ContainsKey(id)).ToArray())
                _round4HookedTiles.Remove(stale);

            foreach (var pair in _tileViews.ToArray())
            {
                var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == pair.Key);
                if (panel is null) continue;
                var tile = pair.Value;
                var isNewTile = !_round4HookedTiles.TryGetValue(panel.Id, out var hooked) || !ReferenceEquals(hooked, tile);
                if (isNewTile)
                {
                    tile.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnRound4TilePointerPressed), true);
                    tile.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnRound4TilePointerMoved), true);
                    tile.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnRound4TilePointerReleased), true);
                    tile.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnRound4TilePointerCanceled), true);
                    _round4HookedTiles[panel.Id] = tile;

                    switch (panel.Kind)
                    {
                        case ProjectWorkspacePanelKinds.ImageShowcase:
                            ReplaceRound4TileBody(tile, BuildRound4ImageShowcase(panel, tile.Width, tile.Height));
                            tile.ContextFlyout = CreateRound4PanelMenu(panel, () => OpenRound4ImageShowcaseEditor(panel));
                            break;
                        case ProjectWorkspacePanelKinds.Inspirations:
                            ReplaceRound4TileBody(tile, BuildRound4Inspirations());
                            break;
                        case ProjectWorkspacePanelKinds.Files:
                            ReplaceRound4TileBody(tile, BuildRound4Files());
                            tile.ContextFlyout = CreateRound4PanelMenu(panel, OpenRound4FileDrawer);
                            break;
                    }
                }

                if (_layoutEditing && _selectedPanelId == panel.Id && _columnCount > 1 && _columnCount != ProjectWorkspaceLayoutService.Columns)
                    EnsureRound4ResizeGrip(tile, panel);
            }
        }
        finally
        {
            _round4ApplyingTileUpgrades = false;
        }
    }

    private static void ReplaceRound4TileBody(Border tile, UIElement replacement)
    {
        if (tile.Child is not Grid root) return;
        var oldBody = root.Children
            .Where(child => Grid.GetRow(child) == 1)
            .FirstOrDefault(child => child is not Border { Tag: Guid });
        if (oldBody is not null) root.Children.Remove(oldBody);
        Grid.SetRow(replacement, 1);
        root.Children.Insert(Math.Min(1, root.Children.Count), replacement);
    }

    private FlyoutBase CreateRound4PanelMenu(ProjectWorkspacePanel panel, Action editAction)
    {
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem
        {
            Text = W("编辑内容", "Edit content", "内容を編集"),
            IsEnabled = _project is { IsArchived: false }
        };
        edit.Click += (_, _) => editAction();
        menu.Items.Add(edit);

        var settings = new MenuFlyoutItem
        {
            Text = W("面板设置", "Panel settings", "パネル設定"),
            Tag = panel.Id,
            IsEnabled = _project is { IsArchived: false }
        };
        settings.Click += OnPanelSettings;
        menu.Items.Add(settings);

        if (_project is { IsArchived: false })
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var delete = new MenuFlyoutItem { Text = W("删除面板", "Remove panel", "パネルを削除"), Tag = panel.Id };
            delete.Click += OnRemovePanel;
            menu.Items.Add(delete);
        }
        return menu;
    }

    // Round 4: the showcase renders one nearly full-width image at a time, always fitted in full.
    // Wheel paging and explicit previous/next buttons both remain available.
    private UIElement BuildRound4ImageShowcase(ProjectWorkspacePanel panel, double tileWidth, double tileHeight)
    {
        var items = GetShowcaseItems(panel);
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
            if (bitmap is not null)
            {
                visual.Children.Add(new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                });
            }
            else
            {
                visual.Children.Add(new FontIcon
                {
                    Glyph = "\uEB9F",
                    FontSize = 30,
                    Opacity = .4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var caption = new Border
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
            };
            visual.Children.Add(caption);

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
                if (!_layoutEditing) ShowRound4ImageViewer(items, itemIndex);
            };
            ToolTipService.SetToolTip(card, W("点击查看大图", "Click to view full image", "クリックして拡大表示"));
            strip.Children.Add(card);
        }

        scroll.Content = strip;
        void PageBy(int direction)
        {
            var step = cardWidth + 12;
            var current = (int)Math.Round(scroll.HorizontalOffset / step);
            var targetIndex = Math.Clamp(current + direction, 0, items.Count - 1);
            scroll.ChangeView(targetIndex * step, null, null, true);
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

    private void ShowRound4ImageViewer(IReadOnlyList<ShowcaseItem> items, int initialIndex)
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
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var counter = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = .65, MinWidth = 46 };
        var title = new TextBlock
        {
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var zoomOut = new Button { Content = "−", MinWidth = 38, Width = 38, Height = 36 };
        var zoomText = new TextBlock { MinWidth = 52, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = .72 };
        var fit = new Button { Content = W("适应", "Fit", "全体表示"), Height = 36 };
        var zoomIn = new Button { Content = "+", MinWidth = 38, Width = 38, Height = 36 };
        var close = new Button { Content = "×", MinWidth = 38, Width = 38, Height = 36 };
        toolbar.Children.Add(counter);
        Grid.SetColumn(title, 1); toolbar.Children.Add(title);
        Grid.SetColumn(zoomOut, 2); toolbar.Children.Add(zoomOut);
        Grid.SetColumn(zoomText, 3); toolbar.Children.Add(zoomText);
        Grid.SetColumn(fit, 4); toolbar.Children.Add(fit);
        Grid.SetColumn(zoomIn, 5); toolbar.Children.Add(zoomIn);
        Grid.SetColumn(close, 6); toolbar.Children.Add(close);
        layout.Children.Add(toolbar);

        var media = new Grid
        {
            MinHeight = 360,
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush")
        };
        Grid.SetRow(media, 1);
        layout.Children.Add(media);

        var image = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var placeholder = new FontIcon
        {
            Glyph = "\uEB9F",
            FontSize = 48,
            Opacity = .4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var viewport = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Enabled,
            MinZoomFactor = .25f,
            MaxZoomFactor = 6f,
            Content = image
        };
        media.Children.Add(viewport);
        media.Children.Add(placeholder);

        var previous = new Button
        {
            Content = "‹", Width = 48, Height = 56, MinWidth = 48, FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var next = new Button
        {
            Content = "›", Width = 48, Height = 56, MinWidth = 48, FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        media.Children.Add(previous);
        media.Children.Add(next);

        void FitImageToViewport()
        {
            var width = Math.Max(120, media.ActualWidth - 36);
            var height = Math.Max(120, media.ActualHeight - 36);
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

        previous.Click += (_, _) => ChangeImage(-1);
        next.Click += (_, _) => ChangeImage(1);
        zoomOut.Click += (_, _) => SetZoom(viewport.ZoomFactor / 1.25f);
        zoomIn.Click += (_, _) => SetZoom(viewport.ZoomFactor * 1.25f);
        fit.Click += (_, _) => FitImageToViewport();
        viewport.ViewChanged += (_, _) => zoomText.Text = $"{viewport.ZoomFactor * 100:0}%";
        media.SizeChanged += (_, _) =>
        {
            if (Math.Abs(viewport.ZoomFactor - 1f) < .01f) FitImageToViewport();
        };
        close.Click += (_, _) => pageRoot.Children.Remove(overlay);
        overlay.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape) { pageRoot.Children.Remove(overlay); args.Handled = true; }
            else if (args.Key == VirtualKey.Left) { ChangeImage(-1); args.Handled = true; }
            else if (args.Key == VirtualKey.Right) { ChangeImage(1); args.Handled = true; }
        };

        pageRoot.Children.Add(overlay);
        RefreshViewer();
        close.Focus(FocusState.Programmatic);
    }

    // Round 4: use the same thumbnail + metadata hierarchy as the field-survey photo organizer.
    private void OpenRound4ImageShowcaseEditor(ProjectWorkspacePanel panel)
    {
        if (_project is null || _project.IsArchived) return;
        DrawerTitle.Text = W("编辑图片展示架", "Edit image showcase", "画像ショーケースを編集");
        DrawerSubtitle.Text = W("以缩略图列表管理现有图片；可直接修改标题、添加或移除图片", "Manage existing images as a thumbnail list; rename, add or remove them directly", "サムネイル一覧で画像を管理し、名前変更・追加・削除ができます");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        var drafts = GetRound4ConfiguredShowcaseItems(panel);
        var listHost = new StackPanel { Spacing = 8 };
        var empty = new TextBlock
        {
            Text = W("尚未添加展示图片。", "No showcase images yet.", "表示画像はまだありません。"),
            Opacity = .62,
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshRows()
        {
            listHost.Children.Clear();
            empty.Visibility = drafts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            for (var i = 0; i < drafts.Count; i++)
            {
                var rowIndex = i;
                var draft = drafts[i];
                var card = new Border
                {
                    Background = ResourceBrush("CardBackgroundFillColorSecondaryBrush"),
                    BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10)
                };
                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var previewFrame = new Border
                {
                    Width = 72, Height = 64, CornerRadius = new CornerRadius(5),
                    Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
                    BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1)
                };
                var bitmap = CreateBitmapImage(draft.Source);
                previewFrame.Child = bitmap is null
                    ? new FontIcon { Glyph = "\uEB9F", FontSize = 22, Opacity = .45, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    : new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                grid.Children.Add(previewFrame);

                var info = new StackPanel { Grid.Column = 1, Spacing = 3 };
                info.Children.Add(new TextBlock { Text = $"P{rowIndex + 1:000}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12, Opacity = .72 });
                var titleBox = new TextBox { Text = draft.Title, PlaceholderText = W("图片标题", "Image title", "画像タイトル") };
                titleBox.TextChanged += (_, _) => draft.Title = titleBox.Text;
                info.Children.Add(titleBox);
                info.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(draft.Source) ? W("无图片路径", "No image path", "画像パスなし") : Path.GetFileName(draft.Source),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Opacity = .78
                });
                info.Children.Add(new TextBlock
                {
                    Text = draft.Source,
                    FontSize = 11,
                    Opacity = .5,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(info, 1);
                grid.Children.Add(info);

                var remove = new Button { Content = W("移除", "Remove", "削除"), VerticalAlignment = VerticalAlignment.Top };
                remove.Click += (_, _) => { drafts.RemoveAt(rowIndex); RefreshRows(); };
                Grid.SetColumn(remove, 2);
                grid.Children.Add(remove);
                card.Child = grid;
                listHost.Children.Add(card);
            }
        }

        var pick = new Button { Content = W("＋ 选择本地图片", "+ Choose local images", "＋ ローカル画像を選択"), HorizontalAlignment = HorizontalAlignment.Stretch };
        pick.Click += async (_, _) =>
        {
            var files = await PickImageFilesAsync();
            foreach (var path in files)
                drafts.Add(new Round4ShowcaseDraft(Path.GetFileNameWithoutExtension(path), path));
            RefreshRows();
        };
        DrawerContent.Children.Add(pick);
        DrawerContent.Children.Add(empty);
        DrawerContent.Children.Add(listHost);
        RefreshRows();

        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer();
        DrawerFooter.Children.Add(cancel);
        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
            panel.Settings["images"] = string.Join(Environment.NewLine, drafts.Select(item =>
            {
                var title = string.IsNullOrWhiteSpace(item.Title) ? Path.GetFileNameWithoutExtension(item.Source) : item.Title.Trim();
                return $"{title}|{item.Source}";
            }));
            await PersistProjectAsync(showSuccess: false);
            CloseDrawer();
            RenderWorkspace();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private List<Round4ShowcaseDraft> GetRound4ConfiguredShowcaseItems(ProjectWorkspacePanel panel)
    {
        var result = new List<Round4ShowcaseDraft>();
        if (!panel.Settings.TryGetValue("images", out var raw) || string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            var source = parts.Length > 1 ? parts[1] : string.Empty;
            var title = string.IsNullOrWhiteSpace(parts[0]) ? Path.GetFileNameWithoutExtension(source) : parts[0];
            result.Add(new Round4ShowcaseDraft(title, source));
        }
        return result;
    }

    private UIElement BuildRound4Inspirations()
    {
        if (_linkedInspirations.Count == 0)
            return new TextBlock { Text = W("暂无关联灵感", "No linked inspirations", "関連付けられたアイデアはありません"), Opacity = .62 };

        var stack = new StackPanel { Spacing = 10 };
        foreach (var item in _linkedInspirations)
        {
            var entry = new Border
            {
                Padding = new Thickness(0, 3, 0, 10),
                BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var text = new StackPanel { Spacing = 4 };
            text.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            text.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Content) ? W("暂无详细内容", "No details yet", "詳細内容はありません") : item.Content,
                TextWrapping = TextWrapping.Wrap,
                Opacity = .76
            });
            var category = item.Category == InspirationCategory.Design
                ? W("设计灵感", "Design inspiration", "デザインアイデア")
                : W("科研灵感", "Research inspiration", "研究アイデア");
            text.Children.Add(new TextBlock
            {
                Text = $"{category} · {item.UpdatedAt.ToLocalTime():g}",
                FontSize = 11,
                Opacity = .5
            });
            entry.Child = text;
            stack.Children.Add(entry);
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

    private UIElement BuildRound4Files()
    {
        var folders = GetLinkedFolders();
        if (folders.Count == 0)
            return new TextBlock
            {
                Text = W("右键此面板添加项目文件夹。", "Right-click this panel to add project folders.", "このパネルを右クリックしてプロジェクトフォルダーを追加できます。"),
                Opacity = .62,
                TextWrapping = TextWrapping.Wrap
            };

        var stack = new StackPanel { Spacing = 8 };
        foreach (var folder in folders)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new FontIcon { Glyph = "\uE8B7", FontSize = 15, Opacity = .62, VerticalAlignment = VerticalAlignment.Center });

            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(new TextBlock { Text = folder.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock { Text = folder.DisplayPath, FontSize = 11, Opacity = .55, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var open = new Button
            {
                Content = W("打开", "Open", "開く"),
                Tag = folder,
                IsEnabled = !folder.RequiresReselection,
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

    private void OpenRound4FileDrawer()
    {
        if (_project is null) return;
        var folders = GetLinkedFolders();
        DrawerTitle.Text = W("文件入口", "Files", "ファイル");
        DrawerSubtitle.Text = W("管理项目链接的文件夹；打开操作直接位于文件入口卡片中", "Manage linked project folders; open them directly from the Files tile", "リンク済みフォルダーを管理します。開く操作はファイルパネルから直接行えます");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        if (!_project.IsArchived)
        {
            var add = new Button { Content = W("＋ 添加文件夹", "+ Add folder", "＋ フォルダーを追加"), HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += async (_, _) => await Round4AddFolderAsync();
            DrawerContent.Children.Add(add);
        }

        if (folders.Count == 0)
        {
            DrawerContent.Children.Add(new TextBlock
            {
                Text = W("尚未链接文件夹。", "No folders are linked yet.", "まだフォルダーがリンクされていません。"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = .62
            });
        }

        foreach (var folder in folders)
        {
            var card = new Border
            {
                Background = ResourceBrush("CardBackgroundFillColorSecondaryBrush"),
                BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (!_project.IsArchived) row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = folder.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock { Text = folder.DisplayPath, FontSize = 11, Opacity = .58, TextTrimming = TextTrimming.CharacterEllipsis });
            row.Children.Add(text);
            if (!_project.IsArchived)
            {
                var remove = new Button { Content = W("移除", "Remove", "削除") };
                remove.Click += async (_, _) => await Round4RemoveFolderAsync(folder);
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
            }
            card.Child = row;
            DrawerContent.Children.Add(card);
        }
        ShowDrawer();
    }

    private async Task Round4AddFolderAsync()
    {
        if (_project is null || _project.IsArchived) return;
        var selected = await _folders.SelectAsync(_project.Id);
        if (!selected.Succeeded || selected.Reference is null)
        {
            if (selected.ErrorKey != "ProjectFolder_SelectionCancelled") ShowError(selected.ErrorKey ?? "Project_Error_SaveFailed");
            return;
        }
        if (GetLinkedFolders().Any(existing => SameFolder(existing, selected.Reference)))
        {
            _folders.Clear(selected.Reference);
            OpenRound4FileDrawer();
            return;
        }

        var candidate = CloneProject(_project);
        if (candidate.WorkFolder is null) candidate.WorkFolder = selected.Reference;
        else candidate.AdditionalFolders.Add(selected.Reference);
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(candidate);
        if (!result.Succeeded) _folders.Clear(selected.Reference);
        await ApplyMutationAsync(result);
        OpenRound4FileDrawer();
    }

    private async Task Round4RemoveFolderAsync(ProjectFolderReference folder)
    {
        if (_project is null || _project.IsArchived) return;
        var candidate = CloneProject(_project);
        if (candidate.WorkFolder is not null && SameFolder(candidate.WorkFolder, folder))
        {
            candidate.WorkFolder = candidate.AdditionalFolders.FirstOrDefault();
            if (candidate.WorkFolder is not null)
                candidate.AdditionalFolders.RemoveAll(item => SameFolder(item, candidate.WorkFolder));
        }
        else
        {
            candidate.AdditionalFolders.RemoveAll(item => SameFolder(item, folder));
        }
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(candidate);
        if (result.Succeeded) _folders.Clear(folder);
        await ApplyMutationAsync(result);
        OpenRound4FileDrawer();
    }

    // Round 4 layout input path: this deliberately bypasses the old 12-column-only gate.
    // 8/6-column responsive views are mapped back to the canonical 12-column layout when saved.
    private void OnRound4TilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || _project.IsArchived || _busy || _columnCount <= 1 || sender is not Border { Tag: Guid id } tile) return;
        if (IsRound4InteractiveSource(e.OriginalSource as DependencyObject, tile) || IsRound4ResizeSource(e.OriginalSource as DependencyObject, tile)) return;
        var point = e.GetCurrentPoint(tile);
        if (!point.Properties.IsLeftButtonPressed) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null) return;

        CancelRound4PointerState(releaseCapture: false);
        ClearPointerOperation();
        _round4PointerTile = tile;
        _round4PointerPanel = panel;
        _round4PointerHandle = tile;
        _round4PointerId = e.Pointer.PointerId;
        _round4PointerStart = e.GetCurrentPoint(TileCanvas).Position;
        _round4StartLeft = Canvas.GetLeft(tile);
        _round4StartTop = Canvas.GetTop(tile);
        _round4StartWidth = tile.Width;
        _round4StartHeight = tile.Height;
        tile.CapturePointer(e.Pointer);

        if (_layoutEditing)
        {
            StartRound4PointerOperation(Round4PointerOperation.Move);
        }
        else
        {
            _round4HoldTimer = DispatcherQueue.CreateTimer();
            _round4HoldTimer.Interval = TimeSpan.FromMilliseconds(360);
            _round4HoldTimer.IsRepeating = false;
            _round4HoldTimer.Tick += (_, _) =>
            {
                _round4HoldTimer?.Stop();
                _round4HoldTimer = null;
                _round4HoldActivated = true;
                _layoutEditing = true;
                _selectedPanelId = panel.Id;
                StartRound4PointerOperation(Round4PointerOperation.Move);
            };
            _round4HoldTimer.Start();
        }
        e.Handled = true;
    }

    private void StartRound4PointerOperation(Round4PointerOperation operation)
    {
        if (_round4PointerPanel is null || _round4PointerTile is null) return;
        RememberLayoutForUndo();
        _round4PointerOperation = operation;
        _selectedPanelId = _round4PointerPanel.Id;
        _round4PointerTile.BorderBrush = ResourceBrush("AccentFillColorDefaultBrush");
        _round4PointerTile.BorderThickness = new Thickness(2);
        _round4PointerTile.Opacity = .94;
    }

    private void OnRound4TilePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerTile is null || !ReferenceEquals(sender, _round4PointerTile) || e.Pointer.PointerId != _round4PointerId) return;
        var position = e.GetCurrentPoint(TileCanvas).Position;
        var dx = position.X - _round4PointerStart.X;
        var dy = position.Y - _round4PointerStart.Y;

        if (_round4PointerOperation == Round4PointerOperation.None)
        {
            if ((dx * dx) + (dy * dy) > 324)
            {
                _round4HoldTimer?.Stop();
                _round4HoldTimer = null;
                var pointer = e.Pointer;
                _round4PointerTile.ReleasePointerCapture(pointer);
                CancelRound4PointerState(releaseCapture: false);
            }
            return;
        }
        if (_round4PointerOperation != Round4PointerOperation.Move) return;
        Canvas.SetLeft(_round4PointerTile, Math.Max(0, _round4StartLeft + dx));
        Canvas.SetTop(_round4PointerTile, Math.Max(0, _round4StartTop + dy));
        e.Handled = true;
    }

    private async void OnRound4TilePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerTile is null || !ReferenceEquals(sender, _round4PointerTile) || e.Pointer.PointerId != _round4PointerId) return;
        _round4HoldTimer?.Stop();
        _round4HoldTimer = null;

        if (_round4PointerOperation == Round4PointerOperation.Move && _round4PointerPanel is not null && _project?.WorkspaceLayout is not null)
        {
            _round4PointerTile.ReleasePointerCapture(e.Pointer);
            var renderX = (int)Math.Round(Math.Max(0, Canvas.GetLeft(_round4PointerTile)) / (_unitWidth + TileGap));
            var y = (int)Math.Round(Math.Max(0, Canvas.GetTop(_round4PointerTile)) / (TileRowHeight + TileGap));
            var canonicalX = Round4ToCanonicalColumn(renderX);
            ProjectWorkspaceLayoutService.MovePanel(_project.WorkspaceLayout, _round4PointerPanel.Id, canonicalX, y);
            CancelRound4PointerState(releaseCapture: false);
            await PersistProjectAsync(showSuccess: false);
            RenderWorkspace();
            DispatcherQueue.TryEnqueue(ApplyRound4TileUpgrades);
            e.Handled = true;
            return;
        }

        _round4PointerTile.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState(releaseCapture: false);
    }

    private void OnRound4TilePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerTile is not null && ReferenceEquals(sender, _round4PointerTile) && e.Pointer.PointerId == _round4PointerId)
            _round4PointerTile.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState(releaseCapture: false);
        RenderWorkspace();
    }

    private void EnsureRound4ResizeGrip(Border tile, ProjectWorkspacePanel panel)
    {
        if (tile.Child is not Grid root) return;
        if (root.Children.OfType<FrameworkElement>().Any(item => item.Name == "Round4ResizeGrip")) return;
        var grip = new Border
        {
            Name = "Round4ResizeGrip",
            Width = 26,
            Height = 26,
            Tag = panel.Id,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -4, -4),
            Background = ResourceBrush("AccentFillColorSecondaryBrush"),
            BorderBrush = ResourceBrush("AccentFillColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Child = new TextBlock { Text = "↘", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        grip.PointerPressed += OnRound4ResizePointerPressed;
        grip.PointerMoved += OnRound4ResizePointerMoved;
        grip.PointerReleased += OnRound4ResizePointerReleased;
        grip.PointerCanceled += OnRound4ResizePointerCanceled;
        Grid.SetRow(grip, 1);
        root.Children.Add(grip);
        ToolTipService.SetToolTip(grip, W("拖动调整尺寸", "Drag to resize", "ドラッグしてサイズ変更"));
    }

    private void OnRound4ResizePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || _columnCount <= 1 || sender is not FrameworkElement { Tag: Guid id } grip || !_tileViews.TryGetValue(id, out var tile)) return;
        var point = e.GetCurrentPoint(grip);
        if (!point.Properties.IsLeftButtonPressed) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null) return;
        CancelRound4PointerState(releaseCapture: false);
        ClearPointerOperation();
        _round4PointerTile = tile;
        _round4PointerPanel = panel;
        _round4PointerHandle = grip;
        _round4PointerId = e.Pointer.PointerId;
        _round4PointerStart = e.GetCurrentPoint(TileCanvas).Position;
        _round4StartLeft = Canvas.GetLeft(tile);
        _round4StartTop = Canvas.GetTop(tile);
        _round4StartWidth = tile.Width;
        _round4StartHeight = tile.Height;
        grip.CapturePointer(e.Pointer);
        StartRound4PointerOperation(Round4PointerOperation.Resize);
        e.Handled = true;
    }

    private void OnRound4ResizePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerOperation != Round4PointerOperation.Resize || _round4PointerHandle is null || !ReferenceEquals(sender, _round4PointerHandle) || _round4PointerTile is null || e.Pointer.PointerId != _round4PointerId) return;
        var position = e.GetCurrentPoint(TileCanvas).Position;
        _round4PointerTile.Width = Math.Max(_unitWidth, _round4StartWidth + position.X - _round4PointerStart.X);
        _round4PointerTile.Height = Math.Max(TileRowHeight, _round4StartHeight + position.Y - _round4PointerStart.Y);
        e.Handled = true;
    }

    private async void OnRound4ResizePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerOperation != Round4PointerOperation.Resize || _round4PointerHandle is null || !ReferenceEquals(sender, _round4PointerHandle) || _round4PointerTile is null || _round4PointerPanel is null || _project?.WorkspaceLayout is null || e.Pointer.PointerId != _round4PointerId) return;
        _round4PointerHandle.ReleasePointerCapture(e.Pointer);
        var renderWidth = Math.Max(1, (int)Math.Round((_round4PointerTile.Width + TileGap) / (_unitWidth + TileGap)));
        var height = Math.Max(1, (int)Math.Round((_round4PointerTile.Height + TileGap) / (TileRowHeight + TileGap)));
        var canonicalWidth = Round4ToCanonicalSpan(renderWidth);
        ProjectWorkspaceLayoutService.ResizePanel(_project.WorkspaceLayout, _round4PointerPanel.Id, canonicalWidth, height);
        CancelRound4PointerState(releaseCapture: false);
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
        DispatcherQueue.TryEnqueue(ApplyRound4TileUpgrades);
        e.Handled = true;
    }

    private void OnRound4ResizePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerHandle is not null && ReferenceEquals(sender, _round4PointerHandle) && e.Pointer.PointerId == _round4PointerId)
            _round4PointerHandle.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState(releaseCapture: false);
        RenderWorkspace();
    }

    private int Round4ToCanonicalColumn(int renderColumn)
    {
        var columns = Math.Max(1, _columnCount);
        var canonical = columns == ProjectWorkspaceLayoutService.Columns
            ? renderColumn
            : (int)Math.Round(renderColumn * ProjectWorkspaceLayoutService.Columns / (double)columns);
        return Math.Clamp(canonical, 0, ProjectWorkspaceLayoutService.Columns - 1);
    }

    private int Round4ToCanonicalSpan(int renderSpan)
    {
        var columns = Math.Max(1, _columnCount);
        var canonical = columns == ProjectWorkspaceLayoutService.Columns
            ? renderSpan
            : (int)Math.Round(renderSpan * ProjectWorkspaceLayoutService.Columns / (double)columns);
        return Math.Clamp(canonical, 1, ProjectWorkspaceLayoutService.Columns);
    }

    private void CancelRound4PointerState(bool releaseCapture)
    {
        _round4HoldTimer?.Stop();
        _round4HoldTimer = null;
        if (releaseCapture && _round4PointerHandle is not null)
        {
            // Capture release is normally performed by the concrete pointer event, where the Pointer object is available.
        }
        _round4PointerTile = null;
        _round4PointerPanel = null;
        _round4PointerHandle = null;
        _round4PointerId = 0;
        _round4PointerOperation = Round4PointerOperation.None;
        _round4HoldActivated = false;
    }

    private static bool IsRound4InteractiveSource(DependencyObject? source, FrameworkElement tile)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, tile); current = VisualTreeHelper.GetParent(current))
        {
            if (current is Button or TextBox or ComboBox or Slider or ToggleSwitch or CheckBox) return true;
        }
        return false;
    }

    private static bool IsRound4ResizeSource(DependencyObject? source, FrameworkElement tile)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, tile); current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Tag: Guid, Width: <= 32, Height: <= 32 }) return true;
        }
        return false;
    }

    private sealed class Round4ShowcaseDraft(string title, string source)
    {
        public string Title { get; set; } = title;
        public string Source { get; } = source;
    }

    private enum Round4PointerOperation { None, Move, Resize }
}
