using System.Text.Json.Serialization;

namespace UrbanPlanToolbox.Models;

public sealed class PlanningTerminologyDataset
{
    public int SchemaVersion { get; set; }
    public string DataVersion { get; set; } = string.Empty;
    public string LastReviewed { get; set; } = string.Empty;
    public List<string> Language { get; set; } = [];
    public PlanningTerminologyCounts Counts { get; set; } = new();
    public List<string> EquivalenceEnum { get; set; } = [];
    public List<PlanningTerm> Terms { get; set; } = [];
    public List<TerminologyAlias> Aliases { get; set; } = [];
    public List<TerminologyRelation> Relations { get; set; } = [];
    public List<HighRiskEquivalence> HighRiskEquivalences { get; set; } = [];
    public Dictionary<string, TerminologySource> Sources { get; set; } = [];
}

public sealed class PlanningTerminologyCounts
{
    public int Terms { get; set; }
    public int Aliases { get; set; }
    [JsonPropertyName("edges")] public int Relations { get; set; }
    [JsonPropertyName("highrisk")] public int HighRisk { get; set; }
    public int Sources { get; set; }
}

public sealed class PlanningTerm
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ZhCN { get; set; } = string.Empty;
    public string JaJP { get; set; } = string.Empty;
    public string JaReading { get; set; } = string.Empty;
    public string EnUS { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public string Equivalence { get; set; } = string.Empty;
    public string DefinitionZh { get; set; } = string.Empty;
    public string DefinitionJa { get; set; } = string.Empty;
    public string DefinitionEn { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
    public string ConfusableOrRelated { get; set; } = string.Empty;
    public List<string> SourceIds { get; set; } = [];
    public string SourceStatus { get; set; } = string.Empty;
    public string ReviewNote { get; set; } = string.Empty;
    public string TranslationStatus { get; set; } = string.Empty;
    public string LastReviewed { get; set; } = string.Empty;
    public List<int> RelatedTermIds { get; set; } = [];
    public string ReleaseStatus { get; set; } = string.Empty;
}

public sealed class TerminologyAlias
{
    public int TermId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class TerminologyRelation
{
    public string Id { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string NoteZh { get; set; } = string.Empty;
    public List<string> SourceIds { get; set; } = [];
}

public sealed class TerminologySource
{
    public string Authority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class HighRiskEquivalence
{
    public string Id { get; set; } = string.Empty;
    public string TermA { get; set; } = string.Empty;
    public string SystemA { get; set; } = string.Empty;
    public string TermB { get; set; } = string.Empty;
    public string SystemB { get; set; } = string.Empty;
    public string Equivalence { get; set; } = string.Empty;
    public string NoteZh { get; set; } = string.Empty;
    public string SourceA { get; set; } = string.Empty;
    public string SourceB { get; set; } = string.Empty;
}

public sealed record TerminologySearchResult(PlanningTerm Term, int Score, string MatchKind);
