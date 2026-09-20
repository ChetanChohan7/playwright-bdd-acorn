using FuzzyPricingMatcher.Tests.Models;

namespace FuzzyPricingMatcher.Tests.Processing;

public interface ICsvScenarioReader
{
    IReadOnlyList<PreparedBaselineScenario> Read(string path, bool requireDataRows = false);
}