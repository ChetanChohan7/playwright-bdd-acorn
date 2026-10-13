using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceDiscoveryFilterTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public async Task Ice_should_select_every_product_when_no_test_tag_is_set(string? testTag)
    {
        var reader = new RecordingReader();

        await IceTestSetup.DiscoverScenariosAsync(reader, testTag ?? string.Empty);

        Assert.That(reader.Calls, Is.EqualTo(new[] { "all" }));
    }

    [Test]
    public async Task Ice_should_select_only_the_test_tag_product_like_radar()
    {
        var reader = new RecordingReader();

        var scenarios = await IceTestSetup.DiscoverScenariosAsync(reader, "Carrot");

        Assert.Multiple(() =>
        {
            Assert.That(reader.Calls, Is.EqualTo(new[] { "product:Carrot" }));
            Assert.That(scenarios.Single().ProductCode, Is.EqualTo("Carrot"));
        });
    }

    private sealed class RecordingReader : IBaselineDataReader
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("all");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([]);
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosByProductCodeAsync(
            string productCode,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"product:{productCode}");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([new IceBaselineScenario { ScenarioId = "S1", ProductCode = productCode }]);
        }

        public Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScenarioResponse?>(null);
    }
}
