namespace UrbanPlanToolbox.Models;

/// <summary>Machine-local lifecycle state for the first-run guide.</summary>
public sealed class FirstRunGuideState
{
    public int StateSchemaVersion { get; set; } = 1;
    public FirstRunGuideInstallationState InstallationState { get; set; }
    public int CompletedFirstRunGuideVersion { get; set; }
    public bool LegacyInstallationMigrationEvaluated { get; set; }
}

public enum FirstRunGuideInstallationState
{
    Unknown = 0,
    NewInstallation = 1,
    ExistingUserMigrated = 2,
    Pending = 3,
    Completed = 4
}
