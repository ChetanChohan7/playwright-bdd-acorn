using FuzzyPricingMatcher.Tests.Database;
using FuzzyPricingMatcher.Tests.Configuration;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed class DatabaseScenarioSource
{
    private readonly IFuzzyMatcherRepository repository;

    public DatabaseScenarioSource(IFuzzyMatcherRepository repository) => this.repository = repository;

    public IEnumerable<TestCaseData> GetTestCases(string? requestedTags, TagMatchMode matchMode = TagMatchMode.Any)
    {
        IReadOnlyList<DatabaseScenarioSelection> scenarios;
        try { scenarios = repository.SelectScenariosAsync(requestedTags, matchMode).GetAwaiter().GetResult(); }
        catch (Exception exception) { throw new InvalidOperationException($"Database scenario discovery failed for requested tags '{requestedTags ?? string.Empty}'.", exception); }
        return scenarios.Select(scenario => new TestCaseData(new ComparisonScenario(scenario.ScenarioId, scenario.QuoteRef)).SetName(new ComparisonScenario(scenario.ScenarioId, scenario.QuoteRef).TestName));
    }
}