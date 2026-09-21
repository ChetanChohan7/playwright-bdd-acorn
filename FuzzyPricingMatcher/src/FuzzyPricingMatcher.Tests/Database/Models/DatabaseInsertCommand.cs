namespace FuzzyPricingMatcher.Tests.Database;

public sealed record DatabaseInsertCommand(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, string XmlResponse, string BuildId);