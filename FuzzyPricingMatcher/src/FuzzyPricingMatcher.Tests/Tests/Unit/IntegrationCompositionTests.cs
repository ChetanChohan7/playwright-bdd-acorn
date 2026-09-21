using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Infrastructure;
using FuzzyPricingMatcher.Tests.Loader;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class IntegrationCompositionTests
{
    [Test]
    [Category("Unit")]
    public void Production_composition_exposes_real_loader_and_comparison_services_without_opening_connections()
    {
        using var root = AutomationCompositionRoot.Create(TestContext.CurrentContext.TestDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(root.HasRegistration<LoaderSynchronizationService>(), Is.True);
            Assert.That(root.HasRegistration<ComparisonScenarioExecutor>(), Is.True);
            Assert.That(root.HasRegistration<MatcherConfiguration>(), Is.True);
            Assert.That(root.HasRegistration<IntegrationConfigurationValidator>(), Is.True);
        });
    }

    [Test]
    [Category("Unit")]
    public void Placeholder_configuration_is_rejected_before_database_access()
    {
        using var root = AutomationCompositionRoot.Create(TestContext.CurrentContext.TestDirectory);
        var validator = root.GetRequiredService<IntegrationConfigurationValidator>();
        Assert.That(() => validator.ValidateDatabase(), Throws.TypeOf<ConfigurationValidationException>());
    }
}