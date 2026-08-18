using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Projects;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private UIElement BuildRound4ImageShowcase(ProjectWorkspacePanel panel, double tileWidth, double tileHeight)
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

    private IReadOnlyList<ShowcaseItem> GetRound4ShowcaseItems(ProjectWorkspacePanel panel)
    {
        var result = new List<ShowcaseItem>();
        if (!panel.Settings.TryGetValue("images", out var raw) || string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            var source = parts.Length > 1 ? parts[1] : string.Empty;
            var title = string.IsNullOrWhiteSpace(parts[0])
                ? (string.IsNullOrWhiteSpace(source) ? W("项目图片", "Project image", "プロジェクト画像") : Path.GetFileNameWithoutExtension(source))
                : parts[0];
            result.Add(new ShowcaseItem(title, source));
        }
        return result;
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
            Content = "‹",
            Width = 48,
            Height = 56,
            MinWidth = 48,
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var next = new Button
        {
            Content = "›",
            Width = 48,
            Height = 56,
            MinWidth = 48,
            FontSize = 26,
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
        close.Click += (_, _) => pageRoot.Children.Remove(overlay);
        viewport.ViewChanged += (_, _) => zoomText.Text = $"{viewport.ZoomFactor * 100:0}%";
        media.SizeChanged += (_, _) =>
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

    private void OpenRound4ImageShowcaseEditor(ProjectWorkspacePanel panel)
    {
        if (_project is null || _project.IsArchived) return;
        DrawerTitle.Text = W("编辑图片展示架", "Edit image showcase", "画像ショーケースを編集");
        DrawerSubtitle.Text = W("以缩略图列表管理现有图片；可直接修改标题、添加或移除图片", "Manage existing images as a thumbnail list; rename, add or remove them directly", "サムネイル一覧で画像を管理し、名前変更・追加・削除ができます");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        var drafts = GetRound4ShowcaseItems(panel)
            .Select(item => new Round4ShowcaseDraft(item.Title, item.Source))
            .ToList();
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
                    Width = 72,
                    Height = 64,
                    CornerRadius = new CornerRadius(5),
                    Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
                    BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
                    BorderThickness = new Thickness(1)
                };
                var bitmap = CreateBitmapImage(draft.Source);
                previewFrame.Child = bitmap is null
                    ? new FontIcon
                    {
                        Glyph = "\uEB9F",
                        FontSize = 22,
                        Opacity = .45,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                    : new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
                grid.Children.Add(previewFrame);

                var info = new StackPanel { Spacing = 3 };
                info.Children.Add(new TextBlock
                {
                    Text = $"P{rowIndex + 1:000}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 12,
                    Opacity = .72
                });
                var titleBox = new TextBox
                {
                    Text = draft.Title,
                    PlaceholderText = W("图片标题", "Image title", "画像タイトル")
                };
                titleBox.TextChanged += (_, _) => draft.Title = titleBox.Text;
                info.Children.Add(titleBox);
                info.Children.Add(new TextBlock
                {
                    Text = Round4ImageFileName(draft.Source),
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

                var remove = new Button
                {
                    Content = W("移除", "Remove", "削除"),
                    VerticalAlignment = VerticalAlignment.Top
                };
                remove.Click += (_, _) =>
                {
                    drafts.RemoveAt(rowIndex);
                    RefreshRows();
                };
                Grid.SetColumn(remove, 2);
                grid.Children.Add(remove);
                card.Child = grid;
                listHost.Children.Add(card);
            }
        }

        var pick = new Button
        {
            Content = W("＋ 选择本地图片", "+ Choose local images", "＋ ローカル画像を選択"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
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
                var titleText = string.IsNullOrWhiteSpace(item.Title)
                    ? Path.GetFileNameWithoutExtension(item.Source)
                    : item.Title.Trim();
                return $"{titleText}|{item.Source}";
            }));
            await PersistProjectAsync(showSuccess: false);
            CloseDrawer();
            RenderWorkspace();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private static string Round4ImageFileName(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
            return Path.GetFileName(uri.LocalPath.TrimEnd('/'));
        return Path.GetFileName(source);
    }

    private sealed class Round4ShowcaseDraft(string title, string source)
    {
        public string Title { get; set; } = title;
        public string Source { get; } = source;
    }
}
