using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class JsonDataStorage
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAppDataPathProvider _pathProvider;
    private readonly int _currentSchemaVersion;
    private readonly DataMigrationRunner _migrationRunner;
    private readonly IStorageDiagnostics _diagnostics;

    public JsonDataStorage(
        IAppDataPathProvider pathProvider,
        int currentSchemaVersion,
        IEnumerable<IDataMigration>? migrations = null,
        IStorageDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        if (currentSchemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion));
        }

        _pathProvider = pathProvider;
        _currentSchemaVersion = currentSchemaVersion;
        _migrationRunner = new DataMigrationRunner(migrations);
        _diagnostics = diagnostics ?? NullStorageDiagnostics.Instance;
    }

    public async Task<DataReadResult<T>> ReadAsync<T>(
        string toolId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var filePath = _pathProvider.GetToolDataFilePath(toolId, fileName);
        var backupPath = _pathProvider.GetToolBackupFilePath(toolId, fileName);
        return await ReadFileAsync<T>(toolId, fileName, filePath, backupPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DataReadResult<T>> ReadFileAsync<T>(
        string storageId,
        string fileName,
        string filePath,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        ValidateExplicitPaths(storageId, fileName, filePath, backupPath);
        var fileLock = FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return Complete<T>(storageId, "read", DataStorageStatus.NotFound);
            }

            var primary = await ReadDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (primary.Status == DocumentStatus.Valid)
            {
                var materialized = await MaterializeAsync<T>(storageId, fileName, filePath, backupPath, primary.Document!, false, cancellationToken).ConfigureAwait(false);
                return materialized.Status == DataStorageStatus.Corrupt
                    ? await RecoverAsync<T>(storageId, fileName, filePath, backupPath, cancellationToken).ConfigureAwait(false)
                    : materialized;
            }

            if (primary.Status == DocumentStatus.FutureVersion)
            {
                return Complete<T>(storageId, "read", DataStorageStatus.UnsupportedFutureVersion, primary.SchemaVersion);
            }

            if (primary.Status == DocumentStatus.IoFailure)
            {
                return Complete<T>(storageId, "read", DataStorageStatus.IoFailure, failureType: primary.FailureType);
            }

            return await RecoverAsync<T>(storageId, fileName, filePath, backupPath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<DataWriteResult> SaveAsync<T>(
        string toolId,
        string fileName,
        T payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var filePath = _pathProvider.GetToolDataFilePath(toolId, fileName);
        var backupPath = _pathProvider.GetToolBackupFilePath(toolId, fileName);
        return await SaveFileAsync(toolId, filePath, backupPath, payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DataWriteResult> SaveFileAsync<T>(
        string storageId,
        string filePath,
        string backupPath,
        T payload,
        CancellationToken cancellationToken = default)
    {
        ValidateExplicitPaths(storageId, Path.GetFileName(filePath), filePath, backupPath);
        ArgumentNullException.ThrowIfNull(payload);
        var fileLock = FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(filePath))
            {
                var existing = await ReadDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (existing.Status == DocumentStatus.FutureVersion)
                {
                    return CompleteWrite(storageId, DataStorageStatus.UnsupportedFutureVersion);
                }

                if (existing.Status == DocumentStatus.Corrupt)
                {
                    return CompleteWrite(storageId, DataStorageStatus.Corrupt, existing.FailureType);
                }

                if (existing.Status == DocumentStatus.IoFailure)
                {
                    return CompleteWrite(storageId, DataStorageStatus.IoFailure, existing.FailureType);
                }

                if (existing.Document!.SchemaVersion < _currentSchemaVersion)
                {
                    return CompleteWrite(storageId, DataStorageStatus.MigrationFailed, "MigrationRequired");
                }

                try
                {
                    if (existing.Document.Payload.Deserialize<T>(DataStorageJson.Options) is null)
                    {
                        return CompleteWrite(storageId, DataStorageStatus.Corrupt, "NullPayload");
                    }
                }
                catch (JsonException exception)
                {
                    return CompleteWrite(storageId, DataStorageStatus.Corrupt, exception.GetType().Name);
                }
            }

            var envelope = new DataEnvelope<T>
            {
                SchemaVersion = _currentSchemaVersion,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Payload = payload
            };
            var result = await WriteEnvelopeAsync(storageId, filePath, backupPath, envelope, preserveExistingBackup: false, cancellationToken).ConfigureAwait(false);
            return CompleteWrite(storageId, result.Status, result.FailureType);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static void ValidateExplicitPaths(string storageId, string fileName, string filePath, string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        if (!Path.IsPathFullyQualified(filePath) || !Path.IsPathFullyQualified(backupPath))
        {
            throw new ArgumentException("Storage paths must be fully qualified.");
        }
    }

    private async Task<DataReadResult<T>> MaterializeAsync<T>(
        string toolId,
        string fileName,
        string filePath,
        string backupPath,
        EnvelopeDocument document,
        bool recovered,
        CancellationToken cancellationToken)
    {
        var payload = document.Payload;
        var version = document.SchemaVersion;
        T? value = default;
        var valueIsMaterialized = false;
        if (version < _currentSchemaVersion)
        {
            var migration = _migrationRunner.Run(payload, version, _currentSchemaVersion);
            if (!migration.Succeeded)
            {
                return Complete<T>(toolId, "migrate", DataStorageStatus.MigrationFailed, version, migration.FailureType);
            }

            payload = migration.Payload!;
            try
            {
                value = payload.Deserialize<T>(DataStorageJson.Options);
                if (value is null)
                {
                    return Complete<T>(toolId, "migrate", DataStorageStatus.MigrationFailed, version, "NullPayload");
                }
                valueIsMaterialized = true;
            }
            catch (JsonException exception)
            {
                return Complete<T>(toolId, "migrate", DataStorageStatus.MigrationFailed, version, exception.GetType().Name);
            }

            foreach (var name in migration.CompletedMigrations)
            {
                _diagnostics.Record(new(DateTimeOffset.UtcNow, "migrate", toolId, version, DataStorageStatus.Success, name));
            }

            var migratedDocument = new EnvelopeDocument(_currentSchemaVersion, DateTimeOffset.UtcNow, payload);
            var write = await WriteEnvelopeAsync(toolId, filePath, backupPath, migratedDocument, preserveExistingBackup: recovered, cancellationToken).ConfigureAwait(false);
            if (!write.Succeeded)
            {
                return Complete<T>(toolId, "migrate", DataStorageStatus.MigrationFailed, version, write.FailureType);
            }

            version = _currentSchemaVersion;
        }

        try
        {
            if (!valueIsMaterialized)
            {
                value = payload.Deserialize<T>(DataStorageJson.Options);
            }
            if (value is null)
            {
                return Complete<T>(toolId, "read", DataStorageStatus.Corrupt, version, "NullPayload");
            }

            var status = recovered ? DataStorageStatus.RecoveredFromBackup : DataStorageStatus.Success;
            _diagnostics.Record(new(DateTimeOffset.UtcNow, recovered ? "recover" : "read", toolId, version, status));
            return new(status, value, version);
        }
        catch (JsonException exception)
        {
            return Complete<T>(toolId, "read", DataStorageStatus.Corrupt, version, exception.GetType().Name);
        }
    }

    private async Task<DataReadResult<T>> RecoverAsync<T>(
        string toolId,
        string fileName,
        string filePath,
        string backupPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(backupPath))
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.Corrupt);
        }

        var backup = await ReadDocumentAsync(backupPath, cancellationToken).ConfigureAwait(false);
        if (backup.Status == DocumentStatus.FutureVersion)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.UnsupportedFutureVersion, backup.SchemaVersion);
        }

        if (backup.Status == DocumentStatus.IoFailure)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.IoFailure, failureType: backup.FailureType);
        }

        if (backup.Status != DocumentStatus.Valid)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.Corrupt, failureType: backup.FailureType);
        }

        var restoredDocument = backup.Document!;
        if (restoredDocument.SchemaVersion < _currentSchemaVersion)
        {
            var migration = _migrationRunner.Run(restoredDocument.Payload, restoredDocument.SchemaVersion, _currentSchemaVersion);
            if (!migration.Succeeded)
            {
                return Complete<T>(toolId, "recover", DataStorageStatus.MigrationFailed, restoredDocument.SchemaVersion, migration.FailureType);
            }

            restoredDocument = new EnvelopeDocument(_currentSchemaVersion, DateTimeOffset.UtcNow, migration.Payload!);
            foreach (var name in migration.CompletedMigrations)
            {
                _diagnostics.Record(new(DateTimeOffset.UtcNow, "migrate", toolId, restoredDocument.SchemaVersion, DataStorageStatus.Success, name));
            }
        }

        T? recoveredValue;
        try
        {
            recoveredValue = restoredDocument.Payload.Deserialize<T>(DataStorageJson.Options);
            if (recoveredValue is null)
            {
                return Complete<T>(toolId, "recover", DataStorageStatus.Corrupt, restoredDocument.SchemaVersion, "NullPayload");
            }
        }
        catch (JsonException exception)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.Corrupt, restoredDocument.SchemaVersion, exception.GetType().Name);
        }

        try
        {
            var diagnosticPath = Path.Combine(
                Path.GetDirectoryName(backupPath)!,
                $"{fileName}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
            File.Copy(filePath, diagnosticPath, overwrite: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.IoFailure, failureType: exception.GetType().Name);
        }

        var restored = await WriteEnvelopeAsync(toolId, filePath, backupPath, restoredDocument, preserveExistingBackup: true, cancellationToken).ConfigureAwait(false);
        if (!restored.Succeeded)
        {
            return Complete<T>(toolId, "recover", DataStorageStatus.IoFailure, failureType: restored.FailureType);
        }

        _diagnostics.Record(new(DateTimeOffset.UtcNow, "recover", toolId, restoredDocument.SchemaVersion, DataStorageStatus.RecoveredFromBackup));
        return new(DataStorageStatus.RecoveredFromBackup, recoveredValue, restoredDocument.SchemaVersion);
    }

    private async Task<DataWriteResult> WriteEnvelopeAsync<T>(
        string toolId,
        string filePath,
        string backupPath,
        T envelope,
        bool preserveExistingBackup,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(filePath)!, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        var backupTemporaryPath = $"{backupPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, DataStorageJson.Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            var verification = await ReadDocumentAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            if (verification.Status is not (DocumentStatus.Valid or DocumentStatus.FutureVersion) ||
                verification.SchemaVersion != _currentSchemaVersion)
            {
                return new(DataStorageStatus.IoFailure, "TemporaryFileVerificationFailed");
            }

            if (!preserveExistingBackup && File.Exists(filePath))
            {
                File.Copy(filePath, backupTemporaryPath, overwrite: false);
                File.Move(backupTemporaryPath, backupPath, overwrite: true);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
            return new(DataStorageStatus.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(DataStorageStatus.IoFailure, exception.GetType().Name);
        }
        finally
        {
            TryDelete(temporaryPath, toolId);
            TryDelete(backupTemporaryPath, toolId);
        }
    }

    private async Task<DocumentReadResult> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var document = await JsonSerializer.DeserializeAsync<EnvelopeDocument>(stream, DataStorageJson.Options, cancellationToken).ConfigureAwait(false);
            if (document is null || document.SchemaVersion < 1 || document.Payload is null || document.SavedAtUtc.Offset != TimeSpan.Zero)
            {
                return new(DocumentStatus.Corrupt, null, document?.SchemaVersion, "InvalidEnvelope");
            }

            if (document.SchemaVersion > _currentSchemaVersion)
            {
                return new(DocumentStatus.FutureVersion, document, document.SchemaVersion);
            }

            return new(DocumentStatus.Valid, document, document.SchemaVersion);
        }
        catch (JsonException exception)
        {
            return new(DocumentStatus.Corrupt, null, null, exception.GetType().Name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(DocumentStatus.IoFailure, null, null, exception.GetType().Name);
        }
    }

    private DataReadResult<T> Complete<T>(
        string toolId,
        string operation,
        DataStorageStatus status,
        int? schemaVersion = null,
        string? failureType = null)
    {
        _diagnostics.Record(new(DateTimeOffset.UtcNow, operation, toolId, schemaVersion, status, ExceptionType: failureType));
        return new(status, default, schemaVersion, failureType);
    }

    private DataWriteResult CompleteWrite(string toolId, DataStorageStatus status, string? failureType = null)
    {
        _diagnostics.Record(new(DateTimeOffset.UtcNow, "write", toolId, _currentSchemaVersion, status, ExceptionType: failureType));
        return new(status, failureType);
    }

    private void TryDelete(string path, string toolId)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Record(new(
                DateTimeOffset.UtcNow,
                "cleanup-temporary-file",
                toolId,
                _currentSchemaVersion,
                DataStorageStatus.IoFailure,
                ExceptionType: exception.GetType().Name));
        }
    }

    private enum DocumentStatus { Valid, Corrupt, FutureVersion, IoFailure }

    private sealed record DocumentReadResult(
        DocumentStatus Status,
        EnvelopeDocument? Document,
        int? SchemaVersion,
        string? FailureType = null);

    private sealed record EnvelopeDocument(int SchemaVersion, DateTimeOffset SavedAtUtc, JsonNode Payload);
}
