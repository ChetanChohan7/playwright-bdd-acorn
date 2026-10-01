using Microsoft.Extensions.Configuration;

namespace PricingValidationFramework.Tests.Helpers.Setup;

/// Shared between IceTestSetup and RadarTestSetup: appsettings.json, an optional
/// appsettings.{environment}.json, appsettings.Development.json when running locally, and
/// environment variables (double-underscore translated to the ":" section separator
/// IConfiguration expects) layered on top, highest priority last.
internal static class TestConfigurationLoader
{
    public static IConfiguration Load()
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
