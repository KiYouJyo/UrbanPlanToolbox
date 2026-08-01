using System.Text.Json.Nodes;

namespace UrbanPlanToolbox.Services;

public sealed class DataMigrationRunner
{
    private readonly IReadOnlyDictionary<int, IDataMigration> _migrations;

    public DataMigrationRunner(IEnumerable<IDataMigration>? migrations = null)
    {
        var steps = (migrations ?? []).ToArray();
        if (steps.Any(step => string.IsNullOrWhiteSpace(step.Name) || step.FromVersion < 1 || step.ToVersion != step.FromVersion + 1))
        {
            throw new ArgumentException("Migrations must be named, positive, single-version steps.", nameof(migrations));
        }

        var duplicate = steps.GroupBy(step => step.FromVersion).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate migration from schema version {duplicate.Key}.", nameof(migrations));
        }

        _migrations = steps.ToDictionary(step => step.FromVersion);
    }

    public DataMigrationResult Run(JsonNode payload, int fromVersion, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (fromVersion < 1 || targetVersion < fromVersion)
        {
            return new(false, fromVersion, null, [], nameof(ArgumentOutOfRangeException));
        }

        var completed = new List<string>();
        var currentPayload = payload.DeepClone();
        var version = fromVersion;
        try
        {
            while (version < targetVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                {
                    return new(false, version, null, completed, "MissingMigration");
                }

                currentPayload = migration.Apply(currentPayload) ?? throw new InvalidOperationException("Migration returned no payload.");
                version = migration.ToVersion;
                completed.Add(migration.Name);
            }

            return new(true, version, currentPayload, completed);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new(false, version, null, completed, exception.GetType().Name);
        }
    }
}
