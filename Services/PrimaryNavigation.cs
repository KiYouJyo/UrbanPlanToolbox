using System.Collections.ObjectModel;
using UrbanPlanToolbox.Models.Navigation;

namespace UrbanPlanToolbox.Services;

public sealed class PrimaryNavigation
{
    private readonly IReadOnlyDictionary<string, PrimaryNavigationDefinition> _routesById;

    public static PrimaryNavigation Default { get; } = new(
    [
        new(PrimaryNavigationIds.Welcome, "欢迎页面", typeof(Views.HomePage)),
        new(PrimaryNavigationIds.CommonTools, "常用功能", typeof(Views.CommonToolsPage)),
        new(PrimaryNavigationIds.DesignTools, "设计工具", typeof(Views.DesignToolsPage)),
        new(PrimaryNavigationIds.ResearchTools, "科研工具", typeof(Views.ResearchToolsPage)),
        new(PrimaryNavigationIds.ProjectArchive, "项目归档", typeof(Views.ProjectArchivePage)),
        new(PrimaryNavigationIds.About, "关于", typeof(Views.AboutPage))
    ]);

    public PrimaryNavigation(IEnumerable<PrimaryNavigationDefinition> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var routeArray = routes.ToArray();
        if (routeArray.Any(route => string.IsNullOrWhiteSpace(route.Id)))
        {
            throw new ArgumentException("Primary navigation IDs cannot be empty.", nameof(routes));
        }

        var duplicateId = routeArray
            .GroupBy(route => route.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException($"Duplicate primary navigation ID: {duplicateId}", nameof(routes));
        }

        All = Array.AsReadOnly(routeArray);
        _routesById = new ReadOnlyDictionary<string, PrimaryNavigationDefinition>(
            routeArray.ToDictionary(route => route.Id, StringComparer.Ordinal));
    }

    public IReadOnlyList<PrimaryNavigationDefinition> All { get; }

    public bool TryGet(string? id, out PrimaryNavigationDefinition? route)
    {
        if (id is null)
        {
            route = null;
            return false;
        }

        return _routesById.TryGetValue(id, out route);
    }
}
