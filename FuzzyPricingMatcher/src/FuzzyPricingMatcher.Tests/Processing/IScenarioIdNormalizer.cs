namespace FuzzyPricingMatcher.Tests.Processing;

public interface IScenarioIdNormalizer
{
    string Normalize(string? scenarioId);
}