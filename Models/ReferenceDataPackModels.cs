using System.Text.Json;

namespace UrbanPlanToolbox.Models;

public static class ReferenceDataPackIds
{
    public const string PlanningRegulations = "planning-regulations";
    public const string PlanningTerminology = "planning-terminology";
    public const string DesignConcepts = "design-concepts";
}

public sealed class ReferenceDataPackManifest
{
    public int FormatVersion { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public Dictionary<string, string> DisplayName { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Description { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string MinAppVersion { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string? SchemaPath { get; init; }
    public string Publisher { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
}

public sealed record ReferenceDataPackState
{
    public string PackId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string ArchiveFileName { get; init; } = string.Empty;
    public string SourceKind { get; init; } = "local";
    public DateTimeOffset InstalledAt { get; init; }
}

public sealed record ReferenceDataPackContent(
    ReferenceDataPackManifest Manifest,
    ReferenceDataPackState State,
    string DataJson,
    string ArchivePath);

public sealed record ReferenceDataPackCatalogEntry(
    string PackId,
    string Version,
    int SchemaVersion,
    string MinAppVersion,
    string DownloadUrl,
    string? Sha256,
    string? FileName,
    long? SizeBytes);

public sealed record ReferenceDataPackUpdateInfo(
    ReferenceDataPackCatalogEntry? Remote,
    ReferenceDataPackState? Local,
    bool UpdateAvailable,
    string Status);

public sealed class PlanningRegulationsPackDocument
{
    public int SchemaVersion { get; init; }
    public string DataVersion { get; init; } = string.Empty;
    public JsonElement Source { get; init; }
    public List<PlanningRegulationRecord> Entries { get; init; } = [];
}

public sealed class PlanningRegulationRecord
{
    public int Id { get; init; }
    public string StableId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string JurisdictionLevel { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string DocumentLevel { get; init; } = string.Empty;
    public string OriginalTitle { get; init; } = string.Empty;
    public string ChineseTitle { get; init; } = string.Empty;
    public string? IdentifierOrYear { get; init; }
    public string ScopeAndPurpose { get; init; } = string.Empty;
    public string EffectOrAdoption { get; init; } = string.Empty;
    public string OfficialUrl { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? DownloadAndCopyrightNote { get; init; }
    public string VerifiedDate { get; init; } = string.Empty;
    public string? SearchKeywords { get; init; }
}

public sealed class PlanningTerminologyPackDocument
{
    public int SchemaVersion { get; init; }
    public string DataVersion { get; init; } = string.Empty;
    public string LastReviewed { get; init; } = string.Empty;
    public List<string> Languages { get; init; } = [];
    public JsonElement Counts { get; init; }
    public List<string> EquivalenceEnum { get; init; } = [];
    public List<PlanningTerminologyRecord> Terms { get; init; } = [];
    public JsonElement Migration { get; init; }
    public List<JsonElement> Aliases { get; init; } = [];
    public List<JsonElement> Edges { get; init; } = [];
    public List<JsonElement> HighRisk { get; init; } = [];
    public List<JsonElement> Sources { get; init; } = [];
}

public sealed class PlanningTerminologyRecord
{
    public int Id { get; init; }
    public string StableId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ZhCN { get; init; } = string.Empty;
    public string JaJP { get; init; } = string.Empty;
    public string? JaReading { get; init; }
    public string EnUS { get; init; } = string.Empty;
    public string Jurisdiction { get; init; } = string.Empty;
    public string? ConceptType { get; init; }
    public string Equivalence { get; init; } = string.Empty;
    public string DefinitionZh { get; init; } = string.Empty;
    public string DefinitionJa { get; init; } = string.Empty;
    public string DefinitionEn { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DesignConceptsPackDocument
{
    public int SchemaVersion { get; init; }
    public string DataVersion { get; init; } = string.Empty;
    public string LastReviewed { get; init; } = string.Empty;
    public List<JsonElement> Sources { get; init; } = [];
    public List<DesignConceptRecord> Entries { get; init; } = [];
}

public sealed class DesignConceptRecord
{
    public int Id { get; init; }
    public string StableId { get; init; } = string.Empty;
    public Dictionary<string, string> Title { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Aliases { get; init; } = [];
    public string Category { get; init; } = string.Empty;
    public List<string> ProjectTypes { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public Dictionary<string, string> Definition { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CaseNote { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SourceIds { get; init; } = [];
    public string ReviewStatus { get; init; } = string.Empty;
    public string LastReviewed { get; init; } = string.Empty;
}
