namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Seeding;
using PricingValidationFramework.Core.Validation;

/// Everything `DataLoader seed` needs, read from the optional --settings JSON with environment
/// variables on top (the same names the Radar pipeline uses, e.g. RadarSettings__Endpoints__Endpoint1__BaseUrl).
public sealed record SeedSettings(
    DatabaseSettings Database,
    RadarSettings Radar,
    RadarRateLimitSettings RateLimit,
    RetrySettings Retry,
    string MappingFile,
    string? RequestTime);

public static class SeedCommand
{
    public const string DefaultMappingFile = "Radar/mappings.json";
    public static readonly string DefaultReportPath = Path.Combine("TestResults", "DataLoader", "seed-summary.json");

    private static readonly string[] Options = ["--settings", "--report", "--request-time", "--max-parallel"];

    public static async Task<int> RunAsync(string[] optionArgs, CancellationToken cancellationToken)
    {
        var reportPath = DefaultReportPath;
        var requestTime = string.Empty;
        BaselineSeedSummary summary;
        try
        {
            var values = ParseOptions(optionArgs);
            reportPath = values.GetValueOrDefault("--report", reportPath);
            var maxParallel = int.Parse(values.GetValueOrDefault("--max-parallel", "4"), CultureInfo.InvariantCulture);
            var settings = LoadSettings(
                BuildConfiguration(values.GetValueOrDefault("--settings")),
                values.GetValueOrDefault("--request-time"),
                AppContext.BaseDirectory);
            // Resolve "now" once, so the summary and every Radar call use the same hour.
            requestTime = RequestTimeFormatter.Resolve(settings.RequestTime);

            Console.WriteLine($"seed: mapping={settings.MappingFile}, endpoints={string.Join(",", settings.Radar.Endpoints.Keys)}, maxParallel={maxParallel}.");
            summary = await RunAsync(settings with { RequestTime = requestTime }, maxParallel, cancellationToken);
        }
        catch (Exception exception)
        {
            summary = new BaselineSeedSummary { RequestTime = requestTime, Cancelled = exception is OperationCanceledException && cancellationToken.IsCancellationRequested };
            summary.Issues.Add($"The seed command could not complete ({exception.GetType().Name}): {exception.Message}");
            Console.Error.WriteLine(summary.Issues[0]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None);
        Console.WriteLine($"seed: requestTime={summary.RequestTime}, success={summary.Succeeded}, withoutBaseline={summary.ScenariosWithoutBaseline}, " +
            $"seeded={summary.Seeded}, alreadyExisted={summary.AlreadyExisted}, failed={summary.Failed}, notRun={summary.NotRun}, report={Path.GetFullPath(reportPath)}.");
        return summary.Cancelled ? 130 : summary.Succeeded ? 0 : 1;
    }

    private static async Task<BaselineSeedSummary> RunAsync(SeedSettings settings, int maxParallel, CancellationToken cancellationToken)
    {
        var connectionFactory = new SqlConnectionFactory(settings.Database);
        using var rateLimiter = new RadarRequestRateLimiter(settings.RateLimit, settings.Radar.Endpoints.Keys);
        using var apiClient = new RadarApiClient(
            rateLimiter,
            new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }),
            retrySettings: settings.Retry);
        var pricingService = new RadarPricingService(
            settings.Radar,
            apiClient,
            new RadarUrlBuilder(),
            // Seeding never compares, so no XPath profiles are loaded.
            new PricingComparisonService([]));
        var seeder = new BaselineSeeder(
            new SqlBaselineSeedStore(
                new RequestDataReader(connectionFactory, settings.Retry),
                new ResultUpdater(connectionFactory, settings.Retry)),
            pricingService);

        return await seeder.RunAsync(settings.RequestTime, maxParallel, result =>
            Console.WriteLine(result.Outcome == BaselineSeedOutcome.Seeded
                ? $"  SEEDED  {result.ScenarioId} ({result.ProductCode}/{result.SchemeCode}, route {result.RouteId})"
                : $"  {result.Outcome.ToString().ToUpperInvariant()}  {result.ScenarioId} ({result.ProductCode}/{result.SchemeCode}) {result.Stage}: {result.Error}"),
            cancellationToken);
    }

    public static IConfiguration BuildConfiguration(string? settingsPath)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            builder.AddJsonFile(Path.GetFullPath(settingsPath), optional: false, reloadOnChange: false);
        }

        return builder.AddEnvironmentVariables().Build();
    }

    public static SeedSettings LoadSettings(IConfiguration configuration, string? requestTimeOption, string baseDirectory)
    {
        var database = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>() ?? new DatabaseSettings();
        if (string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            throw new InvalidOperationException("DatabaseSettings:ConnectionString is required (DatabaseSettings__ConnectionString or the --settings file).");
        }

        var radar = configuration.GetSection("RadarSettings").Get<RadarSettings>() ?? new RadarSettings();
        // An endpoint left completely blank (like the placeholders in a copied settings file) is
        // ignored, so a local run only needs the endpoint its scenarios use. A partly filled one is
        // still rejected by the validator.
        radar.Endpoints = (radar.Endpoints ?? []).Where(endpoint => endpoint.Value is not null &&
                !(string.IsNullOrWhiteSpace(endpoint.Value.BaseUrl) &&
                  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyHeaderName) &&
                  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyValue)))
            .ToDictionary(endpoint => endpoint.Key, endpoint => endpoint.Value, StringComparer.Ordinal);

        var mappingFile = PricingProfileConfiguration.ResolveMappingPath(
            configuration["PricingProfiles:MappingFile"] ?? DefaultMappingFile, baseDirectory);
        radar.Routes = PricingRouteConfiguration.LoadRoutes(mappingFile).CreateRadarRoutes();

        var rateLimit = configuration.GetSection("RadarRateLimitSettings").Get<RadarRateLimitSettings>() ?? new RadarRateLimitSettings();
        RadarSettingsValidator.Validate(radar, rateLimit, requireEndpointForEveryRoute: false);

        var retry = configuration.GetSection("RetrySettings").Get<RetrySettings>() ?? new RetrySettings();
        retry.ValidateRadarPipelineSettings();

        // --request-time, then RADAR_REQUEST_DATETIME (as Radar reads it), then the settings file, then now.
        var requestTime = new[] { requestTimeOption, configuration["RADAR_REQUEST_DATETIME"], configuration["PipelineSettings:RequestTime"] }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        // "now" (any case) means the current UTC hour, as it does for validate and import.
        if (string.Equals(requestTime, "now", StringComparison.OrdinalIgnoreCase))
        {
            requestTime = null;
        }

        RequestTimeFormatter.Resolve(requestTime);

        return new SeedSettings(database, radar, rateLimit, retry, mappingFile, requestTime);
    }

    private static Dictionary<string, string> ParseOptions(string[] optionArgs)
    {
        if (optionArgs.Length % 2 != 0)
        {
            throw new ArgumentException("Options must be --name value pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < optionArgs.Length; index += 2)
        {
            if (!Options.Contains(optionArgs[index]) || !values.TryAdd(optionArgs[index], optionArgs[index + 1]))
            {
                throw new ArgumentException($"Unknown or duplicate seed option '{optionArgs[index]}'. Seed takes {string.Join(", ", Options)}.");
            }
        }

        return values;
    }
}
