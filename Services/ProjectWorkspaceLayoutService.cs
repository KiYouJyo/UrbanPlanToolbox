using System.Text.Json;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

/// <summary>
/// Pure layout engine for the v1.9 project workspace.  The persisted contract always uses a
/// 12-column grid; responsive views may reflow it without writing derived coordinates back.
/// </summary>
public static class ProjectWorkspaceLayoutService
{
    public const int Columns = 12;
    public const int LayoutVersion = 1;
    public const int MaxRows = 96;

    private static readonly string[] DesignKinds =
    [
        ProjectWorkspacePanelKinds.ImageShowcase,
        ProjectWorkspacePanelKinds.Milestones,
        ProjectWorkspacePanelKinds.Description,
        ProjectWorkspacePanelKinds.Inspirations,
        ProjectWorkspacePanelKinds.Files,
        ProjectWorkspacePanelKinds.KeyStrategies
    ];

    private static readonly string[] ResearchKinds =
    [
        ProjectWorkspacePanelKinds.ResearchFramework,
        ProjectWorkspacePanelKinds.ResearchQuestion,
        ProjectWorkspacePanelKinds.ResearchProgress,
        ProjectWorkspacePanelKinds.Inspirations,
        ProjectWorkspacePanelKinds.Files
    ];

    public static IReadOnlyList<string> GetAllowedPanelKinds(string projectKind) =>
        string.Equals(projectKind, ProjectKindCodes.Research, StringComparison.Ordinal) ? ResearchKinds : DesignKinds;

    public static ProjectWorkspaceLayout EnsureLayout(ProjectRecord project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.WorkspaceLayout is null || project.WorkspaceLayout.Panels.Count == 0)
        {
            project.WorkspaceLayout = CreateDefault(project.Kind);
            return project.WorkspaceLayout;
        }

        Normalize(project.WorkspaceLayout, project.Kind);
        return project.WorkspaceLayout;
    }

    public static ProjectWorkspaceLayout CreateDefault(string projectKind)
    {
        var layout = new ProjectWorkspaceLayout { Version = LayoutVersion };
        if (string.Equals(projectKind, ProjectKindCodes.Research, StringComparison.Ordinal))
        {
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.ResearchFramework, 0, 0, 9, 3));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.ResearchQuestion, 9, 0, 3, 3));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.ResearchProgress, 0, 3, 12, 2));
        }
        else
        {
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.ImageShowcase, 0, 0, 6, 3));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.Milestones, 6, 0, 6, 2));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.Description, 6, 2, 6, 1));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.Inspirations, 0, 3, 4, 2));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.Files, 4, 3, 4, 2));
            layout.Panels.Add(Create(ProjectWorkspacePanelKinds.KeyStrategies, 8, 3, 4, 2));
        }
        return layout;
    }

    public static ProjectWorkspacePanel AddPanel(ProjectWorkspaceLayout layout, string projectKind, string panelKind)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!GetAllowedPanelKinds(projectKind).Contains(panelKind, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(panelKind), panelKind, "Panel kind is not available for this project kind.");

        var (width, height) = DefaultSize(panelKind);
        var (x, y) = FindFirstFreeSlot(layout, width, height);
        var panel = Create(panelKind, x, y, width, height);
        layout.Panels.Add(panel);
        return panel;
    }

    public static void MovePanel(ProjectWorkspaceLayout layout, Guid panelId, int x, int y)
    {
        var panel = Find(layout, panelId);
        panel.X = Math.Clamp(x, 0, Math.Max(0, Columns - panel.Width));
        panel.Y = Math.Clamp(y, 0, MaxRows - panel.Height);
        ResolveCollisions(layout, panel);
    }

    public static void ResizePanel(ProjectWorkspaceLayout layout, Guid panelId, int width, int height)
    {
        var panel = Find(layout, panelId);
        var (minW, minH, maxW, maxH) = SizeBounds(panel.Kind);
        panel.Width = Math.Clamp(width, minW, Math.Min(maxW, Columns));
        panel.Height = Math.Clamp(height, minH, maxH);
        panel.X = Math.Clamp(panel.X, 0, Columns - panel.Width);
        panel.Y = Math.Clamp(panel.Y, 0, MaxRows - panel.Height);
        ResolveCollisions(layout, panel);
    }

    public static ProjectWorkspacePanel DuplicatePanel(ProjectWorkspaceLayout layout, string projectKind, Guid panelId)
    {
        var source = Find(layout, panelId);
        var (x, y) = FindFirstFreeSlot(layout, source.Width, source.Height);
        var duplicate = new ProjectWorkspacePanel
        {
            Id = Guid.NewGuid(), Kind = source.Kind, Title = source.Title,
            X = x, Y = y, Width = source.Width, Height = source.Height
        };
        foreach (var entry in source.Settings) duplicate.Settings[entry.Key] = entry.Value;
        layout.Panels.Add(duplicate);
        return duplicate;
    }

    public static bool RemovePanel(ProjectWorkspaceLayout layout, Guid panelId) =>
        layout.Panels.RemoveAll(panel => panel.Id == panelId) > 0;

    public static ProjectWorkspaceLayout Clone(ProjectWorkspaceLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return JsonSerializer.Deserialize<ProjectWorkspaceLayout>(
                   JsonSerializer.Serialize(layout, DataStorageJson.Options), DataStorageJson.Options)
               ?? new ProjectWorkspaceLayout();
    }

    public static void Normalize(ProjectWorkspaceLayout layout, string projectKind)
    {
        ArgumentNullException.ThrowIfNull(layout);
        layout.Version = LayoutVersion;
        var allowed = GetAllowedPanelKinds(projectKind);
        layout.Panels.RemoveAll(panel => panel.Id == Guid.Empty || !ProjectWorkspacePanelKinds.IsValid(panel.Kind) || !allowed.Contains(panel.Kind, StringComparer.Ordinal));

        foreach (var panel in layout.Panels)
        {
            var (minW, minH, maxW, maxH) = SizeBounds(panel.Kind);
            panel.Width = Math.Clamp(panel.Width, minW, Math.Min(maxW, Columns));
            panel.Height = Math.Clamp(panel.Height, minH, maxH);
            panel.X = Math.Clamp(panel.X, 0, Columns - panel.Width);
            panel.Y = Math.Clamp(panel.Y, 0, MaxRows - panel.Height);
        }

        // Preserve user order as the collision priority: older/earlier panels stay where they are,
        // later panels are pushed down into the first valid free row.
        for (var index = 0; index < layout.Panels.Count; index++)
        {
            var panel = layout.Panels[index];
            while (layout.Panels.Take(index).Any(other => Intersects(panel, other)) && panel.Y < MaxRows - panel.Height)
                panel.Y++;
        }
    }

    public static bool HasOverlap(ProjectWorkspaceLayout layout)
    {
        for (var i = 0; i < layout.Panels.Count; i++)
        for (var j = i + 1; j < layout.Panels.Count; j++)
            if (Intersects(layout.Panels[i], layout.Panels[j])) return true;
        return false;
    }

    public static (int Width, int Height) DefaultSize(string kind) => kind switch
    {
        ProjectWorkspacePanelKinds.ImageShowcase => (6, 3),
        ProjectWorkspacePanelKinds.Milestones => (6, 2),
        ProjectWorkspacePanelKinds.Description => (6, 1),
        ProjectWorkspacePanelKinds.Inspirations => (4, 2),
        ProjectWorkspacePanelKinds.Files => (4, 2),
        ProjectWorkspacePanelKinds.KeyStrategies => (4, 2),
        ProjectWorkspacePanelKinds.ResearchFramework => (9, 3),
        ProjectWorkspacePanelKinds.ResearchQuestion => (3, 3),
        ProjectWorkspacePanelKinds.Chart => (6, 3),
        ProjectWorkspacePanelKinds.Literature => (3, 3),
        ProjectWorkspacePanelKinds.DataAndScripts => (3, 3),
        ProjectWorkspacePanelKinds.ResearchProgress => (12, 2),
        ProjectWorkspacePanelKinds.TextNote => (4, 2),
        _ => (4, 2)
    };

    public static (int MinWidth, int MinHeight, int MaxWidth, int MaxHeight) SizeBounds(string kind) => kind switch
    {
        ProjectWorkspacePanelKinds.ResearchFramework => (2, 1, 12, 6),
        ProjectWorkspacePanelKinds.Chart => (2, 1, 12, 6),
        ProjectWorkspacePanelKinds.ImageShowcase => (1, 1, 12, 6),
        ProjectWorkspacePanelKinds.ResearchProgress or ProjectWorkspacePanelKinds.Milestones => (1, 1, 12, 4),
        ProjectWorkspacePanelKinds.Description or ProjectWorkspacePanelKinds.TextNote => (1, 1, 12, 5),
        _ => (1, 1, 12, 5)
    };

    private static ProjectWorkspacePanel Create(string kind, int x, int y, int width, int height) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, X = x, Y = y, Width = width, Height = height
    };

    private static ProjectWorkspacePanel Find(ProjectWorkspaceLayout layout, Guid id) =>
        layout.Panels.FirstOrDefault(panel => panel.Id == id)
        ?? throw new KeyNotFoundException($"Workspace panel {id:D} was not found.");

    private static (int X, int Y) FindFirstFreeSlot(ProjectWorkspaceLayout layout, int width, int height)
    {
        width = Math.Clamp(width, 1, Columns);
        for (var y = 0; y <= MaxRows - height; y++)
        for (var x = 0; x <= Columns - width; x++)
        {
            var candidate = new ProjectWorkspacePanel { Id = Guid.Empty, Kind = ProjectWorkspacePanelKinds.Custom, X = x, Y = y, Width = width, Height = height };
            if (layout.Panels.All(existing => !Intersects(candidate, existing))) return (x, y);
        }
        return (0, Math.Max(0, layout.Panels.Select(panel => panel.Y + panel.Height).DefaultIfEmpty(0).Max()));
    }

    private static void ResolveCollisions(ProjectWorkspaceLayout layout, ProjectWorkspacePanel moved)
    {
        foreach (var other in layout.Panels.Where(panel => panel.Id != moved.Id).OrderBy(panel => panel.Y).ThenBy(panel => panel.X))
        {
            if (!Intersects(moved, other)) continue;
            other.Y = Math.Min(MaxRows - other.Height, moved.Y + moved.Height);
            while (layout.Panels.Any(candidate => candidate.Id != other.Id && candidate.Id != moved.Id && Intersects(candidate, other)) && other.Y < MaxRows - other.Height)
                other.Y++;
        }
    }

    private static bool Intersects(ProjectWorkspacePanel left, ProjectWorkspacePanel right) =>
        left.X < right.X + right.Width && left.X + left.Width > right.X &&
        left.Y < right.Y + right.Height && left.Y + left.Height > right.Y;
}
