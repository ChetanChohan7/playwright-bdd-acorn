using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.DataLoader;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class SeedCommandTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"seed-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void Settings_should_use_the_shared_radar_mapping_and_ignore_blank_placeholder_endpoints()
    {
        var settings = Load(Valid());

        Assert.Multiple(() =>
        {
            Assert.That(settings.MappingFile, Is.EqualTo(Path.Combine(TestContext.CurrentContext.TestDirectory, "Radar", "mappings.json")));
            Assert.That(settings.Radar.Routes, Has.Count.EqualTo(16), "routes come from config/radar/mappings.json");
            Assert.That(settings.Radar.Endpoints.Keys, Is.EqualTo(new[] { "Endpoint1" }), "blank Endpoint2 and Endpoint3 are dropped");
            Assert.That(settings.Database.AuditUser, Is.EqualTo("testuser"));
            Assert.That(settings.RateLimit.RequestsPerSecond, Is.EqualTo(2), "defaults apply when the section is absent");
            Assert.That(settings.RequestTime, Is.Null, "no time given means now");
        });
    }

    [Test]
    public void Settings_should_reject_a_partly_filled_endpoint()
    {
        var values = Valid();
        values["RadarSettings:Endpoints:Endpoint2:BaseUrl"] = "https://radar2.example.test";

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("Endpoint2"));
    }

    [Test]
    public void Settings_should_require_a_connection_string()
    {
        var values = Valid();
        values.Remove("DatabaseSettings:ConnectionString");

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("ConnectionString"));
    }

    [Test]
    public void Settings_should_require_at_least_one_endpoint()
    {
        var values = Valid();
        values.Remove("RadarSettings:Endpoints:Endpoint1:BaseUrl");
        values.Remove("RadarSettings:Endpoints:Endpoint1:ApiKeyHeaderName");
        values.Remove("RadarSettings:Endpoints:Endpoint1:ApiKeyValue");

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("At least one Radar logical endpoint"));
    }

    [TestCase("2024-05-01Z10:00:00", "2024-04-01Z08:00:00", "2024-03-01Z07:00:00", "2024-05-01Z10:00:00")]
    [TestCase(null, "2024-04-01Z08:00:00", "2024-03-01Z07:00:00", "2024-04-01Z08:00:00")]
    [TestCase(null, null, "2024-03-01Z07:00:00", "2024-03-01Z07:00:00")]
    public void Request_time_should_prefer_the_option_then_the_radar_variable_then_the_settings_file(
        string? option, string? radarVariable, string? setting, string expected)
    {
        var values = Valid();
        values["RADAR_REQUEST_DATETIME"] = radarVariable;
        values["PipelineSettings:RequestTime"] = setting;

        Assert.That(Load(values, option).RequestTime, Is.EqualTo(expected));
    }

    [TestCase("now", "2024-04-01Z08:00:00")]
    [TestCase(" NOW ", null)]
    public void Now_should_mean_the_current_hour_even_when_a_later_source_has_a_time(string option, string? radarVariable)
    {
        var values = Valid();
        values["RADAR_REQUEST_DATETIME"] = radarVariable;

        Assert.That(Load(values, option).RequestTime, Is.Null);
    }

    [Test]
    public void Settings_should_reject_a_badly_formatted_request_time()
    {
        Assert.That(() => Load(Valid(), "01/05/2024 10:00"), Throws.ArgumentException);
    }

    [Test]
    public void Settings_file_values_should_be_overridden_by_environment_style_keys()
    {
        var file = Path.Combine(directory, "local-settings.json");
        File.WriteAllText(file, """
            { "DatabaseSettings": { "ConnectionString": "Server=from-file;", "AuditUser": "file-user" },
              "RadarSettings": { "Endpoints": { "Endpoint1": { "BaseUrl": "https://file.example.test", "ApiKeyHeaderName": "X-API-KEY", "ApiKeyValue": "k" } } } }
            """);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(file)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseSettings:AuditUser"] = "env-user" })
            .Build();

        var settings = SeedCommand.LoadSettings(configuration, null, TestContext.CurrentContext.TestDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Database.ConnectionString, Is.EqualTo("Server=from-file;"));
            Assert.That(settings.Database.AuditUser, Is.EqualTo("env-user"));
        });
    }

    [Test]
    public async Task Seed_should_exit_1_and_write_a_summary_when_an_option_is_invalid()
    {
        var report = Path.Combine(directory, "seed-summary.json");

        var exitCode = await SeedCommand.RunAsync(["--report", report, "--max-parallel", "four"], CancellationToken.None);

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("Mode").GetString(), Is.EqualTo("seed"));
            Assert.That(json.RootElement.GetProperty("Succeeded").GetBoolean(), Is.False);
            Assert.That(json.RootElement.GetProperty("Issues")[0].GetString(), Does.Contain("FormatException"));
        });
    }

    [Test]
    public async Task Seed_should_exit_1_before_touching_the_database_when_the_settings_file_is_missing()
    {
        var report = Path.Combine(directory, "seed-summary.json");

        var exitCode = await SeedCommand.RunAsync(
            ["--settings", Path.Combine(directory, "missing.json"), "--report", report], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(File.ReadAllText(report), Does.Contain("FileNotFoundException"));
        });
    }

    private static SeedSettings Load(Dictionary<string, string?> values, string? requestTime = null) =>
        SeedCommand.LoadSettings(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            requestTime,
            TestContext.CurrentContext.TestDirectory);

    private static Dictionary<string, string?> Valid() => new()
    {
        ["DatabaseSettings:ConnectionString"] = "Server=localhost;Database=Test;",
        ["DatabaseSettings:AuditUser"] = "testuser",
        ["RadarSettings:Endpoints:Endpoint1:BaseUrl"] = "https://radar.example.test",
        ["RadarSettings:Endpoints:Endpoint1:ApiKeyHeaderName"] = "X-API-KEY",
        ["RadarSettings:Endpoints:Endpoint1:ApiKeyValue"] = "test-key",
        ["RadarSettings:Endpoints:Endpoint2:BaseUrl"] = "",
        ["RadarSettings:Endpoints:Endpoint2:ApiKeyHeaderName"] = "",
        ["RadarSettings:Endpoints:Endpoint2:ApiKeyValue"] = "",
        ["RadarSettings:Endpoints:Endpoint3:BaseUrl"] = ""
    };
}
