using System.Xml.Linq;
using FuzzyPricingMatcher.Tests.Api;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Data;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Validation;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

[Parallelizable(ParallelScope.All)]
public sealed class ComparisonFoundationTests
{
    [TestCase(10.00, 10.00, -0.05, 0.05, true)]
    [TestCase(10.05, 10.00, -0.05, 0.05, true)]
    [TestCase(9.95, 10.00, -0.05, 0.05, true)]
    [TestCase(10.06, 10.00, -0.05, 0.05, false)]
    [TestCase(9.94, 10.00, -0.05, 0.05, false)]
    [TestCase(10.001, 10.000, -0.002, 0.002, true)]
    [Category("Unit")]
    public void Thresholds_are_decimal_inclusive_and_use_signed_difference(decimal api, decimal baseline, decimal minimum, decimal maximum, bool expected)
    {
        var result = new ThresholdEvaluator().Evaluate(api, baseline, minimum, maximum);
        Assert.That(result.Passed, Is.EqualTo(expected));
    }

    [Test]
    [Category("Unit")]
    public void Invalid_threshold_range_is_rejected()
    {
        Assert.That(() => new ThresholdEvaluator().Evaluate(1m, 1m, 1m, -1m), Throws.ArgumentException);
    }

    [Test]
    [Category("Unit")]
    public void Database_source_uses_select_only_and_safe_names()
    {
        var repository = new ComparisonRepositoryFake();
        var cases = new DatabaseScenarioSource(repository).GetTestCases(string.Empty).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(repository.RequestedTags, Is.EqualTo(string.Empty));
            Assert.That(repository.RequestedMatchMode, Is.EqualTo(TagMatchMode.Any));
            Assert.That(repository.MutationCount, Is.EqualTo(0));
            Assert.That(cases, Has.Length.EqualTo(2));
            Assert.That(cases[0].TestName, Does.Match("^Compare_[A-Za-z0-9_]+$"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Passing_comparison_updates_before_final_result_and_replaces_baseline()
    {
        var fakes = new ComparisonFakes(ApiXml(10.04m), BaselineXml(10m));
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN/001", "Q-1"), Input(-0.05m, 0.05m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.Passed));
            Assert.That(result.Result.Passed, Is.True);
            Assert.That(fakes.Events.IndexOf("pass-update"), Is.LessThan(fakes.Events.IndexOf("final-evidence")));
            Assert.That(fakes.PassCommand!.XmlResponse, Is.EqualTo(ApiXml(10.04m)));
            Assert.That(fakes.ApiRequest!.RawXml, Is.EqualTo(fakes.RawRequest));
        });
    }

    [Test]
    [Category("Unit")]
    public void Threshold_failure_preserves_baseline_and_writes_fail_metadata()
    {
        var fakes = new ComparisonFakes(ApiXml(10.20m), BaselineXml(10m));
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-0.05m, 0.05m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.ThresholdFailed));
            Assert.That(result.Result.Passed, Is.False);
            Assert.That(fakes.FailCommand, Is.Not.Null);
            Assert.That(fakes.PassCommand, Is.Null);
            Assert.That(fakes.Events.IndexOf("fail-update"), Is.LessThan(fakes.Events.IndexOf("final-evidence")));
            Assert.That(fakes.OriginalBaseline, Is.EqualTo(BaselineXml(10m)));
        });
    }

    [Test]
    [Category("Unit")]
    public void API_failure_preserves_baseline_and_does_not_claim_a_pass()
    {
        var fakes = new ComparisonFakes(string.Empty, BaselineXml(10m)) { ApiResult = ApiCallResult.Failure(503, "unavailable", 3, TimeSpan.Zero) };
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.ApiFailed));
            Assert.That(result.DatabaseUpdated, Is.True);
            Assert.That(fakes.PassCommand, Is.Null);
            Assert.That(fakes.FailCommand, Is.Not.Null);
        });
    }

    [Test]
    [Category("Unit")]
    public void API_validation_failure_preserves_baseline_and_update_failure_is_explicit()
    {
        var fakes = new ComparisonFakes("<bad />", BaselineXml(10m)) { FailUpdateException = new InvalidOperationException("write failed") };
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.DatabaseFailed));
            Assert.That(result.Result.Error, Does.Contain("could not be saved"));
            Assert.That(result.DatabaseUpdated, Is.False);
            Assert.That(fakes.OriginalBaseline, Is.EqualTo(BaselineXml(10m)));
        });
    }

    [Test]
    [Category("Unit")]
    public void Cancellation_is_reported_without_network_or_mutation()
    {
        var fakes = new ComparisonFakes(ApiXml(10m), BaselineXml(10m));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m), cancellation.Token);
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.Cancelled));
            Assert.That(fakes.ApiRequest, Is.Null);
            Assert.That(fakes.PassCommand, Is.Null);
        });
    }

    [Test]
    [Category("Unit")]
    public void Missing_response_baseline_fails_without_api_or_status_update()
    {
        var fakes = new ComparisonFakes(ApiXml(10m), BaselineXml(10m));
        fakes.Snapshot = new DatabaseScenarioSnapshot(new DatabaseRequestRecord("SCN", "Q", fakes.RawRequest, "smoke", DateTime.UtcNow), null);
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.MissingBaseline));
            Assert.That(result.DatabaseUpdated, Is.False);
            Assert.That(fakes.ApiRequest, Is.Null);
            Assert.That(fakes.FailCommand, Is.Null);
            Assert.That(result.Result.Error, Does.Contain("response baseline is missing"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Missing_request_marks_unique_response_fail_without_api_call()
    {
        var fakes = new ComparisonFakes(ApiXml(10m), BaselineXml(10m));
        fakes.Snapshot = new DatabaseScenarioSnapshot(null, new DatabaseResponseRecord("SCN", "Q", BaselineXml(10m), "OLD", DateTime.UtcNow, null, null));
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.MissingBaseline));
            Assert.That(result.DatabaseUpdated, Is.True);
            Assert.That(fakes.ApiRequest, Is.Null);
            Assert.That(fakes.FailCommand, Is.Not.Null);
            Assert.That(result.Result.Error, Does.Contain("request baseline is missing"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Database_duplicate_is_reported_without_api_or_mutation()
    {
        var fakes = new ComparisonFakes(ApiXml(10m), BaselineXml(10m)) { SnapshotException = new DatabaseConsistencyException("duplicate rows") };
        var result = CreateExecutor(fakes).Execute(new ComparisonScenario("SCN", "Q"), Input(-1m, 1m));
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ComparisonOutcome.DatabaseConsistencyFailed));
            Assert.That(result.DatabaseUpdated, Is.False);
            Assert.That(fakes.ApiRequest, Is.Null);
            Assert.That(fakes.FailCommand, Is.Null);
        });
    }

    private static ComparisonScenarioExecutor CreateExecutor(ComparisonFakes fakes) => new(fakes, new RequestXmlMetadataReader(), fakes, fakes, CreateValidationService(), new ThresholdEvaluator(), fakes, fakes);

    private static ResponseValidationService CreateValidationService()
    {
        var registry = new SchemaRegistry(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas"), new[] { "Scheme01-response.xsd" });
        return new ResponseValidationService(new XmlSchemaValidator(registry), new ResponseAmountReaderRegistry(new[] { new ResponseAmountReaderRegistration("PlaceholderResponseProcessor", new PlaceholderResponseAmountReader()) }));
    }

    private static ComparisonScenarioInput Input(decimal minimum, decimal maximum) => new("", "", minimum, maximum, "BUILD", "NUMBER");
    private static string RequestXml(string quote) => $"<Request><SchemeCode>S-1</SchemeCode><PolicyReference>{quote}</PolicyReference></Request>";
    private static string ApiXml(decimal amount) => $"<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\"><PlaceholderAmount>{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}</PlaceholderAmount></PlaceholderResponse>";
    private static string BaselineXml(decimal amount) => ApiXml(amount);

    private sealed class ComparisonFakes : IFuzzyMatcherRepository, IScenarioRouteResolver, IXmlApiClient, IScenarioEvidenceWriter, IScenarioLogger
    {
        public ComparisonFakes(string apiXml, string baselineXml)
        {
            RawRequest = RequestXml("Q");
            OriginalBaseline = baselineXml;
            Snapshot = new DatabaseScenarioSnapshot(new DatabaseRequestRecord("SCN", "Q", RawRequest, "smoke", DateTime.UtcNow), new DatabaseResponseRecord("SCN", "Q", baselineXml, "OLD", DateTime.UtcNow, null, null));
            ApiResult = ApiCallResult.Success(200, apiXml, 1, TimeSpan.Zero);
        }
        public string RawRequest { get; }
        public string OriginalBaseline { get; }
        public DatabaseScenarioSnapshot Snapshot { get; set; }
        public ApiCallResult ApiResult { get; set; }
        public Exception? SnapshotException { get; set; }
        public XmlApiRequest? ApiRequest { get; private set; }
        public DatabaseComparisonPassCommand? PassCommand { get; private set; }
        public DatabaseComparisonFailCommand? FailCommand { get; private set; }
        public Exception? FailUpdateException { get; set; }
        public List<string> Events { get; } = [];
        public Task<DatabaseScenarioSnapshot> GetScenarioForComparisonAsync(string scenarioId, CancellationToken cancellationToken = default) => SnapshotException is null ? Task.FromResult(Snapshot) : Task.FromException<DatabaseScenarioSnapshot>(SnapshotException);
        public Task<IReadOnlyList<string>> GetAllRequestScenarioIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(new[] { "SCN" });
        public Task<DatabaseRequestRecord?> GetRequestAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseRequestRecord?>(Snapshot.Request);
        public Task<DatabaseResponseRecord?> GetResponseAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseResponseRecord?>(Snapshot.Response);
        public Task<IReadOnlyList<DatabaseDuplicateResult>> FindDuplicatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseDuplicateResult>>(Array.Empty<DatabaseDuplicateResult>());
        public Task InsertBaselineAsync(DatabaseInsertCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateBaselineAsync(DatabaseUpdateCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateTagsAsync(DatabaseTagsUpdateCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteObsoleteAsync(IReadOnlyList<string> scenarioIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DatabaseScenarioSelection>> SelectScenariosAsync(string? requestedTags, TagMatchMode matchMode, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseScenarioSelection>>(Array.Empty<DatabaseScenarioSelection>());
        public Task UpdateComparisonPassAsync(DatabaseComparisonPassCommand command, CancellationToken cancellationToken = default) { Events.Add("pass-update"); PassCommand = command; return Task.CompletedTask; }
        public Task UpdateComparisonFailAsync(DatabaseComparisonFailCommand command, CancellationToken cancellationToken = default) { Events.Add("fail-update"); if (FailUpdateException is not null) throw FailUpdateException; FailCommand = command; return Task.CompletedTask; }
        public ComparisonScenarioRoute Resolve(string schemeCode) => new(schemeCode, "EndpointA", new FuzzyPricingMatcher.Tests.Configuration.EndpointSettings { BaseUrl = "https://api.example.test", Resource = "/v1/{date}", Username = "__NOT_A_REAL_USERNAME__", Password = "__NOT_A_REAL_PASSWORD__", DatePlacement = "Path", DateParameterName = "date", DateFormat = "yyyy-MM-dd", Enabled = true }, new FuzzyPricingMatcher.Tests.Configuration.RouteSettings { EndpointName = "EndpointA", ResponseSchemaFile = "Scheme01-response.xsd", ResponseProcessorName = "PlaceholderResponseProcessor", Enabled = true });
        public Task<ApiCallResult> SendAsync(XmlApiRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Events.Add("api"); ApiRequest = request; return Task.FromResult(ApiResult); }
        public void Save(ScenarioEvidence evidence) { Events.Add(evidence.Outcome == "Started" ? "initial-evidence" : "final-evidence"); }
        public void Outcome(ComparisonResult result) => Events.Add("outcome");
        public void QuoteMismatch(string scenarioId, string extractedQuoteRef, string storedQuoteRef) => Events.Add("quote-mismatch");
    }

    private sealed class ComparisonRepositoryFake : IFuzzyMatcherRepository
    {
        public string? RequestedTags { get; private set; }
        public TagMatchMode RequestedMatchMode { get; private set; }
        public int MutationCount { get; private set; }
        public Task<IReadOnlyList<DatabaseScenarioSelection>> SelectScenariosAsync(string? requestedTags, TagMatchMode matchMode, CancellationToken cancellationToken = default) { RequestedTags = requestedTags; RequestedMatchMode = matchMode; return Task.FromResult<IReadOnlyList<DatabaseScenarioSelection>>(new[] { new DatabaseScenarioSelection("SCN/001", "quote-ref7CNE"), new DatabaseScenarioSelection("SCN002", "Q2") }); }
        public Task<IReadOnlyList<string>> GetAllRequestScenarioIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<DatabaseRequestRecord?> GetRequestAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseRequestRecord?>(null);
        public Task<DatabaseResponseRecord?> GetResponseAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<DatabaseResponseRecord?>(null);
        public Task<DatabaseScenarioSnapshot> GetScenarioForComparisonAsync(string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult(new DatabaseScenarioSnapshot(null, null));
        public Task<IReadOnlyList<DatabaseDuplicateResult>> FindDuplicatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DatabaseDuplicateResult>>(Array.Empty<DatabaseDuplicateResult>());
        public Task InsertBaselineAsync(DatabaseInsertCommand command, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
        public Task UpdateBaselineAsync(DatabaseUpdateCommand command, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
        public Task UpdateTagsAsync(DatabaseTagsUpdateCommand command, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
        public Task DeleteObsoleteAsync(IReadOnlyList<string> scenarioIds, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
        public Task UpdateComparisonPassAsync(DatabaseComparisonPassCommand command, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
        public Task UpdateComparisonFailAsync(DatabaseComparisonFailCommand command, CancellationToken cancellationToken = default) { MutationCount++; return Task.CompletedTask; }
    }
}
