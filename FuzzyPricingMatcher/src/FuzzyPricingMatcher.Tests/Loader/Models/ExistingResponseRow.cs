namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record ExistingResponseRow(string ScenarioId, string QuoteRef, string XmlResponse, string BuildId, string? Status);