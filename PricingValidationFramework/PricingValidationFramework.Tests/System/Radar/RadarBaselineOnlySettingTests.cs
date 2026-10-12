using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
[NonParallelizable]
public class RadarBaselineOnlySettingTests
{
    private const string VariableName = "RADAR_BASELINE_ONLY_TEST";

    [TearDown]
    public void TearDown() => Environment.SetEnvironmentVariable(VariableName, null);

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("true", true)]
    [TestCase(" TRUE ", true)]
    [TestCase("1", true)]
    [TestCase("false", false)]
    [TestCase("0", false)]
    public void The_baseline_only_flag_should_read_true_and_false_values(string? value, bool expected)
    {
        Environment.SetEnvironmentVariable(VariableName, value);

        Assert.That(RadarTestSetup.ReadFlag(EmptyConfiguration(), VariableName, "BaselineOnly"), Is.EqualTo(expected));
    }

    [Test]
    public void The_baseline_only_flag_should_fall_back_to_settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PipelineSettings:BaselineOnly"] = "true" })
            .Build();

        Assert.That(RadarTestSetup.ReadFlag(configuration, VariableName, "BaselineOnly"), Is.True);
    }

    [Test]
    public void An_unrecognised_baseline_only_value_should_stop_setup()
    {
        Environment.SetEnvironmentVariable(VariableName, "maybe");

        Assert.Throws<InvalidOperationException>(() => RadarTestSetup.ReadFlag(EmptyConfiguration(), VariableName, "BaselineOnly"));
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();
}
