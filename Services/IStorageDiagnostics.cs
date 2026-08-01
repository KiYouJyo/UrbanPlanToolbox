using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed record StorageDiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string Operation,
    string ToolId,
    int? SchemaVersion,
    DataStorageStatus Status,
    string? MigrationName = null,
    string? ExceptionType = null);

public interface IStorageDiagnostics
{
    void Record(StorageDiagnosticEvent diagnosticEvent);
}

public sealed class NullStorageDiagnostics : IStorageDiagnostics
{
    public static NullStorageDiagnostics Instance { get; } = new();
    private NullStorageDiagnostics() { }
    public void Record(StorageDiagnosticEvent diagnosticEvent) { }
}
