namespace UrbanPlanToolbox.Models.Interaction;

/// <summary>Lifecycle states shared by user initiated asynchronous work.</summary>
public enum OperationState
{
    Idle,
    Running,
    Succeeded,
    Failed,
    Canceled
}

/// <summary>Presentation states that distinguish a failed load from an empty result.</summary>
public enum PageState
{
    Loading,
    Empty,
    Content,
    Error
}

public enum AppNotificationKind
{
    Informational,
    Success,
    Warning,
    Error
}

public sealed record AppNotification(
    AppNotificationKind Kind,
    string Title,
    string Message,
    bool IsPersistent = false,
    TimeSpan? Duration = null);

public enum UnsavedChangesDecision
{
    SaveAndContinue,
    DiscardAndContinue,
    Cancel
}
