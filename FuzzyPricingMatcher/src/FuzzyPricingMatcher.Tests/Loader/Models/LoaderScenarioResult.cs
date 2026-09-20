namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record LoaderScenarioResult
{
    public required string ScenarioId { get; init; }
    public string QuoteRef { get; init; } = string.Empty;
    public bool Successful { get; init; }
    public required LoaderScenarioOutcome Outcome { get; init; }
    public bool ApiCalled { get; init; }
    public string RequestTableAction { get; init; } = "None";
    public string ResponseTableAction { get; init; } = "None";
    public string Error { get; init; } = string.Empty;
}