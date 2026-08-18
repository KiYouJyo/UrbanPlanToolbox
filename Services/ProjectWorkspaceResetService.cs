using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public static class ProjectWorkspaceResetService
{
    public static ProjectWorkspaceLayout CreateDefaultPreservingPanelData(ProjectWorkspaceLayout? current, string projectKind)
    {
        var defaults = ProjectWorkspaceLayoutService.CreateDefault(projectKind);
        if (current is null || current.Panels.Count == 0) return defaults;

        var remainingByKind = current.Panels
            .GroupBy(panel => panel.Kind, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<ProjectWorkspacePanel>(group.Select(ClonePanel)),
                StringComparer.Ordinal);

        var result = new ProjectWorkspaceLayout { Version = Math.Max(current.Version, defaults.Version) };
        foreach (var template in defaults.Panels)
        {
            ProjectWorkspacePanel panel;
            if (remainingByKind.TryGetValue(template.Kind, out var matching) && matching.Count > 0)
            {
                var existing = matching.Dequeue();
                panel = new ProjectWorkspacePanel
                {
                    Id = existing.Id,
                    Kind = existing.Kind,
                    Title = existing.Title,
                    X = template.X,
                    Y = template.Y,
                    Width = template.Width,
                    Height = template.Height,
                    Settings = new Dictionary<string, string>(existing.Settings, StringComparer.Ordinal)
                };
            }
            else
            {
                panel = ClonePanel(template);
            }
            result.Panels.Add(panel);
        }

        var nextY = result.Panels.Count == 0 ? 0 : result.Panels.Max(panel => panel.Y + panel.Height);
        foreach (var queue in remainingByKind.Values)
        {
            while (queue.Count > 0)
            {
                var extra = queue.Dequeue();
                extra.X = 0;
                extra.Y = nextY;
                extra.Width = Math.Clamp(extra.Width, 1, ProjectWorkspaceLayoutService.Columns);
                extra.Height = Math.Max(1, extra.Height);
                result.Panels.Add(extra);
                nextY += extra.Height;
            }
        }

        ProjectWorkspaceLayoutService.Normalize(result, projectKind);
        return result;
    }

    private static ProjectWorkspacePanel ClonePanel(ProjectWorkspacePanel source) => new()
    {
        Id = source.Id,
        Kind = source.Kind,
        Title = source.Title,
        X = source.X,
        Y = source.Y,
        Width = source.Width,
        Height = source.Height,
        Settings = new Dictionary<string, string>(source.Settings, StringComparer.Ordinal)
    };
}
