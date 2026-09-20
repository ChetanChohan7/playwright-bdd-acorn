namespace FuzzyPricingMatcher.Tests.Models;

public sealed class BaselineScenarioCsvRow
{
    public int RowNumber { get; init; }
    public string ScenarioId { get; init; } = string.Empty;
    public string XmlRequest { get; init; } = string.Empty;
    public string TestTags { get; init; } = string.Empty;
}