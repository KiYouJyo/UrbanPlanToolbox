namespace UrbanPlanToolbox.Services;

/// <summary>
/// Authoritative persisted-data contract versions. These are intentionally independent
/// from the product and package versions: changing an application version never implies
/// a migration or backup-format change.
/// </summary>
public static class DataContractVersions
{
    // v4 adds the project-scoped customizable workspace layout.  Older builds must not
    // silently rewrite v1.9 project files and drop this new persisted field.
    public const int Project = 4;
    public const int Backup = 2;
}
