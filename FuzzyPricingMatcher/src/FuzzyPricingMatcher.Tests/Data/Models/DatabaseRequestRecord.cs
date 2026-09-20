namespace FuzzyPricingMatcher.Tests.Data;

public sealed record DatabaseRequestRecord(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, DateTime CreatedDate);