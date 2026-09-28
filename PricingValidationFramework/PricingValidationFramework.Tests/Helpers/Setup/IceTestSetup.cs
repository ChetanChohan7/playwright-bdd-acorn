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

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class IceTestSetup : IDisposable
{
    private readonly ILoggerFactory loggerFactory;

    private IceTestSetup(
        string buildId,
        IceSettings iceSettings,
        RetrySettings retrySettings,
        BaselineDataReader baselineReader,
        IceApiClient apiClient,
        IceUrlBuilder urlBuilder,
        JsonValueExtractor jsonExtractor,
        XmlValueExtractor xmlExtractor,
        IceTestRunLogger logger,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        BuildId = buildId;
        IceSettings = iceSettings;
        RetrySettings = retrySettings;
        BaselineReader = baselineReader;
        ApiClient = apiClient;
        UrlBuilder = urlBuilder;
        JsonExtractor = jsonExtractor;
        XmlExtractor = xmlExtractor;
        Logger = logger;
        this.loggerFactory = loggerFactory;
        CancellationToken = cancellationToken;
    }

    public string BuildId { get; }
    public IceSettings IceSettings { get; }
    public RetrySettings RetrySettings { get; }
    public BaselineDataReader BaselineReader { get; }
    public IceApiClient ApiClient { get; }
    public IceUrlBuilder UrlBuilder { get; }
    public JsonValueExtractor JsonExtractor { get; }
    public XmlValueExtractor XmlExtractor { get; }
    public IceTestRunLogger Logger { get; }
    public CancellationToken CancellationToken { get; }

    public static IceTestSetup Create(CancellationToken cancellationToken)
    {
        var configuration = LoadConfiguration();

        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()
            ?? throw new InvalidOperationException("IceSettings is missing.");
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");

        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local";

        var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        GlobalDiagnosticsContext.Set("BuildId", buildId);

        var apiClient = new IceApiClient(
            iceSettings,
            loggerFactory.CreateLogger<IceApiClient>(),
            retrySettings);

        var baselineReader = new BaselineDataReader(
            new SqlConnectionFactory(databaseSettings),
            retrySettings);

        return new IceTestSetup(
            buildId,
            iceSettings,
            retrySettings,
            baselineReader,
            apiClient,
            new IceUrlBuilder(),
            new JsonValueExtractor(),
            new XmlValueExtractor(),
            new IceTestRunLogger(loggerFactory.CreateLogger<IceTestRunLogger>()),
            loggerFactory,
            cancellationToken);
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

        builder.AddInMemoryCollection(GetEnvironmentVariables());

        return builder.Build();
    }

    private static Dictionary<string, string?> GetEnvironmentVariables()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            values[key] = Environment.GetEnvironmentVariable(key);
        }

        return values;
    }

    public void Dispose()
    {
        ApiClient.Dispose();
        loggerFactory.Dispose();
    }
}