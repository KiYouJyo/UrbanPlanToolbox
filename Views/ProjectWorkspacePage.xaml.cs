using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Windows.Foundation;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage : Page
{
    private const double TileGap = 10;
    private const double TileRowHeight = 96;

    private readonly ProjectStorageService _projects = ProjectStorageService.Default;
    private readonly IProjectFolderAccessService _folders = WindowsProjectFolderAccessService.Default;
    private readonly ILocalizationService _localization = LocalizationService.Default;
    private readonly InspirationService _inspirations = InspirationService.Default;

    private readonly Dictionary<Guid, Border> _tileViews = [];
    private ProjectRecord? _project;
    private IReadOnlyList<Inspiration> _linkedInspirations = [];
    private ProjectWorkspaceLayout? _layoutUndo;
    private bool _busy;
    private bool _layoutEditing;
    private Guid? _selectedPanelId;
    private int _columnCount = ProjectWorkspaceLayoutService.Columns;
    private double _unitWidth;

    private ProjectWorkspacePanel? _pointerPanel;
    private FrameworkElement? _pointerHandle;
    private uint _pointerId;
    private Point _pointerStart;
    private double _pointerStartLeft;
    private double _pointerStartTop;
    private double _pointerStartWidth;
    private double _pointerStartHeight;
    private PointerOperation _pointerOperation;

    public ProjectWorkspacePage()
    {
        InitializeComponent();
        ConfigureUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not Guid id)
        {
            ShowError("Project_Error_NotFound");
            return;
        }

        var read = await _projects.ReadAsync(id);
        if (!read.HasValue)
        {
            ShowError("Project_Error_LoadFailed");
            return;
        }

        _project = read.Value;
        var hadLayout = _project.WorkspaceLayout is { Panels.Count: > 0 };
        ProjectWorkspaceLayoutService.EnsureLayout(_project);
        await RefreshLinkedInspirationsAsync();
        ApplyProject();

        // Old v1.8.x projects receive the new workspace lazily.  Persisting the default here
        // makes the layout project-scoped without requiring a destructive schema rewrite.
        if (!hadLayout && !_project.IsArchived)
            await PersistProjectAsync(showSuccess: false);
    }

    private void ConfigureUi()
    {
        BackButton.Content = W("← 返回项目主页", "← Back to projects", "← プロジェクト一覧へ");
        EditOverviewButton.Content = W("编辑项目", "Edit project", "プロジェクトを編集");
        EditOverviewCompactButton.Content = W("编辑概览", "Edit overview", "概要を編集");
        OverviewTitle.Text = W("项目概览", "Project overview", "プロジェクト概要");
        WorkspaceTitleText.Text = W("自定义工作台", "Custom workspace", "カスタムワークスペース");
        WorkspaceSubtitleText.Text = W("拖动与缩放面板，按项目需要组织自己的工作空间", "Arrange panels around the needs of this project", "プロジェクトに合わせてパネルを自由に配置できます");
        AddPanelButton.Content = W("＋ 新建面板", "+ Add panel", "＋ パネルを追加");
        EditLayoutButton.Content = W("编辑布局", "Edit layout", "レイアウトを編集");
        UndoLayoutButton.Content = W("撤销", "Undo", "元に戻す");
        ResetLayoutButton.Content = W("恢复默认", "Reset layout", "既定に戻す");
        ArchivedNotice.Text = W("已归档项目为只读状态。恢复项目后可继续编辑内容与布局。", "Archived projects are read-only. Restore the project to edit its content or layout.", "アーカイブ済みプロジェクトは読み取り専用です。復元すると内容とレイアウトを編集できます。");
        ToolTipService.SetToolTip(MoreButton, W("更多项目操作", "More project actions", "その他のプロジェクト操作"));
    }

    private string W(string zh, string en, string ja)
    {
        var language = _localization.CurrentLanguage;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return en;
        return zh;
    }

    private void ApplyProject()
    {
        if (_project is null) return;

        TitleText.Text = _project.Name;
        ToolTipService.SetToolTip(TitleText, _project.Name);
        KindText.Text = ProjectPresentation.GetKindName(_project.Kind, _localization);
        MetadataText.Text = $"{ProjectPresentation.GetTypeName(_project, _localization)} · {_project.UpdatedAtUtc.ToLocalTime():g}";
        StateText.Text = _localization.GetString(_project.IsArchived ? "Project_State_Archived" : "Project_State_Active");
        StateBadge.Background = ResourceBrush(_project.IsArchived ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorSuccessBackgroundBrush");
        StateText.Foreground = ResourceBrush(_project.IsArchived ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush");
        ArchivedNotice.Visibility = _project.IsArchived ? Visibility.Visible : Visibility.Collapsed;

        ArchiveMenuItem.Text = _localization.GetString(_project.IsArchived ? "Project_Action_Restore" : "Project_Action_Archive");
        DeleteMenuItem.Text = _localization.GetString("Project_Action_Delete");
        EditOverviewButton.IsEnabled = !_project.IsArchived && !_busy;
        EditOverviewCompactButton.IsEnabled = !_project.IsArchived && !_busy;
        AddPanelButton.IsEnabled = !_project.IsArchived && !_busy;
        ResetLayoutButton.IsEnabled = !_project.IsArchived && !_busy;
        ArchiveMenuItem.IsEnabled = !_busy;
        DeleteMenuItem.IsEnabled = !_busy;

        ApplyOverview();
        UpdateResponsiveMode();
        RenderWorkspace();
    }

    private void ApplyOverview()
    {
        if (_project is null) return;
        var isResearch = _project.Kind == ProjectKindCodes.Research;
        var currentStage = GetCurrentStageText();
        OverviewPhaseText.Text = currentStage;

        if (isResearch)
        {
            OverviewTitle.Text = W("研究概览", "Research overview", "研究概要");
            WorkspaceTitleText.Text = W("自定义研究工作台", "Custom research workspace", "カスタム研究ワークスペース");
            WorkspaceSubtitleText.Text = W("研究框架、图表、数据与文献都可以自由组合", "Combine framework, results, data and references freely", "研究フレーム、図表、データ、文献を自由に組み合わせられます");
            OverviewDescriptionText.Text = _project.ResearchDetails?.ResearchSubject
                ?? W("尚未填写研究对象或核心研究问题。", "No research subject or core question yet.", "研究対象・中心課題はまだ設定されていません。");
            OverviewLabel1.Text = W("研究领域", "Research field", "研究分野");
            OverviewValue1.Text = EmptyDash(_project.ResearchDetails?.ResearchField);
            OverviewLabel2.Text = W("研究对象", "Research subject", "研究対象");
            OverviewValue2.Text = TrimForMetric(_project.ResearchDetails?.ResearchSubject);
            OverviewLabel3.Text = W("研究方法", "Methods", "研究方法");
            OverviewValue3.Text = TrimForMetric(_project.ResearchDetails?.ResearchMethods);
            OverviewLabel4.Text = W("时间节点", "Milestones", "マイルストーン");
            OverviewValue4.Text = _project.Milestones.Count.ToString(CultureInfo.CurrentCulture);
        }
        else
        {
            OverviewTitle.Text = W("项目概览", "Project overview", "プロジェクト概要");
            WorkspaceTitleText.Text = W("自定义工作台", "Custom workspace", "カスタムワークスペース");
            WorkspaceSubtitleText.Text = W("拖动与缩放面板，按项目需要组织自己的工作空间", "Arrange panels around the needs of this project", "プロジェクトに合わせてパネルを自由に配置できます");
            OverviewDescriptionText.Text = _project.Description
                ?? W("尚未填写项目说明。", "No project description yet.", "プロジェクト説明はまだありません。");
            OverviewLabel1.Text = W("项目类型", "Project type", "プロジェクト種別");
            OverviewValue1.Text = ProjectPresentation.GetTypeName(_project, _localization);
            OverviewLabel2.Text = W("当前阶段", "Current stage", "現在の段階");
            OverviewValue2.Text = currentStage;
            OverviewLabel3.Text = W("时间节点", "Milestones", "マイルストーン");
            OverviewValue3.Text = _project.Milestones.Count.ToString(CultureInfo.CurrentCulture);
            OverviewLabel4.Text = W("工作文件夹", "Work folder", "作業フォルダー");
            OverviewValue4.Text = _project.WorkFolder?.DisplayName ?? W("未设置", "Not set", "未設定");
        }
    }

    private string GetCurrentStageText()
    {
        if (_project is null) return "—";
        var today = DateOnly.FromDateTime(DateTime.Now);
        var upcoming = _project.Milestones.OrderBy(m => m.Date).FirstOrDefault(m => m.Date >= today);
        if (upcoming is not null) return upcoming.Title;
        var latest = _project.Milestones.OrderByDescending(m => m.Date).FirstOrDefault();
        if (latest is not null) return latest.Title;
        return _project.Kind == ProjectKindCodes.Research
            ? W("研究进行中", "Research in progress", "研究中")
            : W("进行中", "In progress", "進行中");
    }

    private static string EmptyDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    private static string TrimForMetric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        var trimmed = value.Trim();
        return trimmed.Length <= 28 ? trimmed : trimmed[..27] + "…";
    }

    private void UpdateResponsiveMode()
    {
        var width = ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? 1280;
        _columnCount = width >= 1280 ? 12 : width >= 960 ? 8 : width >= 720 ? 6 : 1;
        var canEditLayout = _columnCount == 12 && _project is { IsArchived: false } && !_busy;
        EditLayoutButton.IsEnabled = canEditLayout;
        if (!canEditLayout && _layoutEditing)
        {
            _layoutEditing = false;
            _selectedPanelId = null;
        }
        ToolTipService.SetToolTip(EditLayoutButton, canEditLayout
            ? W("拖动或缩放磁贴", "Drag or resize tiles", "タイルをドラッグ・サイズ変更")
            : W("展开窗口后可编辑布局；窄窗口只改变显示，不覆盖已保存布局。", "Widen the window to edit. Narrow layouts never overwrite the saved 12-column layout.", "ウィンドウを広げると編集できます。狭い表示は保存済み12列レイアウトを上書きしません。"));
    }

    private void RenderWorkspace()
    {
        if (_project?.WorkspaceLayout is null || TileCanvas.ActualWidth <= 0) return;
        ProjectWorkspaceLayoutService.Normalize(_project.WorkspaceLayout, _project.Kind);
        TileCanvas.Children.Clear();
        _tileViews.Clear();

        var renderPanels = BuildResponsivePositions(_project.WorkspaceLayout.Panels, _columnCount);
        var width = Math.Max(1, TileCanvas.ActualWidth);
        _unitWidth = (width - TileGap * (_columnCount - 1)) / _columnCount;
        var maxBottom = 0d;

        foreach (var entry in renderPanels)
        {
            var panel = entry.Panel;
            var left = entry.X * (_unitWidth + TileGap);
            var top = entry.Y * (TileRowHeight + TileGap);
            var panelWidth = entry.Width * _unitWidth + (entry.Width - 1) * TileGap;
            var panelHeight = entry.Height * TileRowHeight + (entry.Height - 1) * TileGap;
            var tile = CreateTile(panel, panelWidth, panelHeight);
            Canvas.SetLeft(tile, left);
            Canvas.SetTop(tile, top);
            TileCanvas.Children.Add(tile);
            _tileViews[panel.Id] = tile;
            maxBottom = Math.Max(maxBottom, top + panelHeight);
        }

        if (_project.WorkspaceLayout.Panels.Count == 0)
        {
            var empty = CreateEmptyWorkspaceCard();
            Canvas.SetLeft(empty, 18);
            Canvas.SetTop(empty, 18);
            TileCanvas.Children.Add(empty);
            maxBottom = 180;
        }

        TileCanvas.Height = Math.Max(540, maxBottom + 24);
        WorkspaceSurface.MinHeight = TileCanvas.Height;
        UndoLayoutButton.Visibility = _layoutUndo is null || !_layoutEditing ? Visibility.Collapsed : Visibility.Visible;
        EditLayoutButton.Content = _layoutEditing
            ? W("完成布局", "Done", "完了")
            : W("编辑布局", "Edit layout", "レイアウトを編集");
    }

    private Border CreateTile(ProjectWorkspacePanel panel, double width, double height)
    {
        var selected = _selectedPanelId == panel.Id;
        var border = new Border
        {
            Width = Math.Max(86, width), Height = Math.Max(82, height),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
            Background = ResourceBrush("CardBackgroundFillColorSecondaryBrush"),
            BorderBrush = selected ? ResourceBrush("AccentFillColorDefaultBrush") : ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(selected ? 2 : 1), Tag = panel.Id
        };
        border.Tapped += OnTileTapped;

        var root = new Grid { RowSpacing = 8 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (_layoutEditing)
        {
            var drag = new TextBlock
            {
                Text = "⠿", Tag = panel.Id, Opacity = .62, FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0)
            };
            drag.PointerPressed += OnPanelHandlePointerPressed;
            drag.PointerMoved += OnPanelHandlePointerMoved;
            drag.PointerReleased += OnPanelHandlePointerReleased;
            drag.PointerCanceled += OnPanelHandlePointerCanceled;
            header.Children.Add(drag);
        }

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(panel.Title) ? PanelName(panel.Kind) : panel.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        if (_layoutEditing)
        {
            var size = new TextBlock
            {
                Text = $"{panel.Width}×{panel.Height}", FontSize = 11, Opacity = .55,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(size, 2);
            header.Children.Add(size);
        }

        var menu = new Button { Content = "⋯", Tag = panel.Id, MinWidth = 32, Padding = new Thickness(5, 1, 5, 1) };
        menu.Flyout = CreatePanelMenu(panel);
        Grid.SetColumn(menu, 3);
        header.Children.Add(menu);
        root.Children.Add(header);

        var body = BuildPanelBody(panel);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        if (_layoutEditing)
        {
            var resize = new Border
            {
                Width = 16, Height = 16, Tag = panel.Id,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = ResourceBrush("AccentFillColorSecondaryBrush"),
                CornerRadius = new CornerRadius(3)
            };
            resize.PointerPressed += OnResizeHandlePointerPressed;
            resize.PointerMoved += OnPanelHandlePointerMoved;
            resize.PointerReleased += OnPanelHandlePointerReleased;
            resize.PointerCanceled += OnPanelHandlePointerCanceled;
            Grid.SetRow(resize, 1);
            root.Children.Add(resize);
        }

        border.Child = root;
        return border;
    }

    private FlyoutBase CreatePanelMenu(ProjectWorkspacePanel panel)
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = W("打开 / 查看详情", "Open / view details", "開く / 詳細を見る"), Tag = panel.Id };
        open.Click += OnOpenPanel;
        menu.Items.Add(open);

        var settings = new MenuFlyoutItem { Text = W("面板设置", "Panel settings", "パネル設定"), Tag = panel.Id, IsEnabled = !_project!.IsArchived };
        settings.Click += OnPanelSettings;
        menu.Items.Add(settings);

        if (!_project!.IsArchived)
        {
            var duplicate = new MenuFlyoutItem { Text = W("复制面板", "Duplicate panel", "パネルを複製"), Tag = panel.Id };
            duplicate.Click += OnDuplicatePanel;
            menu.Items.Add(duplicate);
            menu.Items.Add(new MenuFlyoutSeparator());
            var delete = new MenuFlyoutItem { Text = W("删除面板", "Remove panel", "パネルを削除"), Tag = panel.Id };
            delete.Click += OnRemovePanel;
            menu.Items.Add(delete);
        }
        return menu;
    }

    private UIElement BuildPanelBody(ProjectWorkspacePanel panel) => panel.Kind switch
    {
        ProjectWorkspacePanelKinds.ImageShowcase => BuildImageShowcase(panel),
        ProjectWorkspacePanelKinds.Milestones or ProjectWorkspacePanelKinds.ResearchProgress => BuildMilestones(panel),
        ProjectWorkspacePanelKinds.Description => BuildDescription(),
        ProjectWorkspacePanelKinds.Inspirations => BuildInspirations(panel),
        ProjectWorkspacePanelKinds.Files or ProjectWorkspacePanelKinds.DataAndScripts => BuildFiles(panel),
        ProjectWorkspacePanelKinds.KeyStrategies => BuildStrategies(panel),
        ProjectWorkspacePanelKinds.ResearchFramework => BuildResearchFramework(panel),
        ProjectWorkspacePanelKinds.ResearchQuestion => BuildResearchQuestion(),
        ProjectWorkspacePanelKinds.Chart => BuildChart(),
        ProjectWorkspacePanelKinds.Literature => BuildLiterature(),
        ProjectWorkspacePanelKinds.TextNote => BuildTextNote(panel),
        _ => BuildCustom(panel)
    };

    private UIElement BuildImageShowcase(ProjectWorkspacePanel panel)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition());
        var labels = _linkedInspirations.Select(item => item.Title).Take(4).ToList();
        while (labels.Count < 4)
            labels.Add(W(labels.Count switch { 0 => "总体设计", 1 => "滨水空间", 2 => "节点透视", _ => "参考案例" },
                         labels.Count switch { 0 => "Master plan", 1 => "Waterfront", 2 => "Key view", _ => "Reference" },
                         labels.Count switch { 0 => "全体計画", 1 => "水辺空間", 2 => "主要ビュー", _ => "参考事例" }));
        for (var i = 0; i < 4; i++)
        {
            var card = new Border
            {
                Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10), Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE91B", FontSize = 16, Opacity = .55 },
                        new TextBlock { Text = labels[i], TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .78 }
                    }
                }
            };
            Grid.SetColumn(card, i % 2); Grid.SetRow(card, i / 2); grid.Children.Add(card);
        }
        return grid;
    }

    private UIElement BuildMilestones(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 8 };
        if (_project is null) return stack;
        var ordered = _project.Milestones.OrderBy(item => item.Date).Take(panel.Height <= 1 ? 2 : 4).ToArray();
        if (ordered.Length == 0)
        {
            stack.Children.Add(new TextBlock { Text = W("暂无时间节点。", "No milestones yet.", "マイルストーンはまだありません。"), Opacity = .62, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            foreach (var milestone in ordered)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = ResourceBrush("AccentFillColorDefaultBrush"), VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(dot);
                var text = new StackPanel { Spacing = 1 };
                text.Children.Add(new TextBlock { Text = milestone.Date.ToString("MM/dd", CultureInfo.CurrentCulture), FontSize = 11, Opacity = .58 });
                text.Children.Add(new TextBlock { Text = milestone.Title, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(text, 1); row.Children.Add(text); stack.Children.Add(row);
            }
        }
        return stack;
    }

    private UIElement BuildDescription() => new TextBlock
    {
        Text = _project?.Description ?? W("点击“编辑项目”添加项目说明。", "Use Edit project to add a description.", "「プロジェクトを編集」から説明を追加できます。"),
        TextWrapping = TextWrapping.Wrap, MaxLines = 6, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .8
    };

    private UIElement BuildInspirations(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 8 };
        var count = panel.Height <= 1 ? 2 : 4;
        foreach (var item in _linkedInspirations.Take(count))
        {
            var card = new Border
            {
                Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"), CornerRadius = new CornerRadius(5), Padding = new Thickness(8),
                Child = new TextBlock { Text = item.Title, TextTrimming = TextTrimming.CharacterEllipsis }
            };
            stack.Children.Add(card);
        }
        if (_linkedInspirations.Count == 0)
            stack.Children.Add(new TextBlock { Text = W("暂无关联灵感", "No linked inspirations", "関連付けられたアイデアはありません"), Opacity = .62 });
        return stack;
    }

    private UIElement BuildFiles(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 8 };
        if (_project?.WorkFolder is { } folder)
        {
            stack.Children.Add(FileRow("\uE8B7", folder.DisplayName, folder.DisplayPath));
        }
        else
        {
            stack.Children.Add(FileRow("\uE8B7", W("工作文件夹", "Work folder", "作業フォルダー"), W("尚未设置", "Not configured", "未設定")));
        }
        stack.Children.Add(FileRow("\uE7C3", W("项目归档", "Project archive", "プロジェクトアーカイブ"), _project?.IsArchived == true ? W("已归档", "Archived", "アーカイブ済み") : W("本地项目", "Local project", "ローカルプロジェクト")));
        if (panel.Height > 1) stack.Children.Add(FileRow("\uE753", W("云存档", "Cloud archive", "クラウド保存"), W("由数据管理统一同步", "Managed by Data Management", "データ管理から同期")));
        return stack;
    }

    private static UIElement FileRow(string glyph, string title, string subtitle)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, Opacity = .62, VerticalAlignment = VerticalAlignment.Center });
        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Opacity = .55, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(text, 1); row.Children.Add(text); return row;
    }

    private UIElement BuildStrategies(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 6 };
        var source = _project?.PlanningRequirements;
        var items = string.IsNullOrWhiteSpace(source)
            ? new[] { W("TOD 门户", "TOD gateway", "TOD ゲートウェイ"), W("滨水更新", "Waterfront renewal", "水辺更新"), W("空间导则", "Spatial guidance", "空間ガイドライン") }
            : source.Split(['\r', '\n', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4).ToArray();
        foreach (var item in items)
        {
            stack.Children.Add(new Border
            {
                Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = item, TextTrimming = TextTrimming.CharacterEllipsis }
            });
        }
        return stack;
    }

    private UIElement BuildResearchFramework(ProjectWorkspacePanel panel)
    {
        var details = _project?.ResearchDetails;
        var grid = new Grid { ColumnSpacing = 8 };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var phases = new[]
        {
            (W("1 研究问题", "1 Question", "1 研究課題"), TrimForMetric(details?.ResearchSubject)),
            (W("2 数据与对象", "2 Data & subject", "2 データ・対象"), TrimForMetric(details?.ResearchField)),
            (W("3 分析方法", "3 Methods", "3 分析方法"), TrimForMetric(details?.ResearchMethods)),
            (W("4 预期产出", "4 Output", "4 成果"), W("图表 · 结论 · 建议", "Figures · findings · guidance", "図表・結論・提案"))
        };
        for (var i = 0; i < phases.Length; i++)
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = phases[i].Item1, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = phases[i].Item2, TextWrapping = TextWrapping.Wrap, MaxLines = 4, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .65 });
            var card = new Border { Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Child = stack };
            Grid.SetColumn(card, i); grid.Children.Add(card);
        }
        return grid;
    }

    private UIElement BuildResearchQuestion() => new TextBlock
    {
        Text = _project?.ResearchDetails?.ResearchSubject ?? W("尚未填写核心研究问题。", "No core research question yet.", "中心となる研究課題はまだありません。"),
        TextWrapping = TextWrapping.Wrap, MaxLines = 8, TextTrimming = TextTrimming.CharacterEllipsis
    };

    private UIElement BuildChart()
    {
        var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        var heights = new[] { 34d, 58d, 82d, 112d, 46d, 72d, 96d };
        for (var i = 0; i < heights.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = new Border
            {
                Height = heights[i], Background = ResourceBrush("AccentFillColorSecondaryBrush"),
                CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(bar, i); grid.Children.Add(bar);
        }
        return grid;
    }

    private UIElement BuildLiterature()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(MetricLine("34", W("篇文献", "references", "件の文献")));
        stack.Children.Add(MetricLine("6", W("条待读", "to read", "未読")));
        stack.Children.Add(MetricLine("12", W("条已标注", "annotated", "注釈済み")));
        return stack;
    }

    private static UIElement MetricLine(string value, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, MinWidth = 34 });
        row.Children.Add(new TextBlock { Text = label, Opacity = .62, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private UIElement BuildTextNote(ProjectWorkspacePanel panel) => new TextBlock
    {
        Text = panel.Settings.TryGetValue("content", out var content) && !string.IsNullOrWhiteSpace(content)
            ? content : W("双击或打开面板设置添加笔记。", "Open panel settings to add a note.", "パネル設定からメモを追加できます。"),
        TextWrapping = TextWrapping.Wrap, MaxLines = 8, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .8
    };

    private UIElement BuildCustom(ProjectWorkspacePanel panel) => new TextBlock
    {
        Text = panel.Settings.TryGetValue("content", out var content) ? content : W("自定义面板", "Custom panel", "カスタムパネル"),
        TextWrapping = TextWrapping.Wrap, Opacity = .72
    };

    private Border CreateEmptyWorkspaceCard()
    {
        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 24, Opacity = .58 });
        stack.Children.Add(new TextBlock { Text = W("添加面板", "Add a panel", "パネルを追加"), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = W("从图片、时间节点、文本、文件或自定义面板开始", "Start with images, milestones, notes, files or a custom panel", "画像・マイルストーン・メモ・ファイル・カスタムパネルから始められます"), Opacity = .58, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        return new Border
        {
            Width = Math.Max(240, TileCanvas.ActualWidth - 36), Height = 150,
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(20), Child = stack
        };
    }

    private IReadOnlyList<RenderPanel> BuildResponsivePositions(IReadOnlyList<ProjectWorkspacePanel> panels, int columns)
    {
        var ordered = panels.OrderBy(panel => panel.Y).ThenBy(panel => panel.X).ToArray();
        if (columns == 12) return ordered.Select(panel => new RenderPanel(panel, panel.X, panel.Y, panel.Width, panel.Height)).ToArray();

        var result = new List<RenderPanel>();
        if (columns == 1)
        {
            var y = 0;
            foreach (var panel in ordered)
            {
                var height = Math.Clamp(panel.Height, 1, 4);
                result.Add(new RenderPanel(panel, 0, y, 1, height));
                y += height + 1;
            }
            return result;
        }

        foreach (var panel in ordered)
        {
            var width = Math.Clamp((int)Math.Round(panel.Width * columns / 12d), 1, columns);
            var height = Math.Max(1, panel.Height);
            var placed = false;
            for (var y = 0; y < 96 && !placed; y++)
            for (var x = 0; x <= columns - width; x++)
            {
                var candidate = new RenderPanel(panel, x, y, width, height);
                if (result.All(existing => !RenderIntersects(candidate, existing)))
                {
                    result.Add(candidate); placed = true; break;
                }
            }
            if (!placed)
            {
                var bottom = result.Select(item => item.Y + item.Height).DefaultIfEmpty(0).Max();
                result.Add(new RenderPanel(panel, 0, bottom, width, height));
            }
        }
        return result;
    }

    private static bool RenderIntersects(RenderPanel left, RenderPanel right) =>
        left.X < right.X + right.Width && left.X + left.Width > right.X &&
        left.Y < right.Y + right.Height && left.Y + left.Height > right.Y;

    private string PanelName(string kind) => kind switch
    {
        ProjectWorkspacePanelKinds.ImageShowcase => W("图片展示架", "Image showcase", "画像ショーケース"),
        ProjectWorkspacePanelKinds.Milestones => W("时间节点", "Milestones", "マイルストーン"),
        ProjectWorkspacePanelKinds.Description => W("项目说明", "Project notes", "プロジェクト説明"),
        ProjectWorkspacePanelKinds.Inspirations => W("关联灵感", "Linked inspirations", "関連アイデア"),
        ProjectWorkspacePanelKinds.Files => W("文件入口", "Files", "ファイル"),
        ProjectWorkspacePanelKinds.KeyStrategies => W("重点策略", "Key strategies", "重点戦略"),
        ProjectWorkspacePanelKinds.ResearchFramework => W("研究框架", "Research framework", "研究フレーム"),
        ProjectWorkspacePanelKinds.ResearchQuestion => W("核心研究问题", "Core research question", "中心研究課題"),
        ProjectWorkspacePanelKinds.Chart => W("结果图表", "Results chart", "結果チャート"),
        ProjectWorkspacePanelKinds.Literature => W("文献资料", "Literature", "文献資料"),
        ProjectWorkspacePanelKinds.DataAndScripts => W("数据与脚本", "Data & scripts", "データ・スクリプト"),
        ProjectWorkspacePanelKinds.ResearchProgress => W("研究进度", "Research progress", "研究進捗"),
        ProjectWorkspacePanelKinds.TextNote => W("文本笔记", "Text note", "テキストメモ"),
        _ => W("自定义面板", "Custom panel", "カスタムパネル")
    };

    private async void OnEditOverview(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived || _busy) return;

        var name = new TextBox { Header = _localization.GetString("Project_Field_Name"), Text = _project.Name, MaxLength = ProjectValidation.MaxNameLength };
        var type = new ComboBox { Header = _project.Kind == ProjectKindCodes.Research ? _localization.GetString("ResearchProject_Field_Type") : _localization.GetString("Project_Field_Type"), HorizontalAlignment = HorizontalAlignment.Stretch };
        var options = (_project.Kind == ProjectKindCodes.Research ? ResearchProjectTypeCodes.All : ProjectTypeCodes.All)
            .Select(code => new ProjectTypeOption(code, _project.Kind == ProjectKindCodes.Research ? ProjectPresentation.GetResearchTypeName(code, _localization) : ProjectPresentation.GetDesignTypeName(code, _localization))).ToArray();
        type.ItemsSource = options; type.DisplayMemberPath = nameof(ProjectTypeOption.Name);
        type.SelectedItem = options.FirstOrDefault(option => option.Code == _project.Type) ?? options[0];
        var customType = new TextBox { Header = _localization.GetString("Project_Field_CustomType"), Text = _project.CustomType ?? string.Empty, MaxLength = ProjectValidation.MaxTypeLength, Visibility = _project.Type == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed };
        type.SelectionChanged += (_, _) => customType.Visibility = (type.SelectedItem as ProjectTypeOption)?.Code == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed;

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(name); stack.Children.Add(type); stack.Children.Add(customType);
        TextBox? description = null, field = null, subject = null, methods = null, strategies = null;
        if (_project.Kind == ProjectKindCodes.Research)
        {
            field = new TextBox { Header = _localization.GetString("ResearchProject_Field_Field"), Text = _project.ResearchDetails?.ResearchField ?? string.Empty, MaxLength = ProjectValidation.MaxResearchFieldLength };
            subject = new TextBox { Header = _localization.GetString("ResearchProject_Field_Subject"), Text = _project.ResearchDetails?.ResearchSubject ?? string.Empty, MaxLength = ProjectValidation.MaxResearchSubjectLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96 };
            methods = new TextBox { Header = _localization.GetString("ResearchProject_Field_Methods"), Text = _project.ResearchDetails?.ResearchMethods ?? string.Empty, MaxLength = ProjectValidation.MaxResearchMethodsLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96 };
            stack.Children.Add(field); stack.Children.Add(subject); stack.Children.Add(methods);
        }
        else
        {
            description = new TextBox { Header = _localization.GetString("Project_Field_Description"), Text = _project.Description ?? string.Empty, MaxLength = ProjectValidation.MaxDescriptionLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
            strategies = new TextBox { Header = W("重点策略", "Key strategies", "重点戦略"), Text = _project.PlanningRequirements ?? string.Empty, MaxLength = ProjectValidation.MaxPlanningRequirementsLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96 };
            stack.Children.Add(description); stack.Children.Add(strategies);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = W("编辑项目概览", "Edit project overview", "プロジェクト概要を編集"),
            Content = new ScrollViewer { Content = stack, MaxHeight = Math.Max(300, XamlRoot.Size.Height - 260), VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = _localization.GetString("Project_Action_Save"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary
        };
        if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return;

        var candidate = CloneProject(_project);
        candidate.Name = name.Text;
        candidate.Type = (type.SelectedItem as ProjectTypeOption)?.Code ?? candidate.Type;
        candidate.CustomType = candidate.Type == ProjectTypeCodes.Other ? customType.Text : null;
        if (candidate.Kind == ProjectKindCodes.Research)
        {
            candidate.ResearchDetails ??= new();
            candidate.ResearchDetails.ResearchField = field!.Text;
            candidate.ResearchDetails.ResearchSubject = subject!.Text;
            candidate.ResearchDetails.ResearchMethods = methods!.Text;
        }
        else
        {
            candidate.Description = description!.Text;
            candidate.PlanningRequirements = strategies!.Text;
        }
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await SaveCandidateAsync(candidate);
    }

    private async void OnAddPanel(object sender, RoutedEventArgs e) => OpenAddPanelDrawer();

    private void OpenAddPanelDrawer()
    {
        if (_project is null || _project.IsArchived) return;
        DrawerTitle.Text = W("添加面板", "Add panel", "パネルを追加");
        DrawerSubtitle.Text = W("选择一种面板；添加后可在布局模式调整位置和大小", "Choose a panel. Position and size can be changed in layout mode.", "パネルを選択してください。追加後に位置とサイズを調整できます。");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        var searchHint = new TextBox { PlaceholderText = W("搜索面板类型", "Search panel types", "パネルを検索"), IsEnabled = false };
        DrawerContent.Children.Add(searchHint);
        foreach (var kind in ProjectWorkspaceLayoutService.GetAllowedPanelKinds(_project.Kind))
        {
            var button = new Button
            {
                Tag = kind, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12), CornerRadius = new CornerRadius(6)
            };
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = PanelName(kind), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var defaultSize = ProjectWorkspaceLayoutService.DefaultSize(kind);
            text.Children.Add(new TextBlock { Text = W($"默认 {defaultSize.Width}×{defaultSize.Height} 网格", $"Default {defaultSize.Width}×{defaultSize.Height} grid", $"既定 {defaultSize.Width}×{defaultSize.Height} グリッド"), FontSize = 11, Opacity = .55 });
            row.Children.Add(text);
            var plus = new FontIcon { Glyph = "\uE710", VerticalAlignment = VerticalAlignment.Center, Opacity = .7 };
            Grid.SetColumn(plus, 1); row.Children.Add(plus); button.Content = row; button.Click += OnAddPanelKind;
            DrawerContent.Children.Add(button);
        }
        ShowDrawer();
    }

    private async void OnAddPanelKind(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not Button { Tag: string kind }) return;
        RememberLayoutForUndo();
        var panel = ProjectWorkspaceLayoutService.AddPanel(_project.WorkspaceLayout, _project.Kind, kind);
        _selectedPanelId = panel.Id;
        await PersistProjectAsync(showSuccess: false);
        CloseDrawer();
        RenderWorkspace();
    }

    private void OnToggleLayoutEditing(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived || _columnCount != 12) return;
        _layoutEditing = !_layoutEditing;
        if (!_layoutEditing) _selectedPanelId = null;
        RenderWorkspace();
    }

    private async void OnUndoLayout(object sender, RoutedEventArgs e)
    {
        if (_project is null || _layoutUndo is null) return;
        var current = ProjectWorkspaceLayoutService.Clone(ProjectWorkspaceLayoutService.EnsureLayout(_project));
        _project.WorkspaceLayout = _layoutUndo;
        _layoutUndo = current;
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
    }

    private async void OnResetLayout(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived) return;
        if (!await ConfirmAsync(W("恢复默认布局", "Reset workspace layout", "既定レイアウトに戻す"), W("只会重置面板布局，不会删除项目内容。", "Only the panel layout is reset. Project content is not deleted.", "パネル配置のみをリセットし、プロジェクト内容は削除しません。"))) return;
        RememberLayoutForUndo();
        _project.WorkspaceLayout = ProjectWorkspaceLayoutService.CreateDefault(_project.Kind);
        _selectedPanelId = null;
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
    }

    private void OnTileTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_layoutEditing || sender is not Border { Tag: Guid id }) return;
        _selectedPanelId = id;
        RenderWorkspace();
    }

    private void OnOpenPanel(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not MenuFlyoutItem { Tag: Guid id }) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null) return;
        if (panel.Kind is ProjectWorkspacePanelKinds.Milestones or ProjectWorkspacePanelKinds.ResearchProgress)
            OpenMilestonesDrawer();
        else if (panel.Kind == ProjectWorkspacePanelKinds.Inspirations)
            App.OpenInspirationManagement(_project.Kind == ProjectKindCodes.Research ? InspirationCategory.Research : InspirationCategory.Design);
        else if (panel.Kind is ProjectWorkspacePanelKinds.Files or ProjectWorkspacePanelKinds.DataAndScripts)
            OpenFileDrawer();
        else
            OpenPanelSettingsDrawer(panel);
    }

    private void OnPanelSettings(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not MenuFlyoutItem { Tag: Guid id }) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is not null) OpenPanelSettingsDrawer(panel);
    }

    private void OpenPanelSettingsDrawer(ProjectWorkspacePanel panel)
    {
        if (_project is null) return;
        DrawerTitle.Text = W("面板设置", "Panel settings", "パネル設定");
        DrawerSubtitle.Text = PanelName(panel.Kind);
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        var title = new TextBox { Header = W("面板标题", "Panel title", "パネル名"), Text = panel.Title ?? string.Empty, PlaceholderText = PanelName(panel.Kind), IsReadOnly = _project.IsArchived };
        DrawerContent.Children.Add(title);

        DrawerContent.Children.Add(SectionLabel(W("尺寸", "Size", "サイズ")));
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var sizes = new[] { (1, 1), (3, 1), (4, 2), (6, 2), (6, 3), (9, 3), (12, 2) };
        foreach (var size in sizes)
        {
            var button = new Button { Content = $"{size.Item1}×{size.Item2}", Tag = size, IsEnabled = !_project.IsArchived };
            button.Click += async (_, _) =>
            {
                RememberLayoutForUndo();
                ProjectWorkspaceLayoutService.ResizePanel(_project.WorkspaceLayout!, panel.Id, size.Item1, size.Item2);
                await PersistProjectAsync(showSuccess: false); RenderWorkspace();
            };
            sizeRow.Children.Add(button);
        }
        DrawerContent.Children.Add(sizeRow);

        TextBox? note = null;
        if (panel.Kind is ProjectWorkspacePanelKinds.TextNote or ProjectWorkspacePanelKinds.Custom)
        {
            note = new TextBox
            {
                Header = W("内容", "Content", "内容"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 150,
                Text = panel.Settings.TryGetValue("content", out var content) ? content : string.Empty, IsReadOnly = _project.IsArchived
            };
            DrawerContent.Children.Add(note);
        }

        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer(); DrawerFooter.Children.Add(cancel);
        if (!_project.IsArchived)
        {
            var save = new Button { Content = _localization.GetString("Project_Action_Save") };
            save.Click += async (_, _) =>
            {
                panel.Title = string.IsNullOrWhiteSpace(title.Text) ? null : title.Text.Trim();
                if (note is not null) panel.Settings["content"] = note.Text;
                await PersistProjectAsync(showSuccess: false); CloseDrawer(); RenderWorkspace();
            };
            DrawerFooter.Children.Add(save);
        }
        ShowDrawer();
    }

    private async void OnDuplicatePanel(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not MenuFlyoutItem { Tag: Guid id }) return;
        RememberLayoutForUndo();
        var duplicate = ProjectWorkspaceLayoutService.DuplicatePanel(_project.WorkspaceLayout, _project.Kind, id);
        _selectedPanelId = duplicate.Id;
        await PersistProjectAsync(showSuccess: false); RenderWorkspace();
    }

    private async void OnRemovePanel(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not MenuFlyoutItem { Tag: Guid id }) return;
        if (!await ConfirmAsync(W("删除面板？", "Remove panel?", "パネルを削除しますか？"), W("只会移除这个面板，不会删除项目内容或源文件。", "This removes only the panel, not project content or source files.", "パネルだけを削除し、プロジェクト内容や元ファイルは削除しません。"))) return;
        RememberLayoutForUndo();
        ProjectWorkspaceLayoutService.RemovePanel(_project.WorkspaceLayout, id);
        if (_selectedPanelId == id) _selectedPanelId = null;
        await PersistProjectAsync(showSuccess: false); RenderWorkspace();
    }

    private void OpenMilestonesDrawer()
    {
        if (_project is null) return;
        DrawerTitle.Text = W("时间节点", "Milestones", "マイルストーン");
        DrawerSubtitle.Text = W("编辑日期、标题与说明", "Edit dates, titles and notes", "日付・タイトル・説明を編集");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        if (!_project.IsArchived)
        {
            var add = new Button { Content = W("＋ 新增节点", "+ Add milestone", "＋ マイルストーンを追加"), HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += OnAddMilestone; DrawerContent.Children.Add(add);
        }
        foreach (var milestone in _project.Milestones.OrderBy(item => item.Date).ThenBy(item => item.DisplayOrder))
        {
            var card = new Border { Style = (Style)Application.Current.Resources["SettingsSectionCardStyle"], Padding = new Thickness(12) };
            var row = new Grid { ColumnSpacing = 8 }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = milestone.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            text.Children.Add(new TextBlock { Text = milestone.Date.ToString("d", CultureInfo.CurrentCulture), Opacity = .58 });
            if (!string.IsNullOrWhiteSpace(milestone.Notes)) text.Children.Add(new TextBlock { Text = milestone.Notes, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, Opacity = .68 });
            row.Children.Add(text);
            if (!_project.IsArchived)
            {
                var menu = new Button { Content = "⋯", Tag = milestone.Id, MinWidth = 32 };
                var flyout = new MenuFlyout();
                var edit = new MenuFlyoutItem { Text = _localization.GetString("Milestone_Action_Edit"), Tag = milestone.Id }; edit.Click += OnEditMilestone;
                var delete = new MenuFlyoutItem { Text = _localization.GetString("Milestone_Action_Delete"), Tag = milestone.Id }; delete.Click += OnDeleteMilestone;
                flyout.Items.Add(edit); flyout.Items.Add(delete); menu.Flyout = flyout; Grid.SetColumn(menu, 1); row.Children.Add(menu);
            }
            card.Child = row; DrawerContent.Children.Add(card);
        }
        if (_project.Milestones.Count == 0) DrawerContent.Children.Add(new TextBlock { Text = _localization.GetString("Milestone_Empty"), Opacity = .62 });
        ShowDrawer();
    }

    private void OpenFileDrawer()
    {
        if (_project is null) return;
        DrawerTitle.Text = W("文件入口", "Files", "ファイル");
        DrawerSubtitle.Text = _project.WorkFolder?.DisplayPath ?? W("尚未设置工作文件夹", "No work folder configured", "作業フォルダーは未設定です");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();
        var summary = new Border { Style = (Style)Application.Current.Resources["SettingsSectionCardStyle"], Child = new TextBlock { Text = _project.WorkFolder?.DisplayPath ?? W("选择一个项目工作文件夹后，可从磁贴快速打开。", "Choose a project work folder to open it directly from the tile.", "作業フォルダーを選択するとタイルから直接開けます。"), TextWrapping = TextWrapping.Wrap } };
        DrawerContent.Children.Add(summary);
        var open = new Button { Content = _localization.GetString("Folder_Action_Open"), IsEnabled = _project.WorkFolder is { RequiresReselection: false } }; open.Click += OnOpenFolder;
        DrawerContent.Children.Add(open);
        if (!_project.IsArchived)
        {
            var select = new Button { Content = _localization.GetString(_project.WorkFolder is null ? "Folder_Action_Select" : "Folder_Action_Replace") }; select.Click += OnSelectFolder;
            DrawerContent.Children.Add(select);
            if (_project.WorkFolder is not null)
            {
                var clear = new Button { Content = _localization.GetString("Folder_Action_Clear") }; clear.Click += OnClearFolder; DrawerContent.Children.Add(clear);
            }
        }
        ShowDrawer();
    }

    private void OnPanelHandlePointerPressed(object sender, PointerRoutedEventArgs e) => BeginPointerOperation(sender, e, PointerOperation.Move);
    private void OnResizeHandlePointerPressed(object sender, PointerRoutedEventArgs e) => BeginPointerOperation(sender, e, PointerOperation.Resize);

    private void BeginPointerOperation(object sender, PointerRoutedEventArgs e, PointerOperation operation)
    {
        if (!_layoutEditing || _columnCount != 12 || _project?.WorkspaceLayout is null || sender is not FrameworkElement { Tag: Guid id } handle) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null || !_tileViews.TryGetValue(id, out var tile)) return;
        RememberLayoutForUndo();
        _pointerPanel = panel; _pointerHandle = handle; _pointerOperation = operation; _pointerId = e.Pointer.PointerId;
        _pointerStart = e.GetCurrentPoint(TileCanvas).Position;
        _pointerStartLeft = Canvas.GetLeft(tile); _pointerStartTop = Canvas.GetTop(tile);
        _pointerStartWidth = tile.Width; _pointerStartHeight = tile.Height;
        handle.CapturePointer(e.Pointer); e.Handled = true;
    }

    private void OnPanelHandlePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerPanel is null || _pointerHandle is null || e.Pointer.PointerId != _pointerId || !_tileViews.TryGetValue(_pointerPanel.Id, out var tile)) return;
        var point = e.GetCurrentPoint(TileCanvas).Position;
        var dx = point.X - _pointerStart.X; var dy = point.Y - _pointerStart.Y;
        if (_pointerOperation == PointerOperation.Move)
        {
            Canvas.SetLeft(tile, Math.Max(0, _pointerStartLeft + dx));
            Canvas.SetTop(tile, Math.Max(0, _pointerStartTop + dy));
        }
        else if (_pointerOperation == PointerOperation.Resize)
        {
            tile.Width = Math.Max(_unitWidth, _pointerStartWidth + dx);
            tile.Height = Math.Max(TileRowHeight, _pointerStartHeight + dy);
        }
        e.Handled = true;
    }

    private async void OnPanelHandlePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerPanel is null || _pointerHandle is null || e.Pointer.PointerId != _pointerId || _project?.WorkspaceLayout is null || !_tileViews.TryGetValue(_pointerPanel.Id, out var tile)) return;
        _pointerHandle.ReleasePointerCapture(e.Pointer);
        var panelId = _pointerPanel.Id;
        if (_pointerOperation == PointerOperation.Move)
        {
            var stepX = _unitWidth + TileGap; var stepY = TileRowHeight + TileGap;
            var x = (int)Math.Round(Math.Max(0, Canvas.GetLeft(tile)) / stepX);
            var y = (int)Math.Round(Math.Max(0, Canvas.GetTop(tile)) / stepY);
            ProjectWorkspaceLayoutService.MovePanel(_project.WorkspaceLayout, panelId, x, y);
        }
        else if (_pointerOperation == PointerOperation.Resize)
        {
            var width = (int)Math.Round((tile.Width + TileGap) / (_unitWidth + TileGap));
            var height = (int)Math.Round((tile.Height + TileGap) / (TileRowHeight + TileGap));
            ProjectWorkspaceLayoutService.ResizePanel(_project.WorkspaceLayout, panelId, width, height);
        }
        ClearPointerOperation();
        await PersistProjectAsync(showSuccess: false); RenderWorkspace(); e.Handled = true;
    }

    private void OnPanelHandlePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerHandle is not null && e.Pointer.PointerId == _pointerId) _pointerHandle.ReleasePointerCapture(e.Pointer);
        ClearPointerOperation(); RenderWorkspace();
    }

    private void ClearPointerOperation()
    {
        _pointerPanel = null; _pointerHandle = null; _pointerId = 0; _pointerOperation = PointerOperation.None;
    }

    private void RememberLayoutForUndo()
    {
        if (_project?.WorkspaceLayout is not null) _layoutUndo = ProjectWorkspaceLayoutService.Clone(_project.WorkspaceLayout);
    }

    private async void OnAddMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived) return;
        var input = await ShowMilestoneDialogAsync(null); if (input is null) return;
        SetBusy(true);
        try
        {
            var result = await _projects.AddMilestoneAsync(_project.Id, input.Title, input.Date, input.Time, input.Notes);
            await ApplyMutationAsync(result);
        }
        finally { SetBusy(false); }
        OpenMilestonesDrawer();
    }

    private async void OnEditMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || sender is not MenuFlyoutItem { Tag: Guid id } || _project.IsArchived) return;
        var milestone = _project.Milestones.FirstOrDefault(item => item.Id == id); if (milestone is null) return;
        var input = await ShowMilestoneDialogAsync(milestone); if (input is null) return;
        SetBusy(true);
        try
        {
            var result = await _projects.UpdateMilestoneAsync(_project.Id, id, input.Title, input.Date, input.Time, input.Notes, reminderEnabled: milestone.ReminderEnabled);
            await ApplyMutationAsync(result);
        }
        finally { SetBusy(false); }
        OpenMilestonesDrawer();
    }

    private async void OnDeleteMilestone(object sender, RoutedEventArgs e)
    {
        if (_project is null || sender is not MenuFlyoutItem { Tag: Guid id } || _project.IsArchived) return;
        if (!await ConfirmAsync(_localization.GetString("Milestone_Delete_Title"), _localization.GetString("Milestone_Delete_Message"))) return;
        SetBusy(true);
        try { await ApplyMutationAsync(await _projects.DeleteMilestoneAsync(_project.Id, id)); }
        finally { SetBusy(false); }
        OpenMilestonesDrawer();
    }

    private async Task<MilestoneEditor?> ShowMilestoneDialogAsync(ProjectMilestone? milestone)
    {
        var title = new TextBox { Header = _localization.GetString("Milestone_Field_Title"), MaxLength = ProjectValidation.MaxMilestoneTitleLength, Text = milestone?.Title ?? string.Empty };
        var date = new CalendarDatePicker { Header = _localization.GetString("Milestone_Field_Date"), Date = milestone is null ? null : new DateTimeOffset(milestone.Date.ToDateTime(TimeOnly.MinValue)) };
        var includeTime = new CheckBox { Content = _localization.GetString("Milestone_Field_IncludeTime"), IsChecked = milestone?.Time is not null };
        var time = new TimePicker { Header = _localization.GetString("Milestone_Field_Time"), SelectedTime = milestone?.Time?.ToTimeSpan(), Visibility = includeTime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed };
        includeTime.Click += (_, _) => time.Visibility = includeTime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var notes = new TextBox { Header = _localization.GetString("Milestone_Field_Notes"), MaxLength = ProjectValidation.MaxMilestoneNotesLength, Text = milestone?.Notes ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
        var error = new TextBlock { Foreground = ResourceBrush("SystemFillColorCriticalBrush"), TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 }; panel.Children.Add(title); panel.Children.Add(date); panel.Children.Add(includeTime); panel.Children.Add(time); panel.Children.Add(notes); panel.Children.Add(error);
        MilestoneEditor? result = null;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString(milestone is null ? "Milestone_Add_Title" : "Milestone_Edit_Title"),
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(240, XamlRoot.Size.Height - 240) },
            PrimaryButtonText = _localization.GetString(milestone is null ? "Action_Create" : "Project_Action_Save"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { args.Cancel = true; error.Text = _localization.GetString("ProjectValidation_MilestoneTitleRequired"); return; }
            if (date.Date is null) { args.Cancel = true; error.Text = _localization.GetString("ProjectValidation_MilestoneDateInvalid"); return; }
            if (includeTime.IsChecked == true && time.SelectedTime is null) { args.Cancel = true; error.Text = _localization.GetString("Milestone_Error_TimeRequired"); return; }
            result = new(title.Text, DateOnly.FromDateTime(date.Date.Value.LocalDateTime), includeTime.IsChecked == true ? TimeOnly.FromTimeSpan(time.SelectedTime!.Value) : null, notes.Text);
        };
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary ? result : null;
    }

    private async void OnSelectFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived) return;
        var previous = _project.WorkFolder;
        var selected = await _folders.SelectAsync(_project.Id, previous);
        if (!selected.Succeeded)
        {
            if (selected.ErrorKey != "ProjectFolder_SelectionCancelled") ShowError(selected.ErrorKey ?? "Project_Error_SaveFailed");
            return;
        }
        _project.WorkFolder = selected.Reference; _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(_project);
        if (result.Succeeded) _folders.Clear(previous); else { _folders.Clear(selected.Reference); _project.WorkFolder = previous; }
        await ApplyMutationAsync(result); OpenFileDrawer();
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkFolder is null) return;
        var result = await _folders.OpenAsync(_project.WorkFolder);
        if (!result.Succeeded) ShowError(result.ErrorKey ?? "ProjectFolder_OpenFailed");
    }

    private async void OnClearFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived) return;
        var previous = _project.WorkFolder; _project.WorkFolder = null; _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(_project);
        if (result.Succeeded) _folders.Clear(previous); else _project.WorkFolder = previous;
        await ApplyMutationAsync(result); OpenFileDrawer();
    }

    private async void OnArchive(object sender, RoutedEventArgs e)
    {
        if (_project is null || _busy) return;
        var wasArchived = _project.IsArchived;
        if (!await ConfirmAsync(_localization.GetString(wasArchived ? "Project_Restore_Title" : "Project_Archive_Title"), _localization.GetString(wasArchived ? "Project_Restore_Message" : "Project_Archive_Message"))) return;
        SetBusy(true);
        try
        {
            var result = await _projects.ArchiveAsync(_project.Id, !wasArchived);
            if (!result.Succeeded) { ShowError("Project_Error_SaveFailed"); return; }
            await RefreshRemindersAsync();
            Frame.Navigate(wasArchived ? typeof(HomePage) : typeof(ProjectArchivePage));
        }
        finally { SetBusy(false); }
    }

    private async void OnDeleteProject(object sender, RoutedEventArgs e)
    {
        if (_project is null || !await ConfirmPermanentDeleteAsync(_project.Name)) return;
        var wasArchived = _project.IsArchived; SetBusy(true);
        try
        {
            var result = await _projects.DeleteAsync(_project.Id, _folders);
            if (!result.Succeeded) { ShowError("Project_Delete_Failed"); return; }
            await RefreshRemindersAsync(); Frame.Navigate(wasArchived ? typeof(ProjectArchivePage) : typeof(HomePage));
        }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(string projectName)
    {
        var warning = new TextBlock { Text = _localization.GetFormattedString("Project_Delete_Warning", projectName), TextWrapping = TextWrapping.Wrap };
        var confirmation = new TextBox { Header = _localization.GetString("Project_Delete_ConfirmName"), PlaceholderText = projectName };
        var external = new TextBlock { Text = _localization.GetString("Project_Delete_ExternalFolderSafe"), TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 }; panel.Children.Add(warning); panel.Children.Add(confirmation); panel.Children.Add(external);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = _localization.GetString("Project_Delete_Title"), Content = panel,
            PrimaryButtonText = _localization.GetString("Project_Action_Delete"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = false
        };
        confirmation.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = ProjectValidation.MatchesDeleteConfirmation(projectName, confirmation.Text);
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private async Task SaveCandidateAsync(ProjectRecord candidate)
    {
        var errors = ProjectValidation.Validate(candidate);
        if (errors.Count > 0) { ShowValidation(errors); return; }
        SetBusy(true);
        try { await ApplyMutationAsync(await _projects.SaveAsync(candidate)); }
        finally { SetBusy(false); }
    }

    private async Task PersistProjectAsync(bool showSuccess)
    {
        if (_project is null || _project.IsArchived) return;
        _project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(_project);
        if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; }
        _project = result.Project;
        if (showSuccess) ShowSuccess("Project_Status_Saved");
    }

    private async Task ApplyMutationAsync(ProjectSaveResult result)
    {
        if (!result.Succeeded) { ShowValidation(result.ValidationErrors); return; }
        _project = result.Project;
        await RefreshLinkedInspirationsAsync();
        ApplyProject();
        await RefreshRemindersAsync();
    }

    private async Task RefreshLinkedInspirationsAsync()
    {
        if (_project is null) { _linkedInspirations = []; return; }
        _linkedInspirations = (await _inspirations.ListAsync()).Where(item => item.LinkedProjectId == _project.Id).OrderByDescending(item => item.UpdatedAt).ToArray();
    }

    private ProjectRecord CloneProject(ProjectRecord project) => JsonSerializer.Deserialize<ProjectRecord>(JsonSerializer.Serialize(project, DataStorageJson.Options), DataStorageJson.Options)!;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (_project is not null) ApplyProject();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = title, Content = message,
            PrimaryButtonText = _localization.GetString("Action_Confirm"), CloseButtonText = _localization.GetString("Action_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        return await AppDialogService.Default.ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private static TextBlock SectionLabel(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], Margin = new Thickness(0, 6, 0, 0) };

    private void ShowDrawer() => DrawerLayer.Visibility = Visibility.Visible;
    private void CloseDrawer() => DrawerLayer.Visibility = Visibility.Collapsed;
    private void OnCloseDrawer(object sender, RoutedEventArgs e) => CloseDrawer();
    private void OnDrawerScrimTapped(object sender, TappedRoutedEventArgs e) => CloseDrawer();
    private void OnBack(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HomePage));

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveMode(); RenderWorkspace();
    }

    private void OnTileCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RenderWorkspace();

    private void ShowValidation(IReadOnlyList<string>? errors)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Message = errors is null ? _localization.GetString("Project_Error_SaveFailed") : string.Join(Environment.NewLine, errors.Select(error => _localization.GetString($"ProjectValidation_{error}")));
        StatusBar.IsOpen = true;
    }

    private void ShowError(string key)
    {
        var message = _localization.GetString(key);
        StatusBar.Severity = InfoBarSeverity.Error; StatusBar.Message = message; StatusBar.IsOpen = true;
        AppNotificationService.Default.Notify(new(UrbanPlanToolbox.Models.Interaction.AppNotificationKind.Error, _localization.GetString("Interaction_ErrorTitle"), message, true));
    }

    private void ShowSuccess(string key)
    {
        var message = _localization.GetString(key);
        StatusBar.Severity = InfoBarSeverity.Success; StatusBar.Message = message; StatusBar.IsOpen = true;
        AppNotificationService.Default.Notify(new(UrbanPlanToolbox.Models.Interaction.AppNotificationKind.Success, _localization.GetString("Interaction_SuccessTitle"), message));
    }

    private static Brush ResourceBrush(string key) => (Brush)Application.Current.Resources[key];

    private async Task RefreshRemindersAsync()
    {
        var result = await MilestoneReminderService.Default.RefreshAsync();
        if (result.Succeeded) return;
        var message = _localization.GetFormattedString("Milestone_Reminder_SchedulingFailed", result.Diagnostic ?? result.FailureType ?? "Unknown");
        StatusBar.Severity = InfoBarSeverity.Warning; StatusBar.Message = message; StatusBar.IsOpen = true;
    }

    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record MilestoneEditor(string Title, DateOnly Date, TimeOnly? Time, string? Notes);
    private sealed record RenderPanel(ProjectWorkspacePanel Panel, int X, int Y, int Width, int Height);
    private enum PointerOperation { None, Move, Resize }
}
