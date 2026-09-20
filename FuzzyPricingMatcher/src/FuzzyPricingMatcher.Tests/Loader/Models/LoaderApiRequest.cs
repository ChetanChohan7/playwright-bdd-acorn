namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record LoaderApiRequest(string ScenarioId, string QuoteRef, string RawXml);