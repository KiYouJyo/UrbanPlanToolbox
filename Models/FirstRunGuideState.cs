namespace UrbanPlanToolbox.Models;

/// <summary>Machine-local lifecycle state for the first-run guide.</summary>
public sealed class FirstRunGuideState
{
    public int StateSchemaVersion { get; set; } = 1;
    public int CompletedFirstRunGuideVersion { get; set; }
    public bool LegacyInstallationMigrationEvaluated { get; set; }
}
