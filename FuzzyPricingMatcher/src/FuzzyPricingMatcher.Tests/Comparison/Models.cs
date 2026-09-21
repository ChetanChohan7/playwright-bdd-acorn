using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonExecutionResult(ComparisonResult Result, bool DatabaseUpdated, bool EvidencePrepared);

public sealed record ComparisonResult(string ScenarioId, string QuoteRef, ComparisonOutcome Outcome, bool Passed, decimal? ApiValue, decimal? BaselineValue, decimal? Difference, string Error);

public sealed record ComparisonScenario(string ScenarioId, string QuoteRef, string SetupError = "")
{
    public string TestName => $"Compare_{Safe(ScenarioId)}_{Safe(QuoteRef)}";
    private static string Safe(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}

public sealed record ComparisonScenarioInput(string RequestedTags, string? ApiDate, decimal MinimumThreshold, decimal MaximumThreshold, string BuildId, string BuildNumber);

public sealed record ComparisonScenarioRoute(string SchemeCode, string EndpointName, EndpointSettings Endpoint, RouteSettings Route);

public sealed record ScenarioEvidence(string ScenarioId, string QuoteRef, string RawRequestXml, string RawBaselineXml, string? RawApiXml, string Outcome, string Error);

public sealed record ThresholdResult(bool Passed, decimal Difference);
