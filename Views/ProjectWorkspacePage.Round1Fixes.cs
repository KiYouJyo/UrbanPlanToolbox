using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using UrbanPlanToolbox.Models.Projects;
using UrbanPlanToolbox.Services;
using Windows.System;

namespace UrbanPlanToolbox.Views;

public sealed partial class ProjectWorkspacePage
{
    private readonly Dictionary<Guid, Border> _round1StrategyTiles = [];
    private bool _round1FixesInitialized;
    private bool _round1Applying;

    private void OnRound1WorkspaceLoaded(object sender, RoutedEventArgs e)
    {
        // Preserve the established Round4/Round5 initialization chain, then layer the
        // repair hooks on the same guaranteed Page.Loaded path.
        OnWorkspaceLoaded(sender, e);

        if (_round1FixesInitialized)
        {
            ApplyRound1Fixes();
            return;
        }

        _round1FixesInitialized = true;
        DrawerLayer.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnRound1DrawerKeyDown), true);

        // LayoutUpdated is only used to discover freshly recreated tiles.  All writes
        // performed from this path are idempotent so the handler can never create a
        // layout-update feedback loop.
        TileCanvas.LayoutUpdated -= OnRound1CanvasLayoutUpdated;
        TileCanvas.LayoutUpdated += OnRound1CanvasLayoutUpdated;

        // The project record may still be loading here, but the edit handler itself
        // resolves research/design at click time and is safe to wire immediately.
        RewireRound1OverviewEditor();
        DispatcherQueue.TryEnqueue(ApplyRound1Fixes);
    }

    private void RewireRound1OverviewEditor()
    {
        EditOverviewButton.Click -= OnEditOverview;
        EditOverviewButton.Click -= OnRound1EditOverview;
        EditOverviewCompactButton.Click -= OnEditOverview;
        EditOverviewCompactButton.Click -= OnRound1EditOverview;

        EditOverviewButton.Click += OnRound1EditOverview;
        EditOverviewCompactButton.Click += OnRound1EditOverview;
    }

    private void OnRound1CanvasLayoutUpdated(object? sender, object e)
    {
        if (_project is null) return;
        ApplyRound1Fixes();
    }

    private void ApplyRound1Fixes()
    {
        if (_round1Applying || _project is null) return;
        _round1Applying = true;
        try
        {
            ApplyRound1OverviewMetrics();
            UpgradeRound1StrategyTileMenus();
        }
        finally
        {
            _round1Applying = false;
        }
    }

    private void ApplyRound1OverviewMetrics()
    {
        if (_project?.Kind != ProjectKindCodes.Design) return;

        var count = ProjectStrategyList.Count(_project.PlanningRequirements);
        var label = W("重点策略", "Key strategies", "重点戦略");
        var value = count == 0
            ? W("未设置", "Not set", "未設定")
            : W($"{count} 条", $"{count} items", $"{count} 件");

        // LayoutUpdated can run frequently.  Setting Text even to the same value can
        // invalidate measure/arrange in WinUI, so never write unless the visible value
        // actually changed.
        if (!string.Equals(OverviewLabel2.Text, label, StringComparison.Ordinal))
            OverviewLabel2.Text = label;
        if (!string.Equals(OverviewValue2.Text, value, StringComparison.Ordinal))
            OverviewValue2.Text = value;
    }

    private void UpgradeRound1StrategyTileMenus()
    {
        if (_project?.Kind != ProjectKindCodes.Design || _project.WorkspaceLayout is null) return;

        foreach (var stale in _round1StrategyTiles.Keys.Where(id => !_tileViews.ContainsKey(id)).ToArray())
            _round1StrategyTiles.Remove(stale);

        foreach (var pair in _tileViews.ToArray())
        {
            var panel = _project.WorkspaceLayout.Panels.FirstOrDefault(item => item.Id == pair.Key);
            if (panel?.Kind != ProjectWorkspacePanelKinds.KeyStrategies) continue;
            if (_round1StrategyTiles.TryGetValue(panel.Id, out var hooked) && ReferenceEquals(hooked, pair.Value)) continue;

            pair.Value.ContextFlyout = CreateRound1StrategyMenu(panel);
            _round1StrategyTiles[panel.Id] = pair.Value;
        }
    }

    private FlyoutBase CreateRound1StrategyMenu(ProjectWorkspacePanel panel)
    {
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem
        {
            Text = W("编辑内容", "Edit content", "内容を編集"),
            IsEnabled = _project is { IsArchived: false }
        };
        edit.Click += (_, _) => OpenRound1StrategyEditor();
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

    private void OnRound1DrawerKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || DrawerLayer.Visibility != Visibility.Visible) return;

        // In card editors Esc means "finish editing": invoke the same Save action so
        // the current values are persisted before the drawer closes.  Non-edit drawers
        // that do not expose a Save button simply close.
        var saveText = _localization.GetString("Project_Action_Save");
        var saveButton = DrawerFooter.Children
            .OfType<Button>()
            .FirstOrDefault(button =>
                button.IsEnabled &&
                string.Equals(button.Content?.ToString(), saveText, StringComparison.Ordinal));

        if (saveButton is not null)
        {
            var peer = new ButtonAutomationPeer(saveButton);
            if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invokeProvider)
                invokeProvider.Invoke();
            else
                CloseDrawer();
        }
        else
        {
            CloseDrawer();
        }

        e.Handled = true;
    }

    private async void OnRound1EditOverview(object sender, RoutedEventArgs e)
    {
        if (_project?.Kind == ProjectKindCodes.Research)
        {
            OnEditOverview(sender, e);
            return;
        }

        if (_project is null || _project.Kind != ProjectKindCodes.Design || _project.IsArchived || _busy) return;

        var name = new TextBox
        {
            Header = _localization.GetString("Project_Field_Name"),
            Text = _project.Name,
            MaxLength = ProjectValidation.MaxNameLength
        };
        var type = new ComboBox
        {
            Header = _localization.GetString("Project_Field_Type"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var options = ProjectTypeCodes.All
            .Select(code => new ProjectTypeOption(code, ProjectPresentation.GetDesignTypeName(code, _localization)))
            .ToArray();
        type.ItemsSource = options;
        type.DisplayMemberPath = nameof(ProjectTypeOption.Name);
        type.SelectedItem = options.FirstOrDefault(option => option.Code == _project.Type) ?? options[0];

        var customType = new TextBox
        {
            Header = _localization.GetString("Project_Field_CustomType"),
            Text = _project.CustomType ?? string.Empty,
            MaxLength = ProjectValidation.MaxTypeLength,
            Visibility = _project.Type == ProjectTypeCodes.Other ? Visibility.Visible : Visibility.Collapsed
        };
        type.SelectionChanged += (_, _) =>
            customType.Visibility = (type.SelectedItem as ProjectTypeOption)?.Code == ProjectTypeCodes.Other
                ? Visibility.Visible
                : Visibility.Collapsed;

        var description = new TextBox
        {
            Header = _localization.GetString("Project_Field_Description"),
            Text = _project.Description ?? string.Empty,
            MaxLength = ProjectValidation.MaxDescriptionLength,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };

        var strategies = CreateRound1StrategyListEditor(_project.PlanningRequirements);
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(name);
        stack.Children.Add(type);
        stack.Children.Add(customType);
        stack.Children.Add(description);
        stack.Children.Add(new TextBlock
        {
            Text = W("重点策略", "Key strategies", "重点戦略"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = W("每条策略单独记录，可新增、删除或调整内容。", "Record each strategy separately; add, remove, or edit items as needed.", "戦略を1件ずつ記録し、追加・削除・編集できます。"),
            Opacity = .62,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(strategies.Root);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = W("编辑项目概览", "Edit project overview", "プロジェクト概要を編集"),
            Content = new ScrollViewer
            {
                Content = stack,
                MaxHeight = Math.Max(300, XamlRoot.Size.Height - 260),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = _localization.GetString("Project_Action_Save"),
            CloseButtonText = _localization.GetString("Action_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await AppDialogService.Default.ShowAsync(dialog) != ContentDialogResult.Primary) return;

        var candidate = CloneProject(_project);
        candidate.Name = name.Text;
        candidate.Type = (type.SelectedItem as ProjectTypeOption)?.Code ?? candidate.Type;
        candidate.CustomType = candidate.Type == ProjectTypeCodes.Other ? customType.Text : null;
        candidate.Description = description.Text;
        candidate.PlanningRequirements = ProjectStrategyList.Serialize(strategies.Values);
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await SaveCandidateAsync(candidate);
        ApplyRound1OverviewMetrics();
    }

    private void OpenRound1StrategyEditor()
    {
        if (_project is null || _project.Kind != ProjectKindCodes.Design || _project.IsArchived) return;

        DrawerTitle.Text = W("编辑重点策略", "Edit key strategies", "重点戦略を編集");
        DrawerSubtitle.Text = W("一条一条记录策略，而不是在一个文本框内混合输入。", "Keep strategies as separate items instead of mixing them in one text box.", "1つのテキスト欄にまとめず、戦略を1件ずつ記録します。");
        DrawerContent.Children.Clear();
        DrawerFooter.Children.Clear();

        var strategies = CreateRound1StrategyListEditor(_project.PlanningRequirements);
        DrawerContent.Children.Add(strategies.Root);

        var cancel = new Button { Content = _localization.GetString("Action_Cancel") };
        cancel.Click += (_, _) => CloseDrawer();
        DrawerFooter.Children.Add(cancel);

        var save = new Button { Content = _localization.GetString("Project_Action_Save") };
        save.Click += async (_, _) =>
        {
            if (_project is null) return;
            var candidate = CloneProject(_project);
            candidate.PlanningRequirements = ProjectStrategyList.Serialize(strategies.Values);
            candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await SaveCandidateAsync(candidate);
            CloseDrawer();
            ApplyProject();
            ApplyRound1OverviewMetrics();
        };
        DrawerFooter.Children.Add(save);
        ShowDrawer();
    }

    private StrategyListEditor CreateRound1StrategyListEditor(string? source) => new(
        ProjectStrategyList.Parse(source),
        W("策略内容", "Strategy", "戦略内容"),
        W("删除策略", "Remove strategy", "戦略を削除"),
        W("＋ 添加策略", "+ Add strategy", "＋ 戦略を追加"));

    private sealed class StrategyListEditor
    {
        private readonly string _placeholder;
        private readonly string _removeTooltip;
        private readonly List<RowState> _rows = [];
        private readonly StackPanel _rowsHost = new() { Spacing = 8 };

        public StrategyListEditor(IEnumerable<string> values, string placeholder, string removeTooltip, string addText)
        {
            _placeholder = placeholder;
            _removeTooltip = removeTooltip;
            Root = new StackPanel { Spacing = 8 };
            Root.Children.Add(_rowsHost);

            foreach (var value in values) AddRow(value);
            if (_rows.Count == 0) AddRow(string.Empty);

            var add = new Button
            {
                Content = addText,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            add.Click += (_, _) => AddRow(string.Empty);
            Root.Children.Add(add);
        }

        public StackPanel Root { get; }
        public IEnumerable<string> Values => _rows.Select(row => row.Editor.Text);

        private void AddRow(string value)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var number = new TextBlock
            {
                MinWidth = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = .62,
                TextAlignment = TextAlignment.Right
            };
            grid.Children.Add(number);

            var editor = new TextBox
            {
                Text = value,
                PlaceholderText = _placeholder,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);

            var remove = new Button
            {
                Content = "×",
                MinWidth = 36,
                Width = 36,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(remove, _removeTooltip);
            Grid.SetColumn(remove, 2);
            grid.Children.Add(remove);

            var state = new RowState(grid, number, editor);
            remove.Click += (_, _) => RemoveRow(state);
            _rows.Add(state);
            _rowsHost.Children.Add(grid);
            Renumber();
            editor.Focus(FocusState.Programmatic);
        }

        private void RemoveRow(RowState row)
        {
            if (!_rows.Remove(row)) return;
            _rowsHost.Children.Remove(row.Root);
            if (_rows.Count == 0) AddRow(string.Empty);
            else Renumber();
        }

        private void Renumber()
        {
            for (var index = 0; index < _rows.Count; index++)
                _rows[index].Number.Text = $"{index + 1}.";
        }

        private sealed record RowState(Grid Root, TextBlock Number, TextBox Editor);
    }
}
