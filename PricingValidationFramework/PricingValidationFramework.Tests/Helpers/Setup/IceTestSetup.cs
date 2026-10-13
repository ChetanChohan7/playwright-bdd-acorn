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
using PricingValidationFramework.Tests.Helpers.Reporting;

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

    public static Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(CancellationToken cancellationToken = default) =>
        DiscoverScenariosAsync(includeLocalSettings: false, cancellationToken);

    public static async Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        bool includeLocalSettings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateDatabaseRetrySettings();
        var reader = new BaselineDataReader(new SqlConnectionFactory(databaseSettings), retrySettings);
        return await DiscoverScenariosAsync(reader, IceSelection.From(configuration), cancellationToken);
    }

    /// Which baselines ICE checks. With a Radar report (ICE_RADAR_REPORT): exactly the scenarios that
    /// passed in that Radar run. Otherwise the newest PASS baseline per product/scheme, within the same
    /// TEST_TAG product and SCENARIO_TAG tag filters as Radar.
    internal static async Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        IBaselineDataReader reader,
        IceSelection selection,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(selection.RadarReport))
        {
            var passed = await RadarReportScenarios.ReadPassedScenarioIdsAsync(selection.RadarReport, cancellationToken);
            return passed.Count == 0 ? [] : await reader.GetPassingBaselinesByScenarioIdsAsync(passed, cancellationToken);
        }

        return string.IsNullOrWhiteSpace(selection.ProductCode) && string.IsNullOrWhiteSpace(selection.ScenarioTag)
            ? await reader.GetPassingBaselineScenariosAsync(cancellationToken)
            : await reader.GetFilteredPassingBaselineScenariosAsync(selection.ProductCode, selection.ScenarioTag, cancellationToken);
    }

    internal sealed record IceSelection(string ProductCode, string ScenarioTag, string? RadarReport)
    {
        public const string RadarReportVariable = "ICE_RADAR_REPORT";

        public static IceSelection From(IConfiguration configuration)
        {
            var report = Environment.GetEnvironmentVariable(RadarReportVariable) ?? configuration["PipelineSettings:IceRadarReport"];
            return new IceSelection(
                TestConfigurationLoader.ReadTestTag(configuration),
                TestConfigurationLoader.ReadScenarioTag(configuration),
                string.IsNullOrWhiteSpace(report) ? null : report.Trim());
        }
    }

    /// <param name="includeLocalSettings">True from the live fixture: also reads data/import/local-settings.json.</param>
    public static IceTestSetup Create(bool includeLocalSettings = false)
    {
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);

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