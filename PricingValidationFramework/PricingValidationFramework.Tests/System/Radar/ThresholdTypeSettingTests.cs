using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
[NonParallelizable]
public class ThresholdTypeSettingTests
{
    private const string VariableName = "RADAR_THRESHOLD_TYPE";
    private string? originalValue;

    [SetUp]
    public void SetUp()
    {
        originalValue = Environment.GetEnvironmentVariable(VariableName);
        Environment.SetEnvironmentVariable(VariableName, null);
    }

    [TearDown]
    public void TearDown() => Environment.SetEnvironmentVariable(VariableName, originalValue);

    [Test]
    public void Threshold_type_should_default_to_pound()
    {
        Assert.That(RadarTestSetup.ReadThresholdType(Configuration()), Is.EqualTo(ThresholdType.Pound));
    }

    [TestCase("Percentage", ThresholdType.Percentage)]
    [TestCase(" percentage ", ThresholdType.Percentage)]
    [TestCase("POUND", ThresholdType.Pound)]
    public void Threshold_type_should_be_read_from_the_pipeline_variable_in_any_case(string value, ThresholdType expected)
    {
        Environment.SetEnvironmentVariable(VariableName, value);

        Assert.That(RadarTestSetup.ReadThresholdType(Configuration()), Is.EqualTo(expected));
    }

    [Test]
    public void Threshold_type_should_fall_back_to_the_settings_file()
    {
        Assert.That(RadarTestSetup.ReadThresholdType(Configuration("Percentage")), Is.EqualTo(ThresholdType.Percentage));
    }

    [TestCase("Percent")]
    [TestCase("1")]
    [TestCase("£")]
    public void An_unknown_threshold_type_should_stop_the_run(string value)
    {
        Environment.SetEnvironmentVariable(VariableName, value);

        Assert.That(() => RadarTestSetup.ReadThresholdType(Configuration()),
            Throws.InvalidOperationException.With.Message.Contains("Pound or Percentage"));
    }

    private static IConfiguration Configuration(string? setting = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PipelineSettings:ThresholdType"] = setting })
            .Build();
}
