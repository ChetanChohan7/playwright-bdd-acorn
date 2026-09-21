using FuzzyPricingMatcher.Tests.Loader;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed record DuplicateScenarioResult(string NormalizedScenarioId, IReadOnlyList<int> RowNumbers, int Occurrences, string ErrorMessage);

public sealed class DuplicateScenarioDetector
{
    private readonly ScenarioIdNormalizer normalizer;

    public DuplicateScenarioDetector(ScenarioIdNormalizer? normalizer = null) => this.normalizer = normalizer ?? new ScenarioIdNormalizer();

    public IReadOnlyList<DuplicateScenarioResult> Detect(IEnumerable<BaselineScenarioCsvRow> records) => records
        .GroupBy(record => normalizer.Normalize(record.ScenarioId), StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => new DuplicateScenarioResult(group.Key, group.Select(record => record.RowNumber).ToArray(), group.Count(), $"Scenario_id '{group.Key}' occurs {group.Count()} times."))
        .ToArray();
}