namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ComparisonScenarioInput(string RequestedTags, string? ApiDate, decimal MinimumThreshold, decimal MaximumThreshold, string BuildId, string BuildNumber);