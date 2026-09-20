namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record UpdateBaselineScenarioCommand(string ScenarioId, string QuoteRef, string RawXml, IReadOnlyList<string> NormalizedTags, string RawResponseXml, string BuildId);