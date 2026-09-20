namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonScenario(string ScenarioId, string QuoteRef, string SetupError = "")
{
    public string TestName => $"Compare_{Safe(ScenarioId)}_{Safe(QuoteRef)}";
    private static string Safe(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}