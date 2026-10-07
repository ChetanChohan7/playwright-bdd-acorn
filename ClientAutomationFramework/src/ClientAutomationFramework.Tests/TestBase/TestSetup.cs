using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Database;
using ClientAutomationFramework.Core.Logging;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using RestSharp;

// Deliberately no namespace: a [SetUpFixture] outside any namespace runs its OneTimeSetUp/
// OneTimeTearDown around the WHOLE assembly, so this covers both Integration.Xml and
// Integration.Json. Everything below is lazily created on first use rather than in
// OneTimeSetUp, because NUnit builds TestCaseSource data before OneTimeSetUp runs - relying on
// OneTimeSetUp here would leave those sources reading uninitialized fields.

[SetUpFixture]
public sealed class TestSetup
{
    private static readonly Lazy<AppConfiguration> LazyConfiguration = new(() => AppConfiguration.Load(TestContext.CurrentContext.TestDirectory));
    private static readonly Lazy<RestClient> LazyClient = new(() => new RestClient());
    private static readonly Lazy<SqlConnectionFactory> LazyConnectionFactory = new(() => new SqlConnectionFactory(Configuration.Database));
    private static readonly Lazy<RequestDataReader> LazyRequestReader = new(() => new RequestDataReader(ConnectionFactory, Configuration.Database));
    private static readonly Lazy<ResponseDataReader> LazyResponseReader = new(() => new ResponseDataReader(ConnectionFactory, Configuration.Database));
    private static readonly Lazy<ResultUpdater> LazyResultUpdater = new(() => new ResultUpdater(ConnectionFactory, Configuration.Database));
    private static readonly Lazy<TestRunLogger> LazyLogger = new(() => new TestRunLogger());

    // TEMPORARY: the path to data/xml-response.json, standing in for the database - kept out
    // of Core.AppConfiguration deliberately, since it's a Tests-only stopgap, not a real setting
    // Core should know about. Delete alongside JsonFileResponseSource once the DB is ready.
    private static readonly Lazy<string> LazyStoredResponsesPath = new(() =>
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();
        var relativePath = configuration["StoredResponses:FilePath"] ?? "data/xml-response.json";
        return Path.Combine(TestContext.CurrentContext.TestDirectory, relativePath);
    });

    public static AppConfiguration Configuration => LazyConfiguration.Value;
    public static string StoredResponsesPath => LazyStoredResponsesPath.Value;

    // The single RestSharp instance for the whole test run - both XmlApiClient and
    // JsonApiClient are constructed from this same client, never their own.
    public static RestClient Client => LazyClient.Value;

    public static SqlConnectionFactory ConnectionFactory => LazyConnectionFactory.Value;
    public static RequestDataReader RequestReader => LazyRequestReader.Value;
    public static ResponseDataReader ResponseReader => LazyResponseReader.Value;
    public static ResultUpdater ResultUpdater => LazyResultUpdater.Value;
    public static TestRunLogger Logger => LazyLogger.Value;

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        if (LazyClient.IsValueCreated)
            LazyClient.Value.Dispose();
    }
}
