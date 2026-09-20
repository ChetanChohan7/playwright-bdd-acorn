using FuzzyPricingMatcher.Tests.Models;

namespace FuzzyPricingMatcher.Tests.Processing;

public interface IDuplicateScenarioDetector
{
    IReadOnlyList<DuplicateScenarioResult> Detect(IEnumerable<BaselineScenarioCsvRow> records);
}