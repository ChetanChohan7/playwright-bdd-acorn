using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class IceTestSetup : IDisposable
{
    private IceTestSetup(
        string buildId,
        IceSettings iceSettings,
        BaselineDataReader baselineReader,
        IceApiClient apiClient,
        IceUrlBuilder urlBuilder,
        JsonValueExtractor jsonExtractor,
        XmlValueExtractor xmlExtractor,
        CancellationToken cancellationToken)
    {
        BuildId = buildId;
        IceSettings = iceSettings;
        BaselineReader = baselineReader;
        ApiClient = apiClient;
        UrlBuilder = urlBuilder;
        JsonExtractor = jsonExtractor;
        XmlExtractor = xmlExtractor;
        CancellationToken = cancellationToken;
    }

    public string BuildId { get; }
    public IceSettings IceSettings { get; }
    public BaselineDataReader BaselineReader { get; }
    public IceApiClient ApiClient { get; }
    public IceUrlBuilder UrlBuilder { get; }
    public JsonValueExtractor JsonExtractor { get; }
    public XmlValueExtractor XmlExtractor { get; }
    public CancellationToken CancellationToken { get; }

    public static IceTestSetup Create(CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()
            ?? throw new InvalidOperationException("IceSettings is missing.");

        return new IceTestSetup(
            Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local",
            iceSettings,
            new BaselineDataReader(new SqlConnectionFactory(databaseSettings)),
            new IceApiClient(iceSettings, NullLogger<IceApiClient>.Instance),
            new IceUrlBuilder(),
            new JsonValueExtractor(),
            new XmlValueExtractor(),
            cancellationToken);
    }

    public void Dispose()
    {
        ApiClient.Dispose();
    }
}