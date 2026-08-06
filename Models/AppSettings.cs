using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Models;

public sealed class AppSettings
{
    public const bool DefaultProjectMilestoneNotificationsEnabled = false;
    public int SchemaVersion { get; set; } = 1;
    public string Theme { get; set; } = "System";
    public int DecimalPlaces { get; set; } = 2;
    public bool AutoCalculate { get; set; }
    public string Language { get; set; } = LanguagePreference.SystemValue;
    // Nullable distinguishes legacy settings files that predate the app-level reminder switch.
    public bool? ProjectMilestoneNotificationsEnabled { get; set; }
    public List<string> FavoriteToolIds { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsProjectMilestoneNotificationsEnabled =>
        ProjectMilestoneNotificationsEnabled ?? DefaultProjectMilestoneNotificationsEnabled;
}
