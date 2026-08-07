using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public interface IPlanningTerminologyService
{
    PlanningTerminologyDataset Dataset { get; }
    bool IsAvailable { get; }
    IReadOnlyList<TerminologySearchResult> Search(string? query, string? jurisdiction = null, string? category = null);
    PlanningTerm? GetTerm(int id);
    IReadOnlyList<(TerminologyRelation Relation, PlanningTerm Term)> GetRelatedTerms(int id);
    IReadOnlyList<HighRiskEquivalence> GetHighRiskEquivalences(int id);
    IReadOnlyList<TerminologySource> GetSources(PlanningTerm term);
}
