namespace ClientAutomationFramework.Core.Matching;

public sealed record MatchResult(string ScenarioId, string QuoteRef, bool Passed, string Detail)
{
    public string Status => Passed ? "PASS" : "FAIL";
}
