using FuzzyPricingMatcher.Tests.Validation;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class ScenarioIdNormalizer
{
    public string Normalize(string? scenarioId)
    {
        var normalized = scenarioId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new CsvValidationException("Scenario_id is required.");
        return normalized;
    }
}