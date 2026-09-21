using FuzzyPricingMatcher.Tests.ExternalAPIAccess;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Database;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Models;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Validation;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class MockWorkflowOrchestrationTests
{
    [Test]
    public void Comparison_passes_when_difference_is_inside_the_inclusive_threshold_range()
    {
        var requestXml = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var comparison = new WorkflowFakes(requestXml, ResponseXml(10.25m), ResponseXml(10.00m));
        var comparisonResult = new ComparisonScenarioExecutor(comparison, new RequestXmlMetadataReader(), comparison, comparison, CreateValidationService(), new ThresholdEvaluator(), comparison, comparison).Execute(new ComparisonScenario("SCN-1", "Q-1"), new ComparisonScenarioInput(string.Empty, "2026-09-21", -0.50m, 0.50m, "BUILD-1", "NUMBER-1"));

        Assert.Multiple(() =>
        {
            Assert.That(comparisonResult.Result.Outcome, Is.EqualTo(ComparisonOutcome.Passed));
            Assert.That(comparisonResult.Result.Passed, Is.True);
            Assert.That(comparisonResult.Result.Difference, Is.EqualTo(0.25m));
            Assert.That(comparison.PassCommand, Is.Not.Null);
            Assert.That(comparison.GetScenarioCalls, Is.EqualTo(1));
            Assert.That(comparison.PassCommand!.XmlResponse, Is.EqualTo(ResponseXml(10.25m)));
        });
    }

    [Test]
    public void Comparison_fails_when_difference_is_outside_the_inclusive_threshold_range()
    {
        var requestXml = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var comparison = new WorkflowFakes(requestXml, ResponseXml(11.00m), ResponseXml(10.00m));
        var comparisonResult = new ComparisonScenarioExecutor(comparison, new RequestXmlMetadataReader(), comparison, comparison, CreateValidationService(), new ThresholdEvaluator(), comparison, comparison).Execute(new ComparisonScenario("SCN-1", "Q-1"), new ComparisonScenarioInput(string.Empty, "2026-09-21", -0.50m, 0.50m, "BUILD-1", "NUMBER-1"));

        Assert.Multiple(() =>
        {
            Assert.That(comparisonResult.Result.Outcome, Is.EqualTo(ComparisonOutcome.ThresholdFailed));
            Assert.That(comparisonResult.Result.Passed, Is.False);
            Assert.That(comparisonResult.Result.Difference, Is.EqualTo(1.00m));
            Assert.That(comparison.FailCommand, Is.Not.Null);
            Assert.That(comparison.PassCommand, Is.Null);
        });
    }

    [Test]
    public void Loader_inserts_a_new_record_after_mock_api_fetch_and_validation()
    {
        var requestXml = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var loader = new WorkflowFakes(requestXml, ResponseXml(10.25m), ResponseXml(10.00m));
        var prepared = Prepare(requestXml, "SCN-2", "Q-2");
        var loaderResult = new LoaderSynchronizationService(loader, loader, loader, loader, loader).Synchronize(new[] { prepared }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(loaderResult.Successful, Is.True);
            Assert.That(loaderResult.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.Inserted));
            Assert.That(loader.InsertCommand, Is.Not.Null);
            Assert.That(loader.ApiRequests.Single().RawXml, Is.EqualTo(requestXml.Replace("Q-1", "Q-2", StringComparison.Ordinal)));
            Assert.That(loader.Events, Is.EqualTo(new[] { "api", "validate", "insert" }));
        });
    }

    private static string ResponseXml(decimal amount) => $"<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\"><PlaceholderAmount>{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}</PlaceholderAmount></PlaceholderResponse>";

    private static ExternalResponseValidationService CreateValidationService()
    {
        var registry = new SchemaRegistry(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas"), new[] { "Scheme01-response.xsd" });
        return new ExternalResponseValidationService(new XmlSchemaValidator(registry), new ComparisonAmountReaderRegistry(new[] { new ComparisonAmountReaderRegistration("PlaceholderResponseProcessor", new PlaceholderResponseAmountReader()) }));
    }

    private static PreparedBaselineScenario Prepare(string raw, string scenarioId, string quoteRef)
    {
        var metadata = new RequestXmlMetadataReader().Read(raw.Replace("S-1", "S-1").Replace("Q-1", quoteRef, StringComparison.Ordinal));
        return new PreparedBaselineScenario
        {
            CsvRow = new BaselineScenarioCsvRow { RowNumber = 2, ScenarioId = scenarioId, XmlRequest = raw.Replace("Q-1", quoteRef, StringComparison.Ordinal), TestTags = "synthetic" },
            NormalizedScenarioId = scenarioId,
            RequestMetadata = metadata,
            NormalizedTags = new TagNormalizer().Normalize("synthetic"),
            XmlFingerprint = new XmlFingerprintService().CreateFingerprint(metadata.Document)
        };
    }

    private sealed class WorkflowFakes : IFuzzyMatcherRepository, IScenarioRouteResolver, IExternalXmlServiceClient, IScenarioEvidenceWriter, IScenarioLogger, ILoaderRepository, ILoaderRouteResolver, ILoaderApiClient, ILoaderResponseValidator, ILoaderEvidenceWriter
    {
        private readonly string requestXml;
        private readonly string responseXml;

        public WorkflowFakes(string requestXml, string apiResponseXml, string baselineResponseXml)
        {
            this.requestXml = requestXml;
            this.responseXml = apiResponseXml;
            Snapshot = new DatabaseScenarioSnapshot(new DatabaseRequestRecord("SCN-1", "Q-1", requestXml, "synthetic", DateTime.UtcNow), new DatabaseResponseRecord("SCN-1", "Q-1", baselineResponseXml, "BASELINE", DateTime.UtcNow, null, null));
        }

        public DatabaseScenarioSnapshot Snapshot { get; }
        public DatabaseComparisonPassCommand? PassCommand { get; private set; }
        public DatabaseComparisonFailCommand? FailCommand { get; private set; }
        public int GetScenarioCalls { get; private set; }
        public InsertBaselineScenarioCommand? InsertCommand { get; private set; }
        public List<LoaderApiRequest> ApiRequests { get; } = [];
        public List<string> Events { get; } = [];

        public Task<DatabaseScenarioSnapshot> GetScenarioForComparisonAsync(string scenarioId, CancellationToken cancellationToken = default)
        {
            GetScenarioCalls++;
            return Task.FromResult(Snapshot with
            {
                Request = Snapshot.Request! with { ScenarioId = scenarioId },
                Response = Snapshot.Response! with { ScenarioId = scenarioId }
            });
        }
        public Task<IReadOnlyList<string>> GetAllRequestScenarioIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<DatabaseRequestRecord?> GetRequestAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseRequestRecord?>(Snapshot.Request);
        public Task<DatabaseResponseRecord?> GetResponseAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseResponseRecord?>(Snapshot.Response);
        public Task<IReadOnlyList<DatabaseDuplicateResult>> FindDuplicatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseDuplicateResult>>(Array.Empty<DatabaseDuplicateResult>());
        public Task InsertBaselineAsync(DatabaseInsertCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateBaselineAsync(DatabaseUpdateCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateTagsAsync(DatabaseTagsUpdateCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteObsoleteAsync(IReadOnlyList<string> scenarioIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DatabaseScenarioSelection>> SelectScenariosAsync(string? requestedTags, TagMatchMode matchMode, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseScenarioSelection>>(Array.Empty<DatabaseScenarioSelection>());
        public Task UpdateComparisonPassAsync(DatabaseComparisonPassCommand command, CancellationToken cancellationToken = default) { PassCommand = command; Events.Add("pass"); return Task.CompletedTask; }
        public Task UpdateComparisonFailAsync(DatabaseComparisonFailCommand command, CancellationToken cancellationToken = default) { FailCommand = command; Events.Add("fail"); return Task.CompletedTask; }

        ComparisonScenarioRoute IScenarioRouteResolver.Resolve(string schemeCode) => new(schemeCode, "EndpointA", new EndpointSettings { BaseUrl = "https://mock.invalid", Resource = "/comparison", Username = "mock-user", Password = "mock-password", Enabled = true }, new RouteSettings { EndpointName = "EndpointA", ResponseSchemaFile = "Scheme01-response.xsd", ResponseProcessorName = "PlaceholderResponseProcessor", Enabled = true });
        public Task<ExternalAPIResponse> SendXmlRequestAsync(ExternalXmlRequest request, CancellationToken cancellationToken = default) { Events.Add("comparison-api"); return Task.FromResult(ExternalAPIResponse.Success(200, responseXml, 1, TimeSpan.Zero)); }
        public void Save(ScenarioEvidence evidence) { }
        public void Outcome(ComparisonResult result) { }
        public void QuoteMismatch(string scenarioId, string extractedQuoteRef, string storedQuoteRef) { }

        public BaselineDatabaseSnapshot GetByScenarioId(string scenarioId) => new(Array.Empty<ExistingRequestRow>(), Array.Empty<ExistingResponseRow>());
        public IReadOnlyList<string> GetAllScenarioIds() => Array.Empty<string>();
        public void Insert(InsertBaselineScenarioCommand command) { InsertCommand = command; Events.Add("insert"); }
        public void Update(UpdateBaselineScenarioCommand command) => throw new NotSupportedException();
        public void UpdateTags(string scenarioId, IReadOnlyList<string> normalizedTags) => throw new NotSupportedException();
        public void DeleteObsolete(DeleteObsoleteScenarioCommand command) => throw new NotSupportedException();
        RouteDefinition ILoaderRouteResolver.Resolve(string schemeCode) => new(schemeCode, "EndpointA");
        public LoaderApiResponse Fetch(LoaderApiRequest request, RouteDefinition route) { ApiRequests.Add(request); Events.Add("api"); return new LoaderApiResponse(responseXml); }
        public void Validate(LoaderApiResponse response, RouteDefinition route) => Events.Add("validate");
        public void Save(LoaderEvidence evidence) { }
    }
}
