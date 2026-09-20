namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record DeleteObsoleteScenarioCommand(IReadOnlyList<string> ScenarioIds);