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
using PricingValidationFramework.Core.Validation;

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
        PricingComparisonService pricingProfiles,
        IceTestRunLogger logger,
        ILoggerFactory loggerFactory)
    {
        BuildId = buildId;
        IceSettings = iceSettings;
        ApiClient = apiClient;
        UrlBuilder = urlBuilder;
        JsonExtractor = jsonExtractor;
        PricingProfiles = pricingProfiles;
        Logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public string BuildId { get; }
    public IceSettings IceSettings { get; }
    public IceApiClient ApiClient { get; }
    public IceUrlBuilder UrlBuilder { get; }
    public JsonValueExtractor JsonExtractor { get; }
    public PricingComparisonService PricingProfiles { get; }
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
        return await DiscoverScenariosAsync(reader, TestConfigurationLoader.ReadTestTag(configuration), cancellationToken);
    }

    /// Newest PASS baseline per product/scheme, limited to the TEST_TAG product when one is set
    /// (the same filter as the Radar stage before it).
    internal static Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        IBaselineDataReader reader,
        string testTag,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(testTag)
            ? reader.GetPassingBaselineScenariosAsync(cancellationToken)
            : reader.GetPassingBaselineScenariosByProductCodeAsync(testTag, cancellationToken);
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
        var profiles = TestConfigurationLoader.LoadProfiles(configuration);

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
            new PricingComparisonService(profiles.Profiles),
            new IceTestRunLogger(loggerFactory.CreateLogger<IceTestRunLogger>()),
            loggerFactory);
    }

    public void Dispose()
    {
        ApiClient.Dispose();
        loggerFactory.Dispose();
    }
}