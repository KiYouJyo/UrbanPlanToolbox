using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private UIElement BuildRound4Inspirations()
    {
        if (_linkedInspirations.Count == 0)
        {
            return new TextBlock
            {
                Text = W("暂无关联灵感", "No linked inspirations", "関連付けられたアイデアはありません"),
                Opacity = .62
            };
        }

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
                Text = string.IsNullOrWhiteSpace(item.Content)
                    ? W("暂无详细内容", "No details yet", "詳細内容はありません")
                    : item.Content,
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
            text.Children.Add(new TextBlock
            {
                Text = folder.DisplayName,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
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
        DrawerSubtitle.Text = W(
            "管理项目链接的文件夹；打开操作直接位于文件入口卡片中",
            "Manage linked project folders; open them directly from the Files tile",
            "リンク済みフォルダーを管理します。開く操作はファイルパネルから直接行えます");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        if (!_project.IsArchived)
        {
            var add = new Button
            {
                Content = W("＋ 添加文件夹", "+ Add folder", "＋ フォルダーを追加"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
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
            if (!_project.IsArchived)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock
            {
                Text = folder.DisplayName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = folder.DisplayPath,
                FontSize = 11,
                Opacity = .58,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
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
            if (selected.ErrorKey != "ProjectFolder_SelectionCancelled")
                ShowError(selected.ErrorKey ?? "Project_Error_SaveFailed");
            return;
        }

        if (GetLinkedFolders().Any(existing => SameFolder(existing, selected.Reference)))
        {
            _folders.Clear(selected.Reference);
            OpenRound4FileDrawer();
            return;
        }

        var candidate = CloneProject(_project);
        if (candidate.WorkFolder is null)
            candidate.WorkFolder = selected.Reference;
        else
            candidate.AdditionalFolders.Add(selected.Reference);
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
}
