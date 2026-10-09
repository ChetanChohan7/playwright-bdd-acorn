using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class IceTestSetup : IDisposable
{
    private readonly ILoggerFactory loggerFactory;

    private IceTestSetup(
        string buildId,
        IceSettings iceSettings,
        IceApiClient apiClient,
        IceUrlBuilder urlBuilder,
        JsonValueExtractor jsonExtractor,
        XmlValueExtractor xmlExtractor,
        IceTestRunLogger logger,
        ILoggerFactory loggerFactory)
    {
        BuildId = buildId;
        IceSettings = iceSettings;
        ApiClient = apiClient;
        UrlBuilder = urlBuilder;
        JsonExtractor = jsonExtractor;
        XmlExtractor = xmlExtractor;
        Logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public string BuildId { get; }
    public IceSettings IceSettings { get; }
    public IceApiClient ApiClient { get; }
    public IceUrlBuilder UrlBuilder { get; }
    public JsonValueExtractor JsonExtractor { get; }
    public XmlValueExtractor XmlExtractor { get; }
    public IceTestRunLogger Logger { get; }

    public static async Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = TestConfigurationLoader.Load();
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateDatabaseRetrySettings();
        var reader = new BaselineDataReader(new SqlConnectionFactory(databaseSettings), retrySettings);
        return await reader.GetPassingBaselineScenariosAsync(cancellationToken);
    }

    public static IceTestSetup Create()
    {
        var configuration = TestConfigurationLoader.Load();

        var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()
            ?? throw new InvalidOperationException("IceSettings is missing.");
        iceSettings.Validate();
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateApiRetrySettings();

        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local";

        var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        GlobalDiagnosticsContext.Set("BuildId", buildId);

        var apiClient = new IceApiClient(
            iceSettings,
            loggerFactory.CreateLogger<IceApiClient>(),
            retrySettings);

        return new IceTestSetup(
            buildId,
            iceSettings,
            apiClient,
            new IceUrlBuilder(),
            new JsonValueExtractor(),
            new XmlValueExtractor(),
            new IceTestRunLogger(loggerFactory.CreateLogger<IceTestRunLogger>()),
            loggerFactory);
    }

    public void Dispose()
    {
        ApiClient.Dispose();
        loggerFactory.Dispose();
    }
}