namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record ExistingRequestRow(string ScenarioId, string QuoteRef, string XmlRequest, IReadOnlyList<string> NormalizedTags);