using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Models.Tools;

namespace UrbanPlanToolbox.Services;

public sealed class DesignConceptDictionaryService
{
    public const int DesignConceptDictionarySchemaVersion = 1;
    public const string DataFileName = "concepts.json";
    private readonly JsonDataStorage _storage;

    public DesignConceptDictionaryService(IAppDataPathProvider paths, IStorageDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _storage = new JsonDataStorage(paths, DesignConceptDictionarySchemaVersion, diagnostics: diagnostics);
    }

    public async Task<DataReadResult<DesignConceptDictionaryDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _storage.ReadAsync<DesignConceptDictionaryDocument>(ToolIds.DesignConceptDictionary, DataFileName, cancellationToken);
        if (result.Status == DataStorageStatus.NotFound)
            return new(DataStorageStatus.Success, new DesignConceptDictionaryDocument(), DesignConceptDictionarySchemaVersion);
        if (result.HasValue && !TryValidateDocument(result.Value!, out _))
            return new(DataStorageStatus.Corrupt, null, result.SchemaVersion, "DesignConceptDictionaryInvalid");
        return result;
    }

    public Task<DataWriteResult> SaveAsync(DesignConceptDictionaryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var copy = CloneDocument(document);
        NormalizeDocument(copy);
        return !TryValidateDocument(copy, out var error)
            ? Task.FromResult(new DataWriteResult(DataStorageStatus.IoFailure, error))
            : _storage.SaveAsync(ToolIds.DesignConceptDictionary, DataFileName, copy, cancellationToken);
    }

    public static DesignConceptDictionaryDocument CloneDocument(DesignConceptDictionaryDocument source) => new()
    {
        Concepts = source.Concepts.Select(Clone).ToList()
    };

    public static DesignConcept Clone(DesignConcept source) => new()
    {
        ConceptId = source.ConceptId,
        Name = source.Name,
        Definition = source.Definition,
        ApplicableProjectTypes = [.. source.ApplicableProjectTypes],
        Tags = [.. source.Tags],
        SourceOrReference = source.SourceOrReference,
        Notes = source.Notes,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    public static DesignConceptEditSnapshot CreateEditSnapshot(DesignConcept concept) => new(
        NormalizeText(concept.Name),
        NormalizeText(concept.Definition),
        NormalizeList(concept.ApplicableProjectTypes),
        NormalizeList(concept.Tags),
        NormalizeText(concept.SourceOrReference),
        NormalizeText(concept.Notes));

    public static bool HasBusinessChanges(DesignConceptEditSnapshot baseline, DesignConceptEditSnapshot current) =>
        !Equals(baseline, current);

    public static bool TryBuildConcept(
        DesignConceptDraft draft,
        Guid conceptId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        out DesignConcept concept,
        out string? error)
    {
        concept = new();
        error = null;
        if (conceptId == Guid.Empty) { error = "ConceptIdInvalid"; return false; }
        if (createdAt.Offset != TimeSpan.Zero || updatedAt.Offset != TimeSpan.Zero || updatedAt < createdAt) { error = "ConceptTimestampInvalid"; return false; }
        var name = NormalizeText(draft.Name);
        var definition = NormalizeText(draft.Definition);
        if (name.Length == 0) { error = "ConceptNameRequired"; return false; }
        if (definition.Length == 0) { error = "ConceptDefinitionRequired"; return false; }
        concept = new DesignConcept
        {
            ConceptId = conceptId,
            Name = name,
            Definition = definition,
            ApplicableProjectTypes = NormalizeList(draft.ApplicableProjectTypes).ToList(),
            Tags = NormalizeList(draft.Tags).ToList(),
            SourceOrReference = NullIfEmpty(draft.SourceOrReference),
            Notes = NullIfEmpty(draft.Notes),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        return true;
    }

    public static DesignConcept CreateCopy(DesignConcept source, string copyLabel, DateTimeOffset now)
    {
        var copy = Clone(source);
        copy.ConceptId = Guid.NewGuid();
        copy.Name = $"{source.Name.Trim()} {copyLabel.Trim()}".Trim();
        copy.CreatedAt = now;
        copy.UpdatedAt = now;
        copy.ApplicableProjectTypes = [.. source.ApplicableProjectTypes];
        copy.Tags = [.. source.Tags];
        return copy;
    }

    public static IReadOnlyList<DesignConcept> Search(
        DesignConceptDictionaryDocument document,
        string? query,
        string? projectType,
        string? tag,
        DesignConceptSort sort)
    {
        var normalizedQuery = NormalizeText(query);
        var normalizedProjectType = NormalizeText(projectType);
        var normalizedTag = NormalizeText(tag);
        IEnumerable<DesignConcept> result = document.Concepts;
        if (normalizedQuery.Length > 0)
        {
            result = result.Where(concept => string.Join('\n', concept.Name, concept.Definition, string.Join(' ', concept.ApplicableProjectTypes), string.Join(' ', concept.Tags), concept.SourceOrReference, concept.Notes).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }
        if (normalizedProjectType.Length > 0) result = result.Where(concept => NormalizeList(concept.ApplicableProjectTypes).Contains(normalizedProjectType, StringComparer.OrdinalIgnoreCase));
        if (normalizedTag.Length > 0) result = result.Where(concept => NormalizeList(concept.Tags).Contains(normalizedTag, StringComparer.OrdinalIgnoreCase));
        return sort switch
        {
            DesignConceptSort.Created => result.OrderByDescending(concept => concept.CreatedAt).ThenBy(concept => concept.Name, StringComparer.OrdinalIgnoreCase).ThenBy(concept => concept.ConceptId).ToArray(),
            DesignConceptSort.Name => result.OrderBy(concept => concept.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(concept => concept.UpdatedAt).ThenBy(concept => concept.ConceptId).ToArray(),
            _ => result.OrderByDescending(concept => concept.UpdatedAt).ThenBy(concept => concept.Name, StringComparer.OrdinalIgnoreCase).ThenBy(concept => concept.ConceptId).ToArray()
        };
    }

    public static IReadOnlyList<string> GetProjectTypes(DesignConceptDictionaryDocument document) =>
        document.Concepts.SelectMany(concept => NormalizeList(concept.ApplicableProjectTypes)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> GetTags(DesignConceptDictionaryDocument document) =>
        document.Concepts.SelectMany(concept => NormalizeList(concept.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public static void NormalizeDocument(DesignConceptDictionaryDocument document)
    {
        foreach (var concept in document.Concepts)
        {
            concept.Name = NormalizeText(concept.Name);
            concept.Definition = NormalizeText(concept.Definition);
            concept.ApplicableProjectTypes = NormalizeList(concept.ApplicableProjectTypes).ToList();
            concept.Tags = NormalizeList(concept.Tags).ToList();
            concept.SourceOrReference = NullIfEmpty(concept.SourceOrReference);
            concept.Notes = NullIfEmpty(concept.Notes);
        }
    }

    public static bool TryValidateDocument(DesignConceptDictionaryDocument document, out string? error)
    {
        error = null;
        var ids = new HashSet<Guid>();
        foreach (var concept in document.Concepts)
        {
            if (concept.ConceptId == Guid.Empty || !ids.Add(concept.ConceptId) || string.IsNullOrWhiteSpace(concept.Name) || string.IsNullOrWhiteSpace(concept.Definition)) { error = "DesignConceptInvalid"; return false; }
            if (concept.CreatedAt.Offset != TimeSpan.Zero || concept.UpdatedAt.Offset != TimeSpan.Zero || concept.UpdatedAt < concept.CreatedAt) { error = "ConceptTimestampInvalid"; return false; }
            if (concept.ApplicableProjectTypes is null || concept.Tags is null || concept.ApplicableProjectTypes.Any(string.IsNullOrWhiteSpace) || concept.Tags.Any(string.IsNullOrWhiteSpace)) { error = "ConceptListInvalid"; return false; }
        }
        return true;
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values) =>
        (values ?? []).Select(NormalizeText).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
