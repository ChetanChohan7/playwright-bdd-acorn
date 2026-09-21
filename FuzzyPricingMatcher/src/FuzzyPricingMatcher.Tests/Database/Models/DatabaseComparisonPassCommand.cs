namespace FuzzyPricingMatcher.Tests.Database;

public sealed record DatabaseComparisonPassCommand(string ScenarioId, string XmlResponse, string BuildId);