namespace UrbanPlanToolbox.Models;

public enum AppUpdateState { NotChecked, Checking, UpToDate, UpdateAvailable, Downloading, Verifying, ReadyToInstall, Installing, RestartRequired, Restarting, Completed, UnsupportedChannel, Cancelled, Failed }
