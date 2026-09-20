namespace FuzzyPricingMatcher.Tests.Data;

public sealed record DatabaseComparisonPassCommand(string ScenarioId, string XmlResponse, string BuildId);