using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Models.Tools;
using UrbanPlanToolbox.Services;
using Windows.Foundation;
using Windows.Storage.Pickers;

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

    private DispatcherQueueTimer? _holdTimer;
    private FrameworkElement? _holdHandle;
    private Guid? _holdPanelId;
    private uint _holdPointerId;
    private Point _holdStart;
    private double _holdStartLeft;
    private double _holdStartTop;
    private double _holdStartWidth;
    private double _holdStartHeight;

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
        WorkspaceSubtitleText.Text = W("长按任意面板进入调整状态；拖动移动，右下角拉伸尺寸，点击空白处完成，右键编辑内容", "Press and hold any panel to enter edit mode; drag to move, resize from the lower-right corner, click empty space to finish, and right-click to edit", "任意のパネルを長押しして編集状態に入り、ドラッグで移動、右下でサイズ変更、空白をクリックして完了、右クリックで内容を編集できます");
        AddPanelButton.Content = W("＋ 新建面板", "+ Add panel", "＋ パネルを追加");
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
            WorkspaceSubtitleText.Text = W("长按任意面板进入调整状态；拖动移动，右下角拉伸尺寸，点击空白处完成，右键编辑研究内容", "Press and hold any panel to enter edit mode; drag to move, resize from the lower-right corner, click empty space to finish, and right-click to edit research content", "任意のパネルを長押しして編集状態に入り、ドラッグで移動、右下でサイズ変更、空白をクリックして完了、右クリックで研究内容を編集できます");
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
            WorkspaceSubtitleText.Text = W("长按任意面板进入调整状态；拖动移动，右下角拉伸尺寸，点击空白处完成，右键编辑内容", "Press and hold any panel to enter edit mode; drag to move, resize from the lower-right corner, click empty space to finish, and right-click to edit", "任意のパネルを長押しして編集状態に入り、ドラッグで移動、右下でサイズ変更、空白をクリックして完了、右クリックで内容を編集できます");
            OverviewDescriptionText.Text = _project.Description
                ?? W("尚未填写项目说明。", "No project description yet.", "プロジェクト説明はまだありません。");
            OverviewLabel1.Text = W("项目类型", "Project type", "プロジェクト種別");
            OverviewValue1.Text = ProjectPresentation.GetTypeName(_project, _localization);
            OverviewLabel2.Text = W("当前阶段", "Current stage", "現在の段階");
            OverviewValue2.Text = currentStage;
            OverviewLabel3.Text = W("时间节点", "Milestones", "マイルストーン");
            OverviewValue3.Text = _project.Milestones.Count.ToString(CultureInfo.CurrentCulture);
            OverviewLabel4.Text = W("工作文件夹", "Work folder", "作業フォルダー");
            var folders = GetLinkedFolders();
            OverviewValue4.Text = folders.Count switch
            {
                0 => W("未设置", "Not set", "未設定"),
                1 => folders[0].DisplayName,
                _ => W($"{folders.Count} 个文件夹", $"{folders.Count} folders", $"{folders.Count} 個のフォルダー")
            };
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
        if (!canEditLayout && _layoutEditing)
        {
            _layoutEditing = false;
            _selectedPanelId = null;
            ClearPointerOperation();
        }
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

        TileCanvas.Height = Math.Max(0, maxBottom);
        WorkspaceSurface.MinHeight = TileCanvas.Height;
        UndoLayoutButton.Visibility = _layoutUndo is null || !_layoutEditing ? Visibility.Collapsed : Visibility.Visible;
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
        border.ContextFlyout = CreatePanelMenu(panel);
        border.PointerPressed += OnTilePointerPressed;
        border.PointerMoved += OnTilePointerMoved;
        border.PointerReleased += OnTilePointerReleased;
        border.PointerCanceled += OnTilePointerCanceled;
        ToolTipService.SetToolTip(border, W("长按进入调整状态；右键编辑", "Press and hold to arrange; right-click to edit", "長押しで配置を調整、右クリックで編集"));

        var root = new Grid { RowSpacing = 8 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
  Text = string.IsNullOrWhiteSpace(panel.Title) ? PanelName(panel.Kind) : panel.Title,
  Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
  TextTrimming = TextTrimming.CharacterEllipsis,
  VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(title);

        if (_layoutEditing)
        {
  var size = new TextBlock
  {
      Text = $"{panel.Width}×{panel.Height}", FontSize = 11, Opacity = .55,
      VerticalAlignment = VerticalAlignment.Center
  };
  Grid.SetColumn(size, 1);
  header.Children.Add(size);
        }
        root.Children.Add(header);

        var body = BuildPanelBody(panel);
        if (body is FrameworkElement bodyElement)
  Grid.SetRow(bodyElement, 1);
        root.Children.Add(body);

        if (_layoutEditing && selected && _project is { IsArchived: false } && _columnCount == 12)
        {
  var resize = new Border
  {
      Width = 24, Height = 24, Tag = panel.Id,
      HorizontalAlignment = HorizontalAlignment.Right,
      VerticalAlignment = VerticalAlignment.Bottom,
      Margin = new Thickness(0, 0, -4, -4),
      Background = ResourceBrush("AccentFillColorSecondaryBrush"),
      BorderBrush = ResourceBrush("AccentFillColorDefaultBrush"),
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(12),
      Child = new TextBlock
      {
          Text = "↘", FontSize = 12,
          HorizontalAlignment = HorizontalAlignment.Center,
          VerticalAlignment = VerticalAlignment.Center
      }
  };
  resize.PointerPressed += OnResizeHandlePointerPressed;
  resize.PointerMoved += OnPanelHandlePointerMoved;
  resize.PointerReleased += OnPanelHandlePointerReleased;
  resize.PointerCanceled += OnPanelHandlePointerCanceled;
  Grid.SetRow(resize, 1);
  root.Children.Add(resize);
  ToolTipService.SetToolTip(resize, W("拖动调整尺寸", "Drag to resize", "ドラッグしてサイズ変更"));
        }

        border.Child = root;
        return border;
    }

    private FlyoutBase CreatePanelMenu(ProjectWorkspacePanel panel)
    {
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem
        {
  Text = W("编辑内容", "Edit content", "内容を編集"),
  Tag = panel.Id,
  IsEnabled = _project is { IsArchived: false }
        };
        edit.Click += OnEditPanelContent;
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
        ProjectWorkspacePanelKinds.Chart => BuildChart(panel),
        ProjectWorkspacePanelKinds.Literature => BuildLiterature(panel),
        ProjectWorkspacePanelKinds.TextNote => BuildTextNote(panel),
        _ => BuildCustom(panel)
    };

    private UIElement BuildImageShowcase(ProjectWorkspacePanel panel)
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
        var cardWidth = Math.Clamp(panel.Width * Math.Max(80, _unitWidth) * .84, 260, 620);
        var cardHeight = Math.Clamp(panel.Height * TileRowHeight - 64, 130, 360);

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
          Glyph = "\uEB9F", FontSize = 30, Opacity = .4,
          HorizontalAlignment = HorizontalAlignment.Center,
          VerticalAlignment = VerticalAlignment.Center
      });
  }

  var caption = new Border
  {
      VerticalAlignment = VerticalAlignment.Bottom,
      Background = new SolidColorBrush(Windows.UI.Color.FromArgb(170, 20, 20, 20)),
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
  card.Tapped += async (_, e) =>
  {
      e.Handled = true;
      await ShowImageViewerAsync(items, itemIndex);
  };
  ToolTipService.SetToolTip(card, W("点击查看大图", "Click to view full image", "クリックして拡大表示"));
  strip.Children.Add(card);
        }

        scroll.Content = strip;
        scroll.PointerWheelChanged += (_, e) =>
        {
  var delta = e.GetCurrentPoint(scroll).Properties.MouseWheelDelta;
  if (delta == 0) return;
  var step = cardWidth + 12;
  var target = delta > 0 ? Math.Max(0, scroll.HorizontalOffset - step) : scroll.HorizontalOffset + step;
  scroll.ChangeView(target, null, null, true);
  e.Handled = true;
        };
        host.Children.Add(scroll);

        if (items.Count > 1)
        {
  var previous = CreateCarouselButton("‹", HorizontalAlignment.Left);
  previous.Click += (_, _) => scroll.ChangeView(Math.Max(0, scroll.HorizontalOffset - (cardWidth + 12)), null, null, true);
  host.Children.Add(previous);

  var next = CreateCarouselButton("›", HorizontalAlignment.Right);
  next.Click += (_, _) => scroll.ChangeView(scroll.HorizontalOffset + cardWidth + 12, null, null, true);
  host.Children.Add(next);
        }
        return host;
    }

    private Button CreateCarouselButton(string content, HorizontalAlignment alignment) => new()
    {
        Content = content,
        Width = 44,
        Height = 44,
        MinWidth = 44,
        FontSize = 24,
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = alignment == HorizontalAlignment.Left ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 8, 0),
        Opacity = .94
    };

    private async Task ShowImageViewerAsync(IReadOnlyList<ShowcaseItem> items, int initialIndex)
    {
        if (items.Count == 0) return;
        var index = Math.Clamp(initialIndex, 0, items.Count - 1);
        var image = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var placeholder = new FontIcon { Glyph = "\uEB9F", FontSize = 44, Opacity = .45, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var media = new Grid
        {
  MinHeight = Math.Min(680, Math.Max(360, XamlRoot.Size.Height - 300)),
  Background = ResourceBrush("CardBackgroundFillColorDefaultBrush")
        };
        media.Children.Add(image);
        media.Children.Add(placeholder);

        var title = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var counter = new TextBlock { Opacity = .58, VerticalAlignment = VerticalAlignment.Center };
        var previous = new Button { Content = "‹", Width = 42, Height = 42, MinWidth = 42, FontSize = 22 };
        var next = new Button { Content = "›", Width = 42, Height = 42, MinWidth = 42, FontSize = 22 };

        void Refresh()
        {
  var item = items[index];
  title.Text = item.Title;
  counter.Text = $"{index + 1} / {items.Count}";
  var bitmap = CreateBitmapImage(item.Source);
  image.Source = bitmap;
  image.Visibility = bitmap is null ? Visibility.Collapsed : Visibility.Visible;
  placeholder.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
  previous.IsEnabled = index > 0;
  next.IsEnabled = index < items.Count - 1;
        }
        previous.Click += (_, _) => { if (index > 0) { index--; Refresh(); } };
        next.Click += (_, _) => { if (index < items.Count - 1) { index++; Refresh(); } };
        Refresh();

        var toolbar = new Grid { ColumnSpacing = 10 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(counter);
        Grid.SetColumn(title, 1); toolbar.Children.Add(title);
        Grid.SetColumn(previous, 2); toolbar.Children.Add(previous);
        Grid.SetColumn(next, 3); toolbar.Children.Add(next);

        var root = new Grid { RowSpacing = 12, Width = Math.Min(1100, Math.Max(560, XamlRoot.Size.Width - 180)) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(toolbar);
        Grid.SetRow(media, 1); root.Children.Add(media);

        var dialog = new ContentDialog
        {
  XamlRoot = XamlRoot,
  Title = W("图片查看器", "Image viewer", "画像ビューアー"),
  Content = root,
  CloseButtonText = W("关闭", "Close", "閉じる"),
  MaxWidth = Math.Min(1180, Math.Max(620, XamlRoot.Size.Width - 100))
        };
        await AppDialogService.Default.ShowAsync(dialog);
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
        var count = panel.Height <= 1 ? 2 : 5;
        foreach (var item in _linkedInspirations.Take(count))
        {
  var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
  row.Children.Add(new FontIcon { Glyph = "\uE8F1", FontSize = 12, Opacity = .5, VerticalAlignment = VerticalAlignment.Center });
  row.Children.Add(new TextBlock { Text = item.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
  stack.Children.Add(row);
        }
        if (_linkedInspirations.Count == 0)
  stack.Children.Add(new TextBlock { Text = W("暂无关联灵感", "No linked inspirations", "関連付けられたアイデアはありません"), Opacity = .62 });
        return stack;
    }

    private UIElement BuildFiles(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 8 };
        if (panel.Kind == ProjectWorkspacePanelKinds.DataAndScripts)
        {
  var entries = GetSettingLines(panel, "content");
  if (entries.Count == 0)
  {
      stack.Children.Add(new TextBlock
      {
          Text = W("右键添加数据集、脚本或分析资源。", "Right-click to add datasets, scripts or analysis resources.", "右クリックしてデータセット、スクリプト、分析資料を追加できます。"),
          Opacity = .62,
          TextWrapping = TextWrapping.Wrap
      });
      return stack;
  }
  foreach (var line in entries.Take(panel.Height <= 1 ? 2 : 6))
  {
      var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
      stack.Children.Add(FileRow("\uE943", parts[0], parts.Length > 1 ? parts[1] : string.Empty));
  }
  return stack;
        }

        var folders = GetLinkedFolders();
        if (folders.Count == 0)
        {
  stack.Children.Add(new TextBlock
  {
      Text = W("右键此面板添加项目文件夹。", "Right-click this panel to add project folders.", "このパネルを右クリックしてプロジェクトフォルダーを追加できます。"),
      Opacity = .62,
      TextWrapping = TextWrapping.Wrap
  });
  return stack;
        }

        foreach (var folder in folders.Take(panel.Height <= 1 ? 2 : 6))
  stack.Children.Add(FileRow("\uE8B7", folder.DisplayName, folder.DisplayPath));
        if (folders.Count > (panel.Height <= 1 ? 2 : 6))
  stack.Children.Add(new TextBlock { Text = W($"还有 {folders.Count - (panel.Height <= 1 ? 2 : 6)} 个文件夹", $"{folders.Count - (panel.Height <= 1 ? 2 : 6)} more folders", $"ほか {folders.Count - (panel.Height <= 1 ? 2 : 6)} 個"), Opacity = .55 });
        return stack;
    }

    private IReadOnlyList<ProjectFolderReference> GetLinkedFolders()
    {
        if (_project is null) return [];
        var result = new List<ProjectFolderReference>();
        void Add(ProjectFolderReference? folder)
        {
  if (folder is null) return;
  if (result.Any(existing => SameFolder(existing, folder))) return;
  result.Add(folder);
        }
        Add(_project.WorkFolder);
        foreach (var folder in _project.AdditionalFolders) Add(folder);
        return result;
    }

    private static bool SameFolder(ProjectFolderReference left, ProjectFolderReference right)
    {
        if (!string.IsNullOrWhiteSpace(left.AccessToken) && !string.IsNullOrWhiteSpace(right.AccessToken) &&
  string.Equals(left.AccessToken, right.AccessToken, StringComparison.Ordinal)) return true;
        return string.Equals(left.DisplayPath, right.DisplayPath, StringComparison.OrdinalIgnoreCase);
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
        var stack = new StackPanel { Spacing = 7 };
        var source = _project?.PlanningRequirements;
        var items = string.IsNullOrWhiteSpace(source)
  ? Array.Empty<string>()
  : source.Split(['\r', '\n', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(panel.Height <= 1 ? 3 : 6).ToArray();
        if (items.Length == 0)
        {
  stack.Children.Add(new TextBlock { Text = W("右键添加重点策略。", "Right-click to add key strategies.", "右クリックして重点戦略を追加できます。"), Opacity = .62 });
  return stack;
        }
        foreach (var item in items)
        {
  var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
  row.Children.Add(new TextBlock { Text = "•", Opacity = .55 });
  row.Children.Add(new TextBlock { Text = item, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap });
  stack.Children.Add(row);
        }
        return stack;
    }

    private UIElement BuildResearchFramework(ProjectWorkspacePanel panel)
    {
        var details = _project?.ResearchDetails;
        var output = panel.Settings.TryGetValue("output", out var configuredOutput) && !string.IsNullOrWhiteSpace(configuredOutput)
  ? configuredOutput
  : W("图表 · 结论 · 建议", "Figures · findings · guidance", "図表・結論・提案");
        var grid = new Grid { ColumnSpacing = 8 };
        for (var i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var phases = new[]
        {
  (W("1 研究问题", "1 Question", "1 研究課題"), TrimForMetric(details?.ResearchSubject)),
  (W("2 数据与对象", "2 Data & subject", "2 データ・対象"), TrimForMetric(details?.ResearchField)),
  (W("3 分析方法", "3 Methods", "3 分析方法"), TrimForMetric(details?.ResearchMethods)),
  (W("4 预期产出", "4 Output", "4 成果"), TrimForMetric(output))
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

    private UIElement BuildChart(ProjectWorkspacePanel panel)
    {
        var data = new List<(string Label, double Value)>();
        foreach (var line in GetSettingLines(panel, "data"))
        {
  var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
  if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
      data.Add((parts[0], value));
        }
        if (data.Count == 0)
  return new TextBlock { Text = W("右键添加图表数据。格式：指标,数值", "Right-click to add chart data. Format: label,value", "右クリックしてチャートデータを追加。形式：項目,値"), Opacity = .62, TextWrapping = TextWrapping.Wrap };

        var max = Math.Max(1, data.Max(item => Math.Abs(item.Value)));
        var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Stretch };
        foreach (var _ in data) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < data.Count; i++)
        {
  var column = new Grid { RowSpacing = 5 };
  column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
  column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
  var bar = new Border
  {
      Height = 24 + 90 * Math.Abs(data[i].Value) / max,
      Background = ResourceBrush("AccentFillColorSecondaryBrush"),
      CornerRadius = new CornerRadius(3),
      VerticalAlignment = VerticalAlignment.Bottom,
      Margin = new Thickness(4, 0, 4, 0)
  };
  column.Children.Add(bar);
  var label = new TextBlock { Text = data[i].Label, FontSize = 10, Opacity = .6, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
  Grid.SetRow(label, 1); column.Children.Add(label);
  Grid.SetColumn(column, i); grid.Children.Add(column);
        }
        return grid;
    }

    private UIElement BuildLiterature(ProjectWorkspacePanel panel)
    {
        var stack = new StackPanel { Spacing = 8 };
        var entries = GetSettingLines(panel, "content");
        if (entries.Count == 0)
        {
  stack.Children.Add(new TextBlock { Text = W("右键添加文献资料。", "Right-click to add literature entries.", "右クリックして文献資料を追加できます。"), Opacity = .62 });
  return stack;
        }
        foreach (var entry in entries.Take(panel.Height <= 1 ? 3 : 7))
        {
  var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
  row.Children.Add(new FontIcon { Glyph = "\uE8A5", FontSize = 12, Opacity = .5, VerticalAlignment = VerticalAlignment.Center });
  row.Children.Add(new TextBlock { Text = entry, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap });
  stack.Children.Add(row);
        }
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

    private IReadOnlyList<string> GetSettingLines(ProjectWorkspacePanel panel, string key)
    {
        if (!panel.Settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private IReadOnlyList<ShowcaseItem> GetShowcaseItems(ProjectWorkspacePanel panel)
    {
        var result = new List<ShowcaseItem>();
        if (panel.Settings.TryGetValue("images", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
  foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
  {
      var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
      var title = string.IsNullOrWhiteSpace(parts[0]) ? W("项目图片", "Project image", "プロジェクト画像") : parts[0];
      var source = parts.Length > 1 ? parts[1] : string.Empty;
      result.Add(new ShowcaseItem(title, source));
  }
        }
        if (result.Count == 0)
  result.AddRange(_linkedInspirations.Select(item => new ShowcaseItem(item.Title, string.Empty)));
        return result;
    }

    private static BitmapImage? CreateBitmapImage(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
  Uri uri;
  if (Path.IsPathFullyQualified(source))
      uri = new Uri(Path.GetFullPath(source), UriKind.Absolute);
  else if (!Uri.TryCreate(source, UriKind.Absolute, out uri!))
      return null;
  return new BitmapImage(uri);
        }
        catch
        {
  return null;
        }
    }

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
        DrawerSubtitle.Text = W("选择一种面板；添加后长按面板即可移动和调整尺寸", "Choose a panel. Press and hold it afterward to move or resize it.", "パネルを選択してください。追加後は長押しして移動・サイズ変更できます。");
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

    private void OnEditPanelContent(object sender, RoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || sender is not MenuFlyoutItem { Tag: Guid id }) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is not null) OpenPanelContentEditor(panel);
    }

    private void OpenPanelContentEditor(ProjectWorkspacePanel panel)
    {
        if (_project is null || _project.IsArchived) return;
        switch (panel.Kind)
        {
  case ProjectWorkspacePanelKinds.ImageShowcase:
      OpenImageShowcaseEditor(panel);
      break;
  case ProjectWorkspacePanelKinds.Milestones:
  case ProjectWorkspacePanelKinds.ResearchProgress:
      OpenMilestonesDrawer();
      break;
  case ProjectWorkspacePanelKinds.Description:
      OpenProjectTextEditor(
          W("编辑项目说明", "Edit project notes", "プロジェクト説明を編集"),
          W("项目说明会直接显示在此面板中", "The project description is shown directly in this panel", "プロジェクト説明はこのパネルに直接表示されます"),
          _localization.GetString("Project_Field_Description"),
          _project.Description ?? string.Empty,
          ProjectValidation.MaxDescriptionLength,
          async value =>
          {
              var candidate = CloneProject(_project);
              candidate.Description = value;
              await SaveCandidateAsync(candidate);
          });
      break;
  case ProjectWorkspacePanelKinds.Inspirations:
      App.OpenInspirationManagement(_project.Kind == ProjectKindCodes.Research ? InspirationCategory.Research : InspirationCategory.Design);
      break;
  case ProjectWorkspacePanelKinds.Files:
      OpenFileDrawer();
      break;
  case ProjectWorkspacePanelKinds.KeyStrategies:
      OpenProjectTextEditor(
          W("编辑重点策略", "Edit key strategies", "重点戦略を編集"),
          W("每行或使用分号分隔一条策略", "Use one strategy per line or separate with semicolons", "1行ごと、またはセミコロンで戦略を区切ります"),
          W("重点策略", "Key strategies", "重点戦略"),
          _project.PlanningRequirements ?? string.Empty,
          ProjectValidation.MaxPlanningRequirementsLength,
          async value =>
          {
              var candidate = CloneProject(_project);
              candidate.PlanningRequirements = value;
              await SaveCandidateAsync(candidate);
          });
      break;
  case ProjectWorkspacePanelKinds.ResearchFramework:
      OpenResearchFrameworkEditor(panel);
      break;
  case ProjectWorkspacePanelKinds.ResearchQuestion:
      OpenProjectTextEditor(
          W("编辑核心研究问题", "Edit core research question", "中心研究課題を編集"),
          W("该内容同时作为研究概览中的研究对象/问题摘要", "This also feeds the research overview summary", "この内容は研究概要にも反映されます"),
          _localization.GetString("ResearchProject_Field_Subject"),
          _project.ResearchDetails?.ResearchSubject ?? string.Empty,
          ProjectValidation.MaxResearchSubjectLength,
          async value =>
          {
              var candidate = CloneProject(_project);
              candidate.ResearchDetails ??= new ResearchProjectDetails();
              candidate.ResearchDetails.ResearchSubject = value;
              await SaveCandidateAsync(candidate);
          });
      break;
  case ProjectWorkspacePanelKinds.Chart:
      OpenPanelTextEditor(panel,
          W("编辑图表数据", "Edit chart data", "チャートデータを編集"),
          W("每行使用“指标,数值”，例如：TOD,72", "Use label,value per line, for example: TOD,72", "各行を「項目,値」で入力します。例：TOD,72"),
          W("图表数据", "Chart data", "チャートデータ"), "data");
      break;
  case ProjectWorkspacePanelKinds.Literature:
      OpenPanelTextEditor(panel,
          W("编辑文献资料", "Edit literature", "文献資料を編集"),
          W("每行一条文献、链接或阅读备注", "Use one reference, link or reading note per line", "1行につき1件の文献・リンク・読書メモ"),
          W("文献条目", "Literature entries", "文献項目"), "content");
      break;
  case ProjectWorkspacePanelKinds.DataAndScripts:
      OpenPanelTextEditor(panel,
          W("编辑数据与脚本", "Edit data & scripts", "データ・スクリプトを編集"),
          W("每行可写“名称|路径或说明”", "Use name|path or description on each line", "各行を「名前|パスまたは説明」で入力します"),
          W("数据与脚本", "Data & scripts", "データ・スクリプト"), "content");
      break;
  case ProjectWorkspacePanelKinds.TextNote:
  case ProjectWorkspacePanelKinds.Custom:
      OpenPanelTextEditor(panel,
          W("编辑面板内容", "Edit panel content", "パネル内容を編集"),
          W("内容会保存在当前项目的工作台布局中", "Content is stored in this project's workspace layout", "内容は現在のプロジェクトのワークスペースに保存されます"),
          W("内容", "Content", "内容"), "content");
      break;
        }
    }

    private void OpenProjectTextEditor(string title, string subtitle, string header, string value, int maxLength, Func<string, Task> saveAsync)
    {
        DrawerTitle.Text = title;
        DrawerSubtitle.Text = subtitle;
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();
        var editor = new TextBox
        {
  Header = header,
  Text = value,
  MaxLength = maxLength,
  AcceptsReturn = true,
  TextWrapping = TextWrapping.Wrap,
  MinHeight = 220
        };
        DrawerContent.Children.Add(editor);
        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer();
        DrawerFooter.Children.Add(cancel);
        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
  await saveAsync(editor.Text);
  CloseDrawer();
  ApplyProject();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private void OpenPanelTextEditor(ProjectWorkspacePanel panel, string title, string subtitle, string header, string settingKey)
    {
        DrawerTitle.Text = title;
        DrawerSubtitle.Text = subtitle;
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();
        var editor = new TextBox
        {
  Header = header,
  Text = panel.Settings.TryGetValue(settingKey, out var current) ? current : string.Empty,
  AcceptsReturn = true,
  TextWrapping = TextWrapping.Wrap,
  MinHeight = 240
        };
        DrawerContent.Children.Add(editor);
        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer(); DrawerFooter.Children.Add(cancel);
        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
  panel.Settings[settingKey] = editor.Text;
  await PersistProjectAsync(showSuccess: false);
  CloseDrawer();
  RenderWorkspace();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private void OpenImageShowcaseEditor(ProjectWorkspacePanel panel)
    {
        DrawerTitle.Text = W("编辑图片展示架", "Edit image showcase", "画像ショーケースを編集");
        DrawerSubtitle.Text = W("横向轮播显示；每行使用“标题|图片路径或 URI”", "Horizontal carousel; use title|image path or URI on each line", "横スクロール表示。各行を「タイトル|画像パスまたはURI」で入力します");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        var editor = new TextBox
        {
  Header = W("展示图片", "Showcase images", "表示画像"),
  AcceptsReturn = true,
  TextWrapping = TextWrapping.NoWrap,
  MinHeight = 260,
  Text = string.Join(Environment.NewLine, GetShowcaseItems(panel).Select(item => string.IsNullOrWhiteSpace(item.Source) ? item.Title : $"{item.Title}|{item.Source}"))
        };
        DrawerContent.Children.Add(editor);

        var pick = new Button { Content = W("＋ 选择本地图片", "+ Choose local images", "＋ ローカル画像を選択") };
        pick.Click += async (_, _) =>
        {
  var files = await PickImageFilesAsync();
  if (files.Count == 0) return;
  var lines = files.Select(path => $"{Path.GetFileNameWithoutExtension(path)}|{path}");
  var prefix = editor.Text.Trim();
  editor.Text = string.IsNullOrWhiteSpace(prefix) ? string.Join(Environment.NewLine, lines) : prefix + Environment.NewLine + string.Join(Environment.NewLine, lines);
        };
        DrawerContent.Children.Add(pick);

        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer(); DrawerFooter.Children.Add(cancel);
        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
  panel.Settings["images"] = editor.Text;
  await PersistProjectAsync(showSuccess: false);
  CloseDrawer();
  RenderWorkspace();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private async Task<IReadOnlyList<string>> PickImageFilesAsync()
    {
        if (App.MainWindow is null) return [];
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");
        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    private void OpenResearchFrameworkEditor(ProjectWorkspacePanel panel)
    {
        if (_project is null) return;
        DrawerTitle.Text = W("编辑研究框架", "Edit research framework", "研究フレームを編集");
        DrawerSubtitle.Text = W("研究问题、数据与对象、分析方法、预期产出", "Question, data & subject, methods and expected output", "研究課題、データ・対象、分析方法、想定成果");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();
        var subject = new TextBox { Header = _localization.GetString("ResearchProject_Field_Subject"), Text = _project.ResearchDetails?.ResearchSubject ?? string.Empty, MaxLength = ProjectValidation.MaxResearchSubjectLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
        var field = new TextBox { Header = _localization.GetString("ResearchProject_Field_Field"), Text = _project.ResearchDetails?.ResearchField ?? string.Empty, MaxLength = ProjectValidation.MaxResearchFieldLength };
        var methods = new TextBox { Header = _localization.GetString("ResearchProject_Field_Methods"), Text = _project.ResearchDetails?.ResearchMethods ?? string.Empty, MaxLength = ProjectValidation.MaxResearchMethodsLength, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100 };
        var output = new TextBox { Header = W("预期产出", "Expected output", "想定成果"), Text = panel.Settings.TryGetValue("output", out var configured) ? configured : string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90 };
        DrawerContent.Children.Add(subject); DrawerContent.Children.Add(field); DrawerContent.Children.Add(methods); DrawerContent.Children.Add(output);
        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer(); DrawerFooter.Children.Add(cancel);
        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
  var candidate = CloneProject(_project);
  candidate.ResearchDetails ??= new ResearchProjectDetails();
  candidate.ResearchDetails.ResearchSubject = subject.Text;
  candidate.ResearchDetails.ResearchField = field.Text;
  candidate.ResearchDetails.ResearchMethods = methods.Text;
  var candidatePanel = candidate.WorkspaceLayout?.Panels.FirstOrDefault(item => item.Id == panel.Id);
  if (candidatePanel is not null) candidatePanel.Settings["output"] = output.Text;
  await SaveCandidateAsync(candidate);
  CloseDrawer();
  ApplyProject();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
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
        DrawerSubtitle.Text = W("右键节点编辑或删除；也可以新增节点", "Right-click a milestone to edit or delete it, or add a new one", "マイルストーンを右クリックして編集・削除、または新規追加できます");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        if (!_project.IsArchived)
        {
  var add = new Button { Content = W("＋ 新增节点", "+ Add milestone", "＋ マイルストーンを追加"), HorizontalAlignment = HorizontalAlignment.Stretch };
  add.Click += OnAddMilestone; DrawerContent.Children.Add(add);
        }
        foreach (var milestone in _project.Milestones.OrderBy(item => item.Date).ThenBy(item => item.DisplayOrder))
        {
  var card = new Border { Style = (Style)Application.Current.Resources["SettingsSectionCardStyle"], Padding = new Thickness(12), Tag = milestone.Id };
  var text = new StackPanel { Spacing = 2 };
  text.Children.Add(new TextBlock { Text = milestone.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
  text.Children.Add(new TextBlock { Text = milestone.Date.ToString("d", CultureInfo.CurrentCulture), Opacity = .58 });
  if (!string.IsNullOrWhiteSpace(milestone.Notes))
      text.Children.Add(new TextBlock { Text = milestone.Notes, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.Wrap, Opacity = .68 });
  card.Child = text;
  if (!_project.IsArchived)
  {
      var flyout = new MenuFlyout();
      var edit = new MenuFlyoutItem { Text = _localization.GetString("Milestone_Action_Edit"), Tag = milestone.Id }; edit.Click += OnEditMilestone;
      var delete = new MenuFlyoutItem { Text = _localization.GetString("Milestone_Action_Delete"), Tag = milestone.Id }; delete.Click += OnDeleteMilestone;
      flyout.Items.Add(edit); flyout.Items.Add(delete);
      card.ContextFlyout = flyout;
      ToolTipService.SetToolTip(card, W("右键编辑节点", "Right-click to edit", "右クリックで編集"));
  }
  DrawerContent.Children.Add(card);
        }
        if (_project.Milestones.Count == 0)
  DrawerContent.Children.Add(new TextBlock { Text = _localization.GetString("Milestone_Empty"), Opacity = .62 });
        ShowDrawer();
    }

    private void OpenFileDrawer()
    {
        if (_project is null) return;
        var folders = GetLinkedFolders();
        DrawerTitle.Text = W("文件入口", "Files", "ファイル");
        DrawerSubtitle.Text = folders.Count == 0
  ? W("可链接多个项目文件夹", "Link multiple project folders", "複数のプロジェクトフォルダーをリンクできます")
  : W($"已链接 {folders.Count} 个文件夹", $"{folders.Count} linked folders", $"{folders.Count} 個のフォルダーをリンク済み");
        DrawerContent.Children.Clear(); DrawerFooter.Children.Clear();

        if (!_project.IsArchived)
        {
  var add = new Button
  {
      Content = W("＋ 添加文件夹", "+ Add folder", "＋ フォルダーを追加"),
      HorizontalAlignment = HorizontalAlignment.Stretch
  };
  add.Click += OnAddFolder;
  DrawerContent.Children.Add(add);
        }

        if (folders.Count == 0)
        {
  DrawerContent.Children.Add(new TextBlock
  {
      Text = W("尚未链接文件夹。添加后会直接显示在文件入口面板中。", "No folders are linked yet. Added folders appear directly in the Files panel.", "まだフォルダーがリンクされていません。追加するとファイルパネルに直接表示されます。"),
      TextWrapping = TextWrapping.Wrap,
      Opacity = .62
  });
        }

        foreach (var folder in folders)
        {
  var card = new Border { Style = (Style)Application.Current.Resources["SettingsSectionCardStyle"], Padding = new Thickness(12) };
  var row = new Grid { ColumnSpacing = 8 };
  row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
  row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
  if (!_project.IsArchived) row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

  var text = new StackPanel { Spacing = 2 };
  text.Children.Add(new TextBlock { Text = folder.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
  text.Children.Add(new TextBlock { Text = folder.DisplayPath, FontSize = 11, Opacity = .58, TextTrimming = TextTrimming.CharacterEllipsis });
  row.Children.Add(text);

  var open = new Button { Content = W("打开", "Open", "開く"), Tag = folder, IsEnabled = !folder.RequiresReselection };
  open.Click += OnOpenLinkedFolder;
  Grid.SetColumn(open, 1); row.Children.Add(open);

  if (!_project.IsArchived)
  {
      var remove = new Button { Content = W("移除", "Remove", "削除"), Tag = folder };
      remove.Click += OnRemoveLinkedFolder;
      Grid.SetColumn(remove, 2); row.Children.Add(remove);
  }
  card.Child = row;
  DrawerContent.Children.Add(card);
        }
        ShowDrawer();
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
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
  OpenFileDrawer();
  return;
        }

        var candidate = CloneProject(_project);
        if (candidate.WorkFolder is null) candidate.WorkFolder = selected.Reference;
        else candidate.AdditionalFolders.Add(selected.Reference);
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var result = await _projects.SaveAsync(candidate);
        if (!result.Succeeded) _folders.Clear(selected.Reference);
        await ApplyMutationAsync(result);
        OpenFileDrawer();
    }

    private async void OnOpenLinkedFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProjectFolderReference folder }) return;
        var result = await _folders.OpenAsync(folder);
        if (!result.Succeeded) ShowError(result.ErrorKey ?? "ProjectFolder_OpenFailed");
    }

    private async void OnRemoveLinkedFolder(object sender, RoutedEventArgs e)
    {
        if (_project is null || _project.IsArchived || sender is not Button { Tag: ProjectFolderReference folder }) return;
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
        OpenFileDrawer();
    }

    private void OnTilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_columnCount != 12 || _project is not { IsArchived: false } || _busy ||
  sender is not FrameworkElement { Tag: Guid id } handle || !_tileViews.TryGetValue(id, out var tile)) return;
        var point = e.GetCurrentPoint(handle);
        if (!point.Properties.IsLeftButtonPressed) return;

        CancelHoldCandidate();
        if (_layoutEditing)
        {
  _selectedPanelId = id;
  BeginPointerOperation(handle, e, PointerOperation.Move);
  tile.BorderBrush = ResourceBrush("AccentFillColorDefaultBrush");
  tile.BorderThickness = new Thickness(2);
  return;
        }

        _holdHandle = handle;
        _holdPanelId = id;
        _holdPointerId = e.Pointer.PointerId;
        _holdStart = e.GetCurrentPoint(TileCanvas).Position;
        _holdStartLeft = Canvas.GetLeft(tile);
        _holdStartTop = Canvas.GetTop(tile);
        _holdStartWidth = tile.Width;
        _holdStartHeight = tile.Height;
        handle.CapturePointer(e.Pointer);
        _holdTimer = DispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(360);
        _holdTimer.IsRepeating = false;
        _holdTimer.Tick += (_, _) => ActivateHoldMove();
        _holdTimer.Start();
        e.Handled = true;
    }

    private void OnTilePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerOperation == PointerOperation.Move && _pointerHandle == sender)
        {
  OnPanelHandlePointerMoved(sender, e);
  return;
        }
        if (_holdHandle != sender || e.Pointer.PointerId != _holdPointerId) return;
        var point = e.GetCurrentPoint(TileCanvas).Position;
        var dx = point.X - _holdStart.X;
        var dy = point.Y - _holdStart.Y;
        if ((dx * dx) + (dy * dy) <= 576) return;
        var handle = _holdHandle;
        CancelHoldCandidate();
        handle?.ReleasePointerCapture(e.Pointer);
    }

    private void OnTilePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerOperation == PointerOperation.Move && _pointerHandle == sender)
        {
  OnPanelHandlePointerReleased(sender, e);
  return;
        }
        var handle = _holdHandle;
        CancelHoldCandidate();
        handle?.ReleasePointerCapture(e.Pointer);
    }

    private void OnTilePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerOperation == PointerOperation.Move && _pointerHandle == sender)
        {
  OnPanelHandlePointerCanceled(sender, e);
  return;
        }
        var handle = _holdHandle;
        CancelHoldCandidate();
        handle?.ReleasePointerCapture(e.Pointer);
    }

    private void ActivateHoldMove()
    {
        var handle = _holdHandle;
        var panelId = _holdPanelId;
        var pointerId = _holdPointerId;
        var start = _holdStart;
        var startLeft = _holdStartLeft;
        var startTop = _holdStartTop;
        var startWidth = _holdStartWidth;
        var startHeight = _holdStartHeight;
        _holdTimer?.Stop();
        _holdTimer = null;
        _holdHandle = null;
        _holdPanelId = null;
        _holdPointerId = 0;
        if (handle is null || panelId is null || _project?.WorkspaceLayout is null) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == panelId.Value);
        if (panel is null) return;

        RememberLayoutForUndo();
        _layoutEditing = true;
        _selectedPanelId = panel.Id;
        _pointerPanel = panel;
        _pointerHandle = handle;
        _pointerOperation = PointerOperation.Move;
        _pointerId = pointerId;
        _pointerStart = start;
        _pointerStartLeft = startLeft;
        _pointerStartTop = startTop;
        _pointerStartWidth = startWidth;
        _pointerStartHeight = startHeight;
        if (_tileViews.TryGetValue(panel.Id, out var tile))
        {
  tile.BorderBrush = ResourceBrush("AccentFillColorDefaultBrush");
  tile.BorderThickness = new Thickness(2);
  tile.Opacity = .94;
        }
    }

    private void OnWorkspaceCanvasTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_layoutEditing || !ReferenceEquals(e.OriginalSource, sender) || _pointerOperation != PointerOperation.None) return;
        _layoutEditing = false;
        _selectedPanelId = null;
        RenderWorkspace();
    }

    private void CancelHoldCandidate()
    {
        _holdTimer?.Stop();
        _holdTimer = null;
        _holdHandle = null;
        _holdPanelId = null;
        _holdPointerId = 0;
    }

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
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
        e.Handled = true;
    }

    private void OnPanelHandlePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerHandle is not null && e.Pointer.PointerId == _pointerId) _pointerHandle.ReleasePointerCapture(e.Pointer);
        ClearPointerOperation(); RenderWorkspace();
    }

    private void ClearPointerOperation()
    {
        CancelHoldCandidate();
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

    private sealed record ShowcaseItem(string Title, string Source);
    private sealed record ProjectTypeOption(string Code, string Name);
    private sealed record MilestoneEditor(string Title, DateOnly Date, TimeOnly? Time, string? Notes);
    private sealed record RenderPanel(ProjectWorkspacePanel Panel, int X, int Y, int Width, int Height);
    private enum PointerOperation { None, Move, Resize }
}
