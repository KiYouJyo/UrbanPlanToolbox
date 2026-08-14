namespace UrbanPlanToolbox.Services;

/// <summary>
/// Authoritative persisted-data contract versions. These are intentionally independent
/// from the product and package versions: changing an application version never implies
/// a migration or backup-format change.
/// </summary>
public static class DataContractVersions
{
    public const int Project = 3;
    public const int Backup = 2;
}
