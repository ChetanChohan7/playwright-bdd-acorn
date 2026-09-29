using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog;
using System.Net;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Common;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Validation;
using RestSharp;

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class RadarTestSetup : IDisposable
{
    private readonly PipelineSettings settings;
    private readonly RadarRequestRateLimiter rateLimiter;
    private readonly RadarApiClient apiClient;
    private readonly ILoggerFactory loggerFactory;
    private bool disposed;

    private RadarTestSetup(
        PipelineSettings settings,
        RadarRequestRateLimiter rateLimiter,
        BaselineDataReader baselineReader,
        ResultUpdater resultUpdater,
        RadarApiClient apiClient,
        XmlValueExtractor xmlExtractor,
        RadarScenarioProcessor scenarioProcessor,
        RadarTestRunLogger logger,
        ILoggerFactory loggerFactory)
    {
        this.settings = settings;
        this.rateLimiter = rateLimiter;
        this.apiClient = apiClient;
        BaselineReader = baselineReader;
        ResultUpdater = resultUpdater;
        XmlExtractor = xmlExtractor;
        ScenarioProcessor = scenarioProcessor;
        Logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public BaselineDataReader BaselineReader { get; }
    public ResultUpdater ResultUpdater { get; }
    public XmlValueExtractor XmlExtractor { get; }
    public RadarScenarioProcessor ScenarioProcessor { get; }
    public RadarTestRunLogger Logger { get; }

    public string BuildId => settings.BuildId;
    public string TestTag => settings.TestTag;
    public string RequestTime => settings.RequestTime;
    public decimal MinThreshold => settings.MinThreshold;
    public decimal MaxThreshold => settings.MaxThreshold;

    public static RadarTestSetup Create()
    {
        var configuration = LoadConfiguration();
        var radarConfiguration = LoadAndValidateRadarConfiguration(configuration);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var (pipelineSettings, retrySettings) = LoadValidatedPipelineInputs(configuration);
        var rateLimiter = new RadarRequestRateLimiter(
            radarConfiguration.RateLimitSettings,
            radarConfiguration.RadarSettings.Endpoints.Keys);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        var connectionFactory = new SqlConnectionFactory(databaseSettings);
        var baselineReader = new BaselineDataReader(connectionFactory, retrySettings);
        var resultUpdater = new ResultUpdater(connectionFactory, retrySettings);
        var xmlExtractor = new XmlValueExtractor();
        var logger = new RadarTestRunLogger(loggerFactory.CreateLogger<RadarTestRunLogger>());
        GlobalDiagnosticsContext.Set("RadarBuildId", pipelineSettings.BuildId);
        var radarRestClient = new RestClient(new RestClientOptions
        {
            AutomaticDecompression = DecompressionMethods.All
        });
        var apiClient = new RadarApiClient(
            rateLimiter,
            radarRestClient,
            loggerFactory.CreateLogger<RadarApiClient>(),
            retrySettings);
        var scenarioProcessor = new RadarScenarioProcessor(
            radarConfiguration.RadarSettings,
            apiClient,
            new RadarUrlBuilder(),
            new XsdFileResolver(),
            new XsdValidator(),
            new ThresholdMatcher(),
            logger);

        return new RadarTestSetup(
            pipelineSettings,
            rateLimiter,
            baselineReader,
            resultUpdater,
            apiClient,
            xmlExtractor,
            scenarioProcessor,
            logger,
            loggerFactory);
    }

    public static async Task<IReadOnlyList<ScenarioRequest>> DiscoverScenariosAsync(CancellationToken cancellationToken = default)
    {
        var configuration = LoadConfiguration();
        var radarConfiguration = LoadAndValidateRadarConfiguration(configuration);
        var (pipelineSettings, retrySettings) = LoadValidatedPipelineInputs(configuration);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var reader = new RequestDataReader(new SqlConnectionFactory(databaseSettings), retrySettings);

        return string.IsNullOrWhiteSpace(pipelineSettings.TestTag)
            ? await reader.GetAllScenariosAsync(cancellationToken)
            : await reader.GetScenariosByTestTagAsync(pipelineSettings.TestTag, cancellationToken);
    }

    private static (PipelineSettings PipelineSettings, RetrySettings RetrySettings) LoadValidatedPipelineInputs(
        IConfiguration configuration)
    {
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.Validate();

        var pipelineSettings = new PipelineSettings
        {
            BuildId = ReadRequiredBuildId(),
            MinThreshold = ReadRequiredDecimal("RADAR_MIN_THRESHOLD"),
            MaxThreshold = ReadRequiredDecimal("RADAR_MAX_THRESHOLD"),
            RequestTime = RequestTimeFormatter.Resolve(Environment.GetEnvironmentVariable("RADAR_REQUEST_DATETIME")),
            TestTag = Environment.GetEnvironmentVariable("TEST_TAG") ?? string.Empty
        };
        new PipelineInputValidator().Validate(pipelineSettings);

        return (pipelineSettings, retrySettings);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        apiClient.Dispose();
        rateLimiter.Dispose();
        loggerFactory.Dispose();
        GlobalDiagnosticsContext.Remove("RadarBuildId");
    }

    private static string ReadRequiredBuildId()
    {
        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");

        if (!string.IsNullOrWhiteSpace(buildId))
        {
            return buildId.Trim();
        }

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.Equals(environmentName, "local", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        throw new InvalidOperationException("BUILD_BUILDID is required outside local development.");
    }

    private static decimal ReadRequiredDecimal(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{variableName} is required and must be a valid decimal.");
        }

        if (!decimal.TryParse(value.Trim(), global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{variableName} must be a valid decimal.");
        }

        return parsed;
    }

    private static IConfiguration LoadConfiguration()
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

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            values[key.Replace("__", ":", StringComparison.Ordinal)] = Environment.GetEnvironmentVariable(key);
        }

        return builder
            .AddInMemoryCollection(values)
            .Build();
    }

    private static RadarRunConfiguration LoadAndValidateRadarConfiguration(IConfiguration configuration)
    {
        var radarSection = configuration.GetSection("RadarSettings");
        if (!radarSection.Exists())
        {
            throw new InvalidOperationException("RadarSettings is missing.");
        }

        var radarSettings = radarSection.Get<RadarSettings>()
            ?? throw new InvalidOperationException("RadarSettings is missing.");
        var rateLimitSection = configuration.GetSection("RadarRateLimitSettings");
        if (!rateLimitSection.Exists())
        {
            throw new InvalidOperationException("RadarRateLimitSettings is missing.");
        }

        var rateLimitSettings = rateLimitSection.Get<RadarRateLimitSettings>()
            ?? throw new InvalidOperationException("RadarRateLimitSettings is missing.");
        ValidateRadarConfiguration(radarSettings, rateLimitSettings);

        return new RadarRunConfiguration(radarSettings, rateLimitSettings);
    }

    internal static void ValidateRadarConfiguration(
        RadarSettings radarSettings,
        RadarRateLimitSettings rateLimitSettings)
    {
        ArgumentNullException.ThrowIfNull(radarSettings);
        ArgumentNullException.ThrowIfNull(rateLimitSettings);
        rateLimitSettings.Validate();

        if (radarSettings.Endpoints is null || radarSettings.Endpoints.Count == 0)
        {
            throw new InvalidOperationException("At least one Radar logical endpoint must be configured.");
        }

        var endpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpointName in radarSettings.Endpoints.Keys)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new InvalidOperationException("Radar logical endpoint names must not be empty.");
            }

            if (endpointName.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.')))
            {
                throw new InvalidOperationException("Radar logical endpoint names contain unsupported characters.");
            }

            if (!endpointNames.Add(endpointName))
            {
                throw new InvalidOperationException("Radar logical endpoint names must be unique ignoring case.");
            }

            var endpoint = radarSettings.Endpoints[endpointName];
            if (endpoint is null ||
                !Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Every Radar endpoint must have an absolute HTTP or HTTPS BaseUrl.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.ApiKeyHeaderName))
            {
                throw new InvalidOperationException("Every Radar endpoint must have an ApiKeyHeaderName.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.ApiKeyValue))
            {
                throw new InvalidOperationException("Every Radar endpoint must have an ApiKeyValue.");
            }
        }

        if (radarSettings.Routes is null)
        {
            throw new InvalidOperationException("Radar routes are missing.");
        }

        if (radarSettings.Routes.Count == 0)
        {
            throw new InvalidOperationException("At least one Radar route must be configured.");
        }

        foreach (var route in radarSettings.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Value?.ProductCode) ||
                string.IsNullOrWhiteSpace(route.Value.SchemeCode))
            {
                throw new InvalidOperationException("Every Radar route must have ProductCode and SchemeCode values.");
            }

            if (string.IsNullOrWhiteSpace(route.Value.EndpointName) || !endpointNames.Contains(route.Value.EndpointName))
            {
                throw new InvalidOperationException("Every Radar route must reference a configured logical endpoint.");
            }

            if (string.IsNullOrWhiteSpace(route.Value.RouteKey))
            {
                throw new InvalidOperationException("Every Radar route must have a RouteKey.");
            }
        }

        if (radarSettings.ResponseXsdMappings is null)
        {
            throw new InvalidOperationException("Radar response XSD mappings are missing.");
        }

        foreach (var mapping in radarSettings.ResponseXsdMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.Key) || !radarSettings.Routes.ContainsKey(mapping.Key))
            {
                throw new InvalidOperationException("Every Radar response XSD mapping must reference a configured route identifier.");
            }
        }
    }

    private sealed record RadarRunConfiguration(
        RadarSettings RadarSettings,
        RadarRateLimitSettings RateLimitSettings);
}
