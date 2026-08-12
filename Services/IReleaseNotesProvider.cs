using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IReleaseNotesProvider
{
    Task<LocalizedReleaseNotes?> GetAsync(string version, string locale, CancellationToken cancellationToken = default);
}
