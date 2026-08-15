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
    public MilestoneReminderRepeatInterval ProjectMilestoneReminderRepeatInterval { get; set; } = MilestoneReminderRepeatInterval.None;
    public List<string> FavoriteToolIds { get; set; } = [];
    public int? LastNormalWindowWidth { get; set; }
    public int? LastNormalWindowHeight { get; set; }
    public bool WasWindowMaximized { get; set; }
    public bool BackgroundResidencyEnabled { get; set; }
    public bool SilentStartupShowRecorder { get; set; }
    // Legacy persisted values are retained solely for one-time migration reads.
    [System.Text.Json.Serialization.JsonIgnore] public bool? CloseToTrayEnabled { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool? StartWithWindows { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool? InspirationRecorderEnabled { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool? ShowRecorderOnBackgroundStartup { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool? InspirationRecorderAlwaysOnTop { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsProjectMilestoneNotificationsEnabled =>
        ProjectMilestoneNotificationsEnabled ?? DefaultProjectMilestoneNotificationsEnabled;

    [System.Text.Json.Serialization.JsonIgnore]
    public MilestoneReminderRepeatInterval NormalizedProjectMilestoneReminderRepeatInterval =>
        Enum.IsDefined(ProjectMilestoneReminderRepeatInterval)
            ? ProjectMilestoneReminderRepeatInterval
            : MilestoneReminderRepeatInterval.None;
}
