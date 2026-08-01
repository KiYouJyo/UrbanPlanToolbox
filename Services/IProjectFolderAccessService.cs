using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public sealed record ProjectFolderAccessResult(bool Succeeded, ProjectFolderReference? Reference = null, string? ErrorKey = null);

public interface IProjectFolderAccessService
{
    Task<ProjectFolderAccessResult> SelectAsync(Guid projectId, ProjectFolderReference? current = null);
    Task<ProjectFolderAccessResult> OpenAsync(ProjectFolderReference reference);
    void Clear(ProjectFolderReference? reference);
}
