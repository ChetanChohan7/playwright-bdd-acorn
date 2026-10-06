using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Common;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class RadarPipelineInputContractTests
{
    [Test]
    public void Pipeline_input_validator_should_require_build_id()
    {
        var settings = new PipelineSettings
        {
            BuildId = string.Empty,
            MinThreshold = 0m,
            MaxThreshold = 100m
        };

        var validator = new PipelineInputValidator();

        Assert.That(() => validator.Validate(settings), Throws.ArgumentException.With.Message.Contains("BuildId"));
    }

    [Test]
    public void Pipeline_input_validator_should_reject_invalid_threshold_range()
    {
        var settings = new PipelineSettings
        {
            BuildId = "build-1",
            MinThreshold = 100m,
            MaxThreshold = 50m
        };

        var validator = new PipelineInputValidator();

        Assert.That(() => validator.Validate(settings), Throws.ArgumentException.With.Message.Contains("MinThreshold"));
    }

    [Test]
    public void Pipeline_input_validator_should_reject_invalid_request_time_format()
    {
        var settings = new PipelineSettings
        {
            BuildId = "build-1",
            MinThreshold = 0m,
            MaxThreshold = 100m,
            RequestTime = "2024-01-01T10:00:00Z"
        };

        var validator = new PipelineInputValidator();

        Assert.That(() => validator.Validate(settings), Throws.ArgumentException.With.Message.Contains("RequestTime"));
    }

    [Test]
    public void Request_time_formatter_should_generate_utc_value_when_missing()
    {
        var requestTime = RequestTimeFormatter.Resolve(null);

            Assert.That(requestTime, Does.Match("\\d{4}-\\d{2}-\\d{2}Z\\d{2}:00:00"));
    }

    [Test]
    public void Request_time_formatter_should_validate_exact_supplied_value()
    {
        var requestTime = RequestTimeFormatter.Resolve("2024-03-01Z09:30:45");

            Assert.That(requestTime, Is.EqualTo("2024-03-01Z09:00:00"));
    }

    [Test]
    public void Radar_test_setup_should_read_runtime_inputs_once_and_validate_them()
    {
        var originalBuildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
        var originalMin = Environment.GetEnvironmentVariable("RADAR_MIN_THRESHOLD");
        var originalMax = Environment.GetEnvironmentVariable("RADAR_MAX_THRESHOLD");
        var originalRequestDate = Environment.GetEnvironmentVariable("RADAR_REQUEST_DATETIME");
        var originalTestTag = Environment.GetEnvironmentVariable("TEST_TAG");
        var schemaVariables = new[]
        {
            "RadarSettings__ResponseXsdMappings__Route001",
            "RadarSettings__ResponseXsdMappings__Route002",
            "RadarSettings__ResponseXsdMappings__Route003"
        };
        var originalSchemas = schemaVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable("BUILD_BUILDID", "build-42");
            Environment.SetEnvironmentVariable("RADAR_MIN_THRESHOLD", "10.50");
            Environment.SetEnvironmentVariable("RADAR_MAX_THRESHOLD", "20.25");
            Environment.SetEnvironmentVariable("RADAR_REQUEST_DATETIME", "2024-03-01Z09:30:45");
            Environment.SetEnvironmentVariable("TEST_TAG", "smoke");
            foreach (var name in schemaVariables)
            {
                Environment.SetEnvironmentVariable(name, "PricingComparisonFixture.xsd");
            }

            using var settings = RadarTestSetup.Create();

            Assert.That(settings.BuildId, Is.EqualTo("build-42"));
            Assert.That(settings.MinThreshold, Is.EqualTo(10.50m));
            Assert.That(settings.MaxThreshold, Is.EqualTo(20.25m));
                Assert.That(settings.RequestTime, Is.EqualTo("2024-03-01Z09:00:00"));
            Assert.That(settings.TestTag, Is.EqualTo("smoke"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_BUILDID", originalBuildId);
            Environment.SetEnvironmentVariable("RADAR_MIN_THRESHOLD", originalMin);
            Environment.SetEnvironmentVariable("RADAR_MAX_THRESHOLD", originalMax);
            Environment.SetEnvironmentVariable("RADAR_REQUEST_DATETIME", originalRequestDate);
            Environment.SetEnvironmentVariable("TEST_TAG", originalTestTag);
            foreach (var originalSchema in originalSchemas)
            {
                Environment.SetEnvironmentVariable(originalSchema.Key, originalSchema.Value);
            }
        }
    }

    [Test]
    public void Radar_test_setup_should_require_runtime_thresholds_when_settings_are_blank()
    {
        var variableNames = new[]
        {
            "BUILD_BUILDID", "ASPNETCORE_ENVIRONMENT", "RADAR_MIN_THRESHOLD", "RADAR_MAX_THRESHOLD",
            "RADAR_REQUEST_DATETIME", "TEST_TAG", "PipelineSettings__MinThreshold", "PipelineSettings__MaxThreshold"
        };
        var originalValues = variableNames.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable("BUILD_BUILDID", "build-42");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            foreach (var name in variableNames.Skip(2))
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            var exception = Assert.Throws<InvalidOperationException>(() => RadarTestSetup.Create());
            Assert.That(exception!.Message, Does.Contain("RADAR_MIN_THRESHOLD is required"));
        }
        finally
        {
            foreach (var (name, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Test]
    public void Radar_test_setup_should_accept_pipeline_settings_environment_overrides()
    {
        var variableNames = new[]
        {
            "BUILD_BUILDID", "RADAR_MIN_THRESHOLD", "RADAR_MAX_THRESHOLD", "RADAR_REQUEST_DATETIME", "TEST_TAG",
            "PipelineSettings__MinThreshold", "PipelineSettings__MaxThreshold",
            "PipelineSettings__RequestTime", "PipelineSettings__TestTag",
            "RadarSettings__ResponseXsdMappings__Route001",
            "RadarSettings__ResponseXsdMappings__Route002",
            "RadarSettings__ResponseXsdMappings__Route003"
        };
        var originalValues = variableNames.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable("BUILD_BUILDID", "build-42");
            foreach (var name in variableNames.Skip(1).Take(4))
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            Environment.SetEnvironmentVariable("PipelineSettings__MinThreshold", "-10.5");
            Environment.SetEnvironmentVariable("PipelineSettings__MaxThreshold", "20.25");
            Environment.SetEnvironmentVariable("PipelineSettings__RequestTime", "2024-03-01Z09:30:45");
            Environment.SetEnvironmentVariable("PipelineSettings__TestTag", "smoke");
            foreach (var name in variableNames.Skip(9))
            {
                Environment.SetEnvironmentVariable(name, "PricingComparisonFixture.xsd");
            }

            using var settings = RadarTestSetup.Create();

            Assert.Multiple(() =>
            {
                Assert.That(settings.MinThreshold, Is.EqualTo(-10.5m));
                Assert.That(settings.MaxThreshold, Is.EqualTo(20.25m));
                    Assert.That(settings.RequestTime, Is.EqualTo("2024-03-01Z09:00:00"));
                Assert.That(settings.TestTag, Is.EqualTo("smoke"));
            });
        }
        finally
        {
            foreach (var (name, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}