using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Data;
using FuzzyPricingMatcher.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Integration.Comparison;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
[Category("Compare")]
public sealed class ComparisonTests
{
    private static AutomationCompositionRoot? compositionRoot;

    public static IEnumerable<TestCaseData> Scenarios => BuildScenarios();

    private static IReadOnlyList<TestCaseData> BuildScenarios()
    {
        if (!IntegrationTestExecution.IsEnabled)
            return new[] { new TestCaseData(new ComparisonScenario("__SETUP__", "__SETUP__", IntegrationTestExecution.DisabledMessage)).SetName("Compare_IntegrationConfigurationRequired") };
        try
        {
            compositionRoot ??= AutomationCompositionRoot.Create(TestContext.CurrentContext.TestDirectory);
            var configuration = compositionRoot.GetRequiredService<MatcherConfiguration>();
            var validator = compositionRoot.GetRequiredService<IntegrationConfigurationValidator>();
            validator.ValidateEnabledRoutes();
            var repository = compositionRoot.GetRequiredService<IFuzzyMatcherRepository>();
            var scenarioSelections = repository.SelectScenariosAsync(configuration.Pipeline.RequestedTestTags, configuration.Automation.TagMatchMode).GetAwaiter().GetResult();
            validator.ValidateComparison(scenarioSelections.Select(selection => selection.ScenarioId));
            return scenarioSelections.Select(selection => new TestCaseData(new ComparisonScenario(selection.ScenarioId, selection.QuoteRef)).SetName(new ComparisonScenario(selection.ScenarioId, selection.QuoteRef).TestName)).ToArray();
        }
        catch (Exception exception)
        {
            return new[] { new TestCaseData(new ComparisonScenario("__SETUP__", "__SETUP__", exception.Message)).SetName("Compare_IntegrationConfigurationFailure") };
        }
    }

    [Test]
    [TestCaseSource(nameof(Scenarios))]
    public void Compare_selected_scenario_and_assert_after_persistence(ComparisonScenario scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.SetupError))
            Assert.Fail($"Comparison integration is not configured: {scenario.SetupError}");

        compositionRoot ??= AutomationCompositionRoot.Create(TestContext.CurrentContext.TestDirectory);
        var configuration = compositionRoot.GetRequiredService<MatcherConfiguration>();
        var comparisonInput = new ComparisonScenarioInput(configuration.Pipeline.RequestedTestTags, configuration.Pipeline.ApiDate, configuration.Automation.MinimumThreshold, configuration.Automation.MaximumThreshold, configuration.Pipeline.BuildId, configuration.Pipeline.BuildNumber);
        using var scope = compositionRoot.CreateScope();
        var execution = scope.ServiceProvider.GetRequiredService<IComparisonScenarioExecutor>().Execute(scenario, comparisonInput);
        scope.ServiceProvider.GetRequiredService<NUnitScenarioOutputWriter>().Write(execution.Result, comparisonInput.BuildId, string.Empty, string.Empty, comparisonInput.MinimumThreshold, comparisonInput.MaximumThreshold, null);
        Assert.That(execution.DatabaseUpdated, Is.True, execution.Result.Error);
        Assert.That(execution.Result.Passed, Is.True, execution.Result.Error);
    }

    [OneTimeTearDown]
    public void DisposeCompositionRoot() => compositionRoot?.Dispose();
}
