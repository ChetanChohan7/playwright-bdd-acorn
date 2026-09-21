namespace FuzzyPricingMatcher.Tests.Database;

public sealed record DatabaseResponseRecord(string ScenarioId, string QuoteRef, string XmlResponse, string BuildId, DateTime CreatedDate, DateTime? LastUpdated, string? Status);