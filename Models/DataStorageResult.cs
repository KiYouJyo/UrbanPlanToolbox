namespace UrbanPlanToolbox.Models;

public enum DataStorageStatus
{
    Success,
    NotFound,
    RecoveredFromBackup,
    Corrupt,
    UnsupportedFutureVersion,
    MigrationFailed,
    IoFailure
}

public sealed record DataReadResult<T>(
    DataStorageStatus Status,
    T? Value = default,
    int? SchemaVersion = null,
    string? FailureType = null)
{
    public bool HasValue => Status is DataStorageStatus.Success or DataStorageStatus.RecoveredFromBackup;
}

public sealed record DataWriteResult(DataStorageStatus Status, string? FailureType = null)
{
    public bool Succeeded => Status == DataStorageStatus.Success;
}
