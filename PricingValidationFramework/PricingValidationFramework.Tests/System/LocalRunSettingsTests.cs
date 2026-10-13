using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
[NonParallelizable]
public class LocalRunSettingsTests
{
    private string directory = string.Empty;
    private string? originalLocalSettings;
    private string? originalScenarioTag;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"local-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        originalLocalSettings = Environment.GetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable);
        originalScenarioTag = Environment.GetEnvironmentVariable("SCENARIO_TAG");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, null);
        Environment.SetEnvironmentVariable("SCENARIO_TAG", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, originalLocalSettings);
        Environment.SetEnvironmentVariable("SCENARIO_TAG", originalScenarioTag);
        Directory.Delete(directory, true);
    }

    [TestCase(null, "")]
    [TestCase(" smoke ; regression,Smoke ", "smoke,regression")]
    [TestCase("a_b,c-d,e.f", "a_b,c-d,e.f")]
    public void Tags_should_be_normalised_to_a_comma_separated_list(string? tags, string expected)
    {
        Assert.That(ScenarioTags.Normalize(tags), Is.EqualTo(expected));
    }

    [TestCase("smoke test")]
    [TestCase("smoke|regression")]
    [TestCase("50%")]
    public void Invalid_tags_should_be_rejected(string tags)
    {
        Assert.That(() => ScenarioTags.Normalize(tags), Throws.InstanceOf<InvalidDataException>());
    }

    [TestCase(null, "")]
    [TestCase(" smoke ", "smoke")]
    public void Scenario_tag_filter_should_accept_one_tag_or_nothing(string? value, string expected)
    {
        Environment.SetEnvironmentVariable("SCENARIO_TAG", value);

        Assert.That(TestConfigurationLoader.ReadScenarioTag(new ConfigurationBuilder().Build()), Is.EqualTo(expected));
    }

    [TestCase("smoke,regression")]
    [TestCase("smo%ke")]
    public void Scenario_tag_filter_should_reject_more_than_one_tag_or_bad_characters(string value)
    {
        Environment.SetEnvironmentVariable("SCENARIO_TAG", value);

        Assert.That(() => TestConfigurationLoader.ReadScenarioTag(new ConfigurationBuilder().Build()),
            Throws.InvalidOperationException.With.Message.Contains("SCENARIO_TAG"));
    }

    [TestCase("now")]
    [TestCase(" NOW ")]
    [TestCase("")]
    public void Request_time_now_should_mean_the_current_utc_hour(string value)
    {
        var expected = RequestTimeFormatter.FormatUtcNow();

        Assert.That(RequestTimeFormatter.Resolve(value), Is.EqualTo(expected).Or.EqualTo(RequestTimeFormatter.FormatUtcNow()));
    }

    [Test]
    public void Blank_endpoints_should_be_dropped_and_partly_filled_ones_kept_for_validation()
    {
        var settings = new RadarSettings
        {
            Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
            {
                ["Endpoint1"] = new() { BaseUrl = "https://radar.example.test", ApiKeyHeaderName = "X", ApiKeyValue = "k" },
                ["Endpoint2"] = new() { BaseUrl = "", ApiKeyHeaderName = " ", ApiKeyValue = "" },
                ["Endpoint3"] = new() { BaseUrl = "https://radar3.example.test" }
            }
        };

        RadarSettingsValidator.RemoveBlankEndpoints(settings);

        Assert.That(settings.Endpoints.Keys, Is.EquivalentTo(new[] { "Endpoint1", "Endpoint3" }));
    }

    [Test]
    public void Local_settings_should_be_found_beside_the_solution_from_a_build_output_folder()
    {
        var output = Directory.CreateDirectory(Path.Combine(directory, "PricingValidationFramework.Tests", "bin", "Debug", "net10.0")).FullName;
        File.WriteAllText(Path.Combine(directory, "PricingValidationFramework.slnx"), "<Solution />");
        Directory.CreateDirectory(Path.Combine(directory, "data", "import"));

        Assert.That(TestConfigurationLoader.FindLocalSettings(output), Is.Null, "no file yet");

        var settings = Path.Combine(directory, "data", "import", "local-settings.json");
        File.WriteAllText(settings, "{}");
        Assert.That(TestConfigurationLoader.FindLocalSettings(output), Is.EqualTo(settings));
    }

    [Test]
    public void Local_settings_path_can_be_given_explicitly()
    {
        var settings = Path.Combine(directory, "mine.json");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, settings);

        Assert.That(TestConfigurationLoader.FindLocalSettings("/anywhere"), Is.EqualTo(settings));
    }

    [Test]
    public void Local_settings_should_only_be_loaded_when_a_live_fixture_asks_for_them()
    {
        var settings = Path.Combine(directory, "local-settings.json");
        File.WriteAllText(settings, """{ "PipelineSettings": { "MinThreshold": "-7" } }""");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, settings);

        Assert.Multiple(() =>
        {
            Assert.That(TestConfigurationLoader.Load()["PipelineSettings:MinThreshold"], Is.Not.EqualTo("-7"), "unit and system tests stay isolated");
            Assert.That(TestConfigurationLoader.Load(includeLocalSettings: true)["PipelineSettings:MinThreshold"], Is.EqualTo("-7"));
        });
    }
}
