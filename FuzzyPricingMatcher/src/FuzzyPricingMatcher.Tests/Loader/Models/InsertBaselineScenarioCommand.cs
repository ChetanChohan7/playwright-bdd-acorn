namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record InsertBaselineScenarioCommand(string ScenarioId, string QuoteRef, string RawXml, IReadOnlyList<string> NormalizedTags, string RawResponseXml, string BuildId);