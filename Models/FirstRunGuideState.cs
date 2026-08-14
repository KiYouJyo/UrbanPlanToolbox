namespace UrbanPlanToolbox.Models;

/// <summary>Machine-local lifecycle state for the first-run guide.</summary>
public sealed class FirstRunGuideState
{
    public int StateSchemaVersion { get; set; } = 2;
    public FirstRunGuideInstallationState InstallationState { get; set; }
    public int CompletedFirstRunGuideVersion { get; set; }
}

public enum FirstRunGuideInstallationState
{
    Unknown = 0,
    NewInstallation = 1,
    // Retained only to read schema v1 synthetic completion. Schema v2 never writes it.
    ExistingUserMigrated = 2,
    Pending = 3,
    Completed = 4
}
