namespace ClientAutomationFramework.Core.Matching;

/// Confirms a value read from a JSON response is exactly equal to the value already stored for
/// the scenario - no tolerance, unlike XmlToleranceMatcher.
public sealed class JsonExactMatcher
{
    public MatchResult Evaluate(string scenarioId, string quoteRef, decimal? actualAmount, decimal? expectedAmount)
    {
        if (actualAmount is null)
            return new MatchResult(scenarioId, quoteRef, false, "Response did not contain a readable amount.");
        if (expectedAmount is null)
            return new MatchResult(scenarioId, quoteRef, false, "No stored amount to compare against.");

        var matches = actualAmount.Value == expectedAmount.Value;
        return new MatchResult(scenarioId, quoteRef, matches, $"Stored={expectedAmount}, Actual={actualAmount}.");
    }
}
