using System.Text.Json;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class PlanningTerminologyService : IPlanningTerminologyService
{
    public const string DataFileName = "PlanningTerminology.v1.0.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly IReadOnlyDictionary<int, PlanningTerm> _terms;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<TerminologyAlias>> _aliases;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<TerminologyRelation>> _relations;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<HighRiskEquivalence>> _highRisk;

    public static PlanningTerminologyService Default { get; } = CreateDefault();
    public bool IsAvailable { get; }

    public PlanningTerminologyDataset Dataset { get; }

    public PlanningTerminologyService(PlanningTerminologyDataset dataset)
        : this(dataset, true)
    {
    }

    private PlanningTerminologyService(PlanningTerminologyDataset dataset, bool isAvailable)
    {
        Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        IsAvailable = isAvailable;
        Validate(dataset);
        _terms = dataset.Terms.ToDictionary(term => term.Id);
        _aliases = dataset.Aliases.GroupBy(alias => alias.TermId).ToDictionary(group => group.Key, group => (IReadOnlyList<TerminologyAlias>)group.ToArray());
        _relations = dataset.Relations.GroupBy(relation => relation.SourceId).ToDictionary(group => group.Key, group => (IReadOnlyList<TerminologyRelation>)group.ToArray());
        _highRisk = dataset.HighRiskEquivalences.SelectMany(item => new[] { (Name: item.TermA, Item: item), (Name: item.TermB, Item: item) }).Select(pair => (Id: _terms.Values.FirstOrDefault(term => term.ZhCN == pair.Name || term.JaJP == pair.Name)?.Id ?? -1, pair.Item)).Where(pair => pair.Id >= 0).GroupBy(pair => pair.Id).ToDictionary(group => group.Key, group => (IReadOnlyList<HighRiskEquivalence>)group.Select(pair => pair.Item).DistinctBy(item => item.Id).ToArray());
        AppLogger.Default.Info(nameof(PlanningTerminologyService), "dataset_load_success");
    }

    private static PlanningTerminologyService CreateDefault()
    {
        try { return LoadPackaged(); }
        catch (Exception ex)
        {
            AppLogger.Default.Error(nameof(PlanningTerminologyService), "dataset_validation_failed", ex);
            return new PlanningTerminologyService(new PlanningTerminologyDataset { SchemaVersion = 1, DataVersion = "1.0.0", EquivalenceEnum = ["exact", "approximate", "translation-only", "none"] }, false);
        }
    }

    public static PlanningTerminologyService LoadPackaged()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Data", "PlanningTerminology", DataFileName);
            return new(JsonSerializer.Deserialize<PlanningTerminologyDataset>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Terminology JSON is empty."));
        }
        catch (Exception ex)
        {
            AppLogger.Default.Error(nameof(PlanningTerminologyService), "dataset_load_failed", ex);
            throw new InvalidDataException("Planning terminology data could not be loaded.", ex);
        }
    }

    public static PlanningTerminologyDataset Deserialize(string json) => JsonSerializer.Deserialize<PlanningTerminologyDataset>(json, JsonOptions) ?? throw new InvalidDataException("Terminology JSON is empty.");

    public IReadOnlyList<TerminologySearchResult> Search(string? query, string? jurisdiction = null, string? category = null)
    {
        var q = Normalize(query);
        return _terms.Values.Where(term => (string.IsNullOrWhiteSpace(jurisdiction) || jurisdiction switch { "通用" => term.Jurisdiction.StartsWith("通用", StringComparison.Ordinal), "中国" => term.Jurisdiction.StartsWith("中国", StringComparison.Ordinal), "日本" => term.Jurisdiction.StartsWith("日本", StringComparison.Ordinal), _ => term.Jurisdiction == jurisdiction }) && (string.IsNullOrWhiteSpace(category) || term.Category == category))
            .Select(term => Score(term, q)).Where(result => result is not null).Select(result => result!).OrderByDescending(result => result.Score).ThenBy(result => result.Term.Id).ToArray();
    }

    public PlanningTerm? GetTerm(int id) => _terms.TryGetValue(id, out var term) ? term : null;

    public IReadOnlyList<(TerminologyRelation Relation, PlanningTerm Term)> GetRelatedTerms(int id) =>
        Dataset.Relations.Where(relation => relation.SourceId == id || relation.TargetId == id).Select(relation => (relation, GetTerm(relation.SourceId == id ? relation.TargetId : relation.SourceId))).Where(item => item.Item2 is not null).Select(item => (item.relation, item.Item2!)).ToArray();

    public IReadOnlyList<HighRiskEquivalence> GetHighRiskEquivalences(int id) => _highRisk.TryGetValue(id, out var values) ? values : [];

    public IReadOnlyList<TerminologySource> GetSources(PlanningTerm term) => term.SourceIds.Where(Dataset.Sources.ContainsKey).Select(id => Dataset.Sources[id]).ToArray();

    public static void Validate(PlanningTerminologyDataset data)
    {
        if (data.SchemaVersion != 1 || data.DataVersion != "1.0.0") throw new InvalidDataException("Terminology schema or data version is invalid.");
        if (data.Counts.Terms != data.Terms.Count || data.Counts.Aliases != data.Aliases.Count || data.Counts.Relations != data.Relations.Count || data.Counts.HighRisk != data.HighRiskEquivalences.Count || data.Counts.Sources != data.Sources.Count) throw new InvalidDataException("Terminology counts do not match the data.");
        var ids = data.Terms.Select(term => term.Id).ToHashSet();
        if (ids.Count != data.Terms.Count || data.Aliases.Any(alias => !ids.Contains(alias.TermId)) || data.Relations.Any(relation => !ids.Contains(relation.SourceId) || !ids.Contains(relation.TargetId))) throw new InvalidDataException("Terminology contains a dangling or duplicate term reference.");
        if (data.Aliases.GroupBy(alias => $"{alias.TermId}:{alias.Alias}:{alias.Type}", StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new InvalidDataException("Terminology contains duplicate aliases.");
        if (data.Relations.Select(relation => relation.Id).Distinct(StringComparer.Ordinal).Count() != data.Relations.Count || data.Sources.Keys.Distinct(StringComparer.Ordinal).Count() != data.Sources.Count) throw new InvalidDataException("Terminology contains duplicate stable IDs.");
        var allowed = new HashSet<string>(["exact", "approximate", "translation-only", "none"], StringComparer.Ordinal);
        if (data.EquivalenceEnum.Any(value => !allowed.Contains(value)) || data.Terms.Any(term => !allowed.Contains(term.Equivalence))) throw new InvalidDataException("Terminology contains an invalid equivalence value.");
        if (data.Terms.SelectMany(term => term.SourceIds).Any(id => !data.Sources.ContainsKey(id)) || data.Relations.SelectMany(relation => relation.SourceIds).Any(id => !data.Sources.ContainsKey(id)) || data.HighRiskEquivalences.SelectMany(item => new[] { item.SourceA, item.SourceB }).Any(id => !data.Sources.ContainsKey(id))) throw new InvalidDataException("Terminology contains a dangling source reference.");
    }

    private TerminologySearchResult? Score(PlanningTerm term, string query)
    {
        if (query.Length == 0) return new(term, 0, "all");
        var aliases = _aliases.GetValueOrDefault(term.Id, []);
        var candidates = new List<(int score, string kind)>();
        Add(term.ZhCN, 1000, "primary-exact"); Add(term.JaJP, 1000, "primary-exact"); Add(term.EnUS, 1000, "primary-exact");
        foreach (var alias in aliases) Add(alias.Alias, 500 + Math.Clamp(alias.Weight, 0, 100) * 5, alias.Type);
        foreach (var (value, baseScore, kind) in new[] { (term.ZhCN, 850, "primary-prefix"), (term.JaJP, 850, "primary-prefix"), (term.EnUS, 850, "primary-prefix") }) if (Normalize(value).StartsWith(query, StringComparison.Ordinal)) candidates.Add((baseScore, kind));
        foreach (var (value, baseScore, kind) in new[] { (term.ZhCN, 700, "primary-contains"), (term.JaJP, 700, "primary-contains"), (term.EnUS, 700, "primary-contains"), (term.JaReading, 300, "reading") }) if (Normalize(value).Contains(query, StringComparison.Ordinal)) candidates.Add((baseScore, kind));
        return candidates.Count == 0 ? null : candidates.OrderByDescending(item => item.score).First() is var best ? new(term, best.score, best.kind) : null;

        void Add(string? value, int score, string kind) { if (Normalize(value) == query) candidates.Add((score, kind)); else if (Normalize(value).StartsWith(query, StringComparison.Ordinal)) candidates.Add((score - 50, kind + "-prefix")); else if (Normalize(value).Contains(query, StringComparison.Ordinal)) candidates.Add((score - 100, kind + "-contains")); }
    }

    internal static string Normalize(string? value) => string.Join(" ", (value ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace('－', '-').Replace('–', '-').Replace('—', '-').ToUpperInvariant();
}
