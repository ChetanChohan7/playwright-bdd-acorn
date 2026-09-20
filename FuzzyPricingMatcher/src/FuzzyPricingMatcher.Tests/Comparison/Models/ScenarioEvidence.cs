namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed record ScenarioEvidence(string ScenarioId, string QuoteRef, string RawRequestXml, string RawBaselineXml, string? RawApiXml, string Outcome, string Error);