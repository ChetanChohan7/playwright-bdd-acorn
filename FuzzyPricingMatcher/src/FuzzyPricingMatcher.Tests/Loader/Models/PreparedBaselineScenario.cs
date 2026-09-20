using FuzzyPricingMatcher.Tests.Processing;

namespace FuzzyPricingMatcher.Tests.Models;

public sealed class PreparedBaselineScenario
{
    public required BaselineScenarioCsvRow CsvRow { get; init; }
    public required string NormalizedScenarioId { get; init; }
    public required RequestXmlMetadata RequestMetadata { get; init; }
    public required IReadOnlyList<string> NormalizedTags { get; init; }
    public required string XmlFingerprint { get; init; }
}