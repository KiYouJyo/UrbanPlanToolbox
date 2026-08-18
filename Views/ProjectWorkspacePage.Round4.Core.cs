using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Windows.Foundation;

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
        if (tile.Child is not Grid root || replacement is not FrameworkElement replacementElement) return;
        var oldBody = root.Children
            .OfType<FrameworkElement>()
            .Where(child => Grid.GetRow(child) == 1)
            .FirstOrDefault(child => child is not Border { Tag: Guid });
        if (oldBody is not null) root.Children.Remove(oldBody);
        Grid.SetRow(replacementElement, 1);
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
            var delete = new MenuFlyoutItem
            {
                Text = W("删除面板", "Remove panel", "パネルを削除"),
                Tag = panel.Id
            };
            delete.Click += OnRemovePanel;
            menu.Items.Add(delete);
        }
        return menu;
    }

    private void OnRound4TilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_project?.WorkspaceLayout is null || _project.IsArchived || _busy || _columnCount <= 1 ||
            sender is not Border { Tag: Guid id } tile) return;
        if (IsRound4InteractiveSource(e.OriginalSource as DependencyObject, tile) ||
            IsRound4ResizeSource(e.OriginalSource as DependencyObject, tile)) return;

        var current = e.GetCurrentPoint(tile);
        if (!current.Properties.IsLeftButtonPressed) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null) return;

        CancelRound4PointerState();
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
                if (_round4PointerPanel is null || _round4PointerTile is null) return;
                _layoutEditing = true;
                _selectedPanelId = _round4PointerPanel.Id;
                StartRound4PointerOperation(Round4PointerOperation.Move);
                if (_columnCount != ProjectWorkspaceLayoutService.Columns)
                    EnsureRound4ResizeGrip(_round4PointerTile, _round4PointerPanel);
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
                _round4PointerTile.ReleasePointerCapture(e.Pointer);
                CancelRound4PointerState();
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
            var renderY = (int)Math.Round(Math.Max(0, Canvas.GetTop(_round4PointerTile)) / (TileRowHeight + TileGap));
            var canonicalX = Round4ToCanonicalColumn(renderX);
            ProjectWorkspaceLayoutService.MovePanel(_project.WorkspaceLayout, _round4PointerPanel.Id, canonicalX, renderY);
            CancelRound4PointerState();
            await PersistProjectAsync(showSuccess: false);
            RenderWorkspace();
            DispatcherQueue.TryEnqueue(ApplyRound4TileUpgrades);
            e.Handled = true;
            return;
        }

        _round4PointerTile.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState();
    }

    private void OnRound4TilePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerTile is not null && ReferenceEquals(sender, _round4PointerTile) && e.Pointer.PointerId == _round4PointerId)
            _round4PointerTile.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState();
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
            Child = new TextBlock
            {
                Text = "↘",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
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
        if (_project?.WorkspaceLayout is null || _columnCount <= 1 ||
            sender is not FrameworkElement { Tag: Guid id } grip || !_tileViews.TryGetValue(id, out var tile)) return;
        var current = e.GetCurrentPoint(grip);
        if (!current.Properties.IsLeftButtonPressed) return;
        var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == id);
        if (panel is null) return;

        CancelRound4PointerState();
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
        if (_round4PointerOperation != Round4PointerOperation.Resize || _round4PointerHandle is null ||
            !ReferenceEquals(sender, _round4PointerHandle) || _round4PointerTile is null || e.Pointer.PointerId != _round4PointerId) return;
        var position = e.GetCurrentPoint(TileCanvas).Position;
        _round4PointerTile.Width = Math.Max(_unitWidth, _round4StartWidth + position.X - _round4PointerStart.X);
        _round4PointerTile.Height = Math.Max(TileRowHeight, _round4StartHeight + position.Y - _round4PointerStart.Y);
        e.Handled = true;
    }

    private async void OnRound4ResizePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerOperation != Round4PointerOperation.Resize || _round4PointerHandle is null ||
            !ReferenceEquals(sender, _round4PointerHandle) || _round4PointerTile is null || _round4PointerPanel is null ||
            _project?.WorkspaceLayout is null || e.Pointer.PointerId != _round4PointerId) return;

        _round4PointerHandle.ReleasePointerCapture(e.Pointer);
        var renderWidth = Math.Max(1, (int)Math.Round((_round4PointerTile.Width + TileGap) / (_unitWidth + TileGap)));
        var height = Math.Max(1, (int)Math.Round((_round4PointerTile.Height + TileGap) / (TileRowHeight + TileGap)));
        var canonicalWidth = Round4ToCanonicalSpan(renderWidth);
        ProjectWorkspaceLayoutService.ResizePanel(_project.WorkspaceLayout, _round4PointerPanel.Id, canonicalWidth, height);
        CancelRound4PointerState();
        await PersistProjectAsync(showSuccess: false);
        RenderWorkspace();
        DispatcherQueue.TryEnqueue(ApplyRound4TileUpgrades);
        e.Handled = true;
    }

    private void OnRound4ResizePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_round4PointerHandle is not null && ReferenceEquals(sender, _round4PointerHandle) && e.Pointer.PointerId == _round4PointerId)
            _round4PointerHandle.ReleasePointerCapture(e.Pointer);
        CancelRound4PointerState();
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

    private void CancelRound4PointerState()
    {
        _round4HoldTimer?.Stop();
        _round4HoldTimer = null;
        _round4PointerTile = null;
        _round4PointerPanel = null;
        _round4PointerHandle = null;
        _round4PointerId = 0;
        _round4PointerOperation = Round4PointerOperation.None;
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
            if (current is FrameworkElement element && element.Tag is Guid && element.Width <= 32 && element.Height <= 32)
                return true;
        }
        return false;
    }

    private enum Round4PointerOperation { None, Move, Resize }
}
