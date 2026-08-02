namespace UrbanPlanToolbox.Models;

public sealed class RegulationsIndexDocument
{
    public int DataVersion { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string SourceVerifiedDate { get; init; } = string.Empty;
    public string GeneratedAt { get; init; } = string.Empty;
    public List<RegulationEntry> Entries { get; init; } = [];
    public List<OfficialPortal> OfficialPortals { get; init; } = [];
    public List<RegulationsFieldNote> FieldNotes { get; init; } = [];
}

public sealed class RegulationEntry
{
    public int Id { get; init; }
    public string Region { get; init; } = string.Empty;
    public string? JurisdictionLevel { get; init; }
    public string? Topic { get; init; }
    public string? DocumentLevel { get; init; }
    public string OriginalTitle { get; init; } = string.Empty;
    public string? ChineseTitle { get; init; }
    public string? IdentifierOrYear { get; init; }
    public string ScopeAndPurpose { get; init; } = string.Empty;
    public string? EffectOrAdoption { get; init; }
    public string? OfficialUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? DownloadAndCopyrightNote { get; init; }
    public string VerifiedDate { get; init; } = string.Empty;
    public string? SearchKeywords { get; init; }
}

public sealed class OfficialPortal
{
    public string PortalId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string PlatformName { get; init; } = string.Empty;
    public string? Coverage { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? UsageNote { get; init; }
}

public sealed class RegulationsFieldNote
{
    public string Topic { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}
