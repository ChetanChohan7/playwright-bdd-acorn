using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.Helpers.Setup;

/// Shared between IceTestSetup and RadarTestSetup: appsettings.json, an optional
/// appsettings.{environment}.json, appsettings.Development.json when running locally, then (for
/// Development/local runs only) data/import/local-settings.json - the file DataLoader import and
/// seed use - and environment variables (double-underscore translated to the ":" section separator
/// IConfiguration expects) layered on top, highest priority last.
internal static class TestConfigurationLoader
{
    public static PricingProfileConfiguration LoadProfiles(IConfiguration configuration)
    {
        if (configuration.GetSection("RadarSettings:Routes").Exists() ||
            configuration.GetSection("RadarSettings:ResponseXsdMappings").Exists())
        {
            throw new InvalidOperationException("Move RadarSettings routes and schema mappings to PricingProfiles:MappingFile.");
        }
        return PricingProfileConfiguration.Load(PricingProfileConfiguration.ResolveMappingPath(
            configuration["PricingProfiles:MappingFile"], TestContext.CurrentContext.TestDirectory));
    }

    public const string LocalSettingsVariable = "PVF_LOCAL_SETTINGS";

    /// The product filter shared by Radar and ICE: TEST_TAG, then PipelineSettings:TestTag; empty means all.
    /// It matches [Xml].[Request].Product_code exactly, so both stages of a Radar run select the same products.
    public static string ReadTestTag(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("TEST_TAG") ?? configuration["PipelineSettings:TestTag"] ?? string.Empty;

    /// The tag filter shared by Radar and ICE: SCENARIO_TAG, then PipelineSettings:ScenarioTag; empty means all.
    /// One tag, matched against the comma-separated [Xml].[Request].Test_tags.
    public static string ReadScenarioTag(IConfiguration configuration) =>
        ScenarioTags.ValidateFilter(
            Environment.GetEnvironmentVariable("SCENARIO_TAG") ?? configuration["PipelineSettings:ScenarioTag"],
            "SCENARIO_TAG");

    /// PVF_LOCAL_SETTINGS when set, otherwise data/import/local-settings.json in the folder holding
    /// PricingValidationFramework.slnx, found by walking up from the test output folder. Null if absent.
    public static string? FindLocalSettings(string startDirectory)
    {
        var explicitPath = Environment.GetEnvironmentVariable(LocalSettingsVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        for (var folder = new DirectoryInfo(startDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PricingValidationFramework.slnx")))
            {
                var candidate = Path.Combine(folder.FullName, "data", "import", "local-settings.json");
                return File.Exists(candidate) ? candidate : null;
            }
        }

        return null;
    }

    /// <param name="includeLocalSettings">
    /// True only for the live Radar/ICE fixtures, so unit and system tests never depend on a
    /// developer's local-settings.json.
    /// </param>
    public static IConfiguration Load(bool includeLocalSettings = false)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true);

        if (string.Equals(environmentName, "local", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddJsonFile("appsettings.Development.json", optional: true);
        }

        // Local runs share one settings file with DataLoader import/seed. Pipelines set another
        // environment (e.g. Uat), and the file is ignored by Git, so it never applies there.
        if (includeLocalSettings &&
            (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
             environmentName.Equals("local", StringComparison.OrdinalIgnoreCase)))
        {
            var localSettings = FindLocalSettings(TestContext.CurrentContext.TestDirectory);
            if (localSettings is not null)
            {
                builder.AddJsonFile(localSettings, optional: false, reloadOnChange: false);
            }
        }

        return builder
            .AddInMemoryCollection(GetEnvironmentVariables())
            .Build();
    }

    public static Dictionary<string, string?> GetEnvironmentVariables()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            values[key.Replace("__", ":", StringComparison.Ordinal)] = Environment.GetEnvironmentVariable(key);
        }

        return values;
    }
}
