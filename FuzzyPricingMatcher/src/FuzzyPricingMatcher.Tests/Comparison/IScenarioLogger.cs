namespace FuzzyPricingMatcher.Tests.Comparison;

public interface IScenarioLogger
{
    void Outcome(ComparisonResult result);
    void QuoteMismatch(string scenarioId, string extractedQuoteRef, string storedQuoteRef);
}