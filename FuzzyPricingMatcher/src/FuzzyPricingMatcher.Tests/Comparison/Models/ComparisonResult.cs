namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonResult(string ScenarioId, string QuoteRef, ComparisonOutcome Outcome, bool Passed, decimal? ApiValue, decimal? BaselineValue, decimal? Difference, string Error);