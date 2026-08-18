namespace UrbanPlanToolbox.Services;

/// <summary>
/// Authoritative persisted-data contract versions. These are intentionally independent
/// from the product and package versions: changing an application version never implies
/// a migration or backup-format change.
/// </summary>
public static class DataContractVersions
{
    // Workspace layout is optional, reconstructable UI metadata added compatibly to the
    // existing project payload. Core project data keeps the v3 contract.
    public const int Project = 3;
    public const int Backup = 2;
}
