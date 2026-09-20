namespace FuzzyPricingMatcher.Tests.Data;

public sealed record DatabaseUpdateCommand(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, string XmlResponse, string BuildId);