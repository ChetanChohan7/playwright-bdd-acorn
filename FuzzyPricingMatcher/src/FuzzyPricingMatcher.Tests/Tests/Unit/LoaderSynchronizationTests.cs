using System.Xml.Linq;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Processing;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class LoaderSynchronizationTests
{
    [Test]
    [Category("Unit")]
    public void New_scenario_passes_raw_xml_validates_before_insert_and_requires_api()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference><Amount>9</Amount></Request>";
        var fakes = new LoaderFakes();
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "smoke") }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.Inserted));
            Assert.That(result.ScenarioResults.Single().ApiCalled, Is.True);
            Assert.That(fakes.ApiRequests.Single().RawXml, Is.EqualTo(raw));
            Assert.That(fakes.Events, Is.EqualTo(new[] { "api", "validate", "insert" }));
            Assert.That(fakes.InsertCommands.Single().RawXml, Is.EqualTo(raw));
        });
    }

    [Test]
    [Category("Unit")]
    public void Changed_xml_updates_request_and_response_after_validation()
    {
        var oldRaw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-old</PolicyReference><Amount>1</Amount></Request>";
        var newRaw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-new</PolicyReference><Amount>2</Amount></Request>";
        var fakes = new LoaderFakes { Snapshot = Snapshot(oldRaw, "Q-old", "smoke") };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(newRaw, 2, "smoke") }, "BUILD-2");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.XmlUpdated));
            Assert.That(fakes.ApiRequests.Single().RawXml, Is.EqualTo(newRaw));
            Assert.That(fakes.UpdateCommands.Single().QuoteRef, Is.EqualTo("Q-new"));
            Assert.That(fakes.Events, Is.EqualTo(new[] { "api", "validate", "update" }));
        });
    }

    [Test]
    [Category("Unit")]
    public void Tags_only_updates_tags_without_api_or_response_mutation()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var fakes = new LoaderFakes { Snapshot = Snapshot(raw, "Q-1", "smoke") };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "pricing") }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.TagsUpdated));
            Assert.That(result.ScenarioResults.Single().ApiCalled, Is.False);
            Assert.That(fakes.ApiRequests, Is.Empty);
            Assert.That(fakes.UpdateTagsCalls, Has.Count.EqualTo(1));
            Assert.That(fakes.UpdateCommands, Is.Empty);
        });
    }

    [Test]
    [Category("Unit")]
    public void Unchanged_scenario_requires_no_api_or_mutation()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var fakes = new LoaderFakes { Snapshot = Snapshot(raw, "Q-1", "smoke") };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "smoke") }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.Unchanged));
            Assert.That(result.ScenarioResults.Single().Successful, Is.True);
            Assert.That(fakes.ApiRequests, Is.Empty);
            Assert.That(fakes.InsertCommands, Is.Empty);
            Assert.That(fakes.UpdateCommands, Is.Empty);
        });
    }

    [Test]
    [Category("Unit")]
    public void Api_or_validation_failure_preserves_existing_data_and_skips_deletion()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference><Amount>1</Amount></Request>";
        var fakes = new LoaderFakes { ApiException = new LoaderApiException("API failed"), Snapshot = new BaselineDatabaseSnapshot(Array.Empty<ExistingRequestRow>(), Array.Empty<ExistingResponseRow>()) };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "smoke") }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.ApiFailed));
            Assert.That(result.DeletionSkippedReason, Is.Not.Empty);
            Assert.That(fakes.InsertCommands, Is.Empty);
            Assert.That(fakes.UpdateCommands, Is.Empty);
            Assert.That(fakes.Evidence, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("Unit")]
    public void Duplicate_rows_are_failed_with_all_rows_while_unique_rows_continue()
    {
        var duplicate = Prepared("<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>", 2, "smoke", "SCN");
        var duplicateTwo = Prepared(duplicate.CsvRow.XmlRequest, 3, "smoke", "scn");
        var unique = Prepared("<Request><SchemeCode>S-2</SchemeCode><PolicyReference>Q-2</PolicyReference></Request>", 4, "smoke", "UNIQUE");
        var fakes = new LoaderFakes();
        var result = CreateService(fakes).Synchronize(new[] { duplicate, duplicateTwo, unique }, "BUILD-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Where(item => item.ScenarioId.Equals("SCN", StringComparison.OrdinalIgnoreCase)), Has.All.Matches<LoaderScenarioResult>(item => item.Outcome == LoaderScenarioOutcome.DuplicateScenarioId));
            Assert.That(result.ScenarioResults.Single(item => item.ScenarioId == "UNIQUE").Outcome, Is.EqualTo(LoaderScenarioOutcome.Inserted));
            Assert.That(result.ScenarioResults.First(item => item.ScenarioId.Equals("SCN", StringComparison.OrdinalIgnoreCase)).Error, Does.Contain("2, 3"));
            Assert.That(fakes.ApiRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    [Category("Unit")]
    public void Database_duplicates_are_not_modified()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var fakes = new LoaderFakes { Snapshot = new BaselineDatabaseSnapshot(new[] { new ExistingRequestRow("SCN", "Q", raw, new[] { "smoke" }), new ExistingRequestRow("SCN", "Q2", raw, new[] { "smoke" }) }, Array.Empty<ExistingResponseRow>()) };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "smoke", "SCN") }, "BUILD-1");
        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Single().Outcome, Is.EqualTo(LoaderScenarioOutcome.DatabaseConsistencyFailed));
            Assert.That(fakes.ApiRequests, Is.Empty);
            Assert.That(fakes.UpdateTagsCalls, Is.Empty);
        });
    }

    [Test]
    [Category("Unit")]
    public void Obsolete_deletion_is_requested_only_after_successful_rows()
    {
        var raw = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>Q-1</PolicyReference></Request>";
        var fakes = new LoaderFakes { AllIds = new[] { "SCN", "OBSOLETE" } };
        var result = CreateService(fakes).Synchronize(new[] { Prepared(raw, 2, "smoke", "SCN") }, "BUILD-1");
        Assert.Multiple(() =>
        {
            Assert.That(result.ScenarioResults.Any(item => item.Outcome == LoaderScenarioOutcome.Deleted), Is.True);
            Assert.That(fakes.DeleteCommands.Single().ScenarioIds, Is.EqualTo(new[] { "OBSOLETE" }));
            Assert.That(fakes.Events, Does.Contain("delete-response-before-request"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Summary_contains_the_required_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"loader-summary-{Guid.NewGuid():N}.txt");
        try
        {
            var result = new LoaderSynchronizationResult(new[] { new LoaderScenarioResult { ScenarioId = "SCN", QuoteRef = "Q", Successful = true, Outcome = LoaderScenarioOutcome.Unchanged, ApiCalled = false, RequestTableAction = "None", ResponseTableAction = "None" } });
            new LoaderSummaryWriter().Write(path, result);
            var line = File.ReadAllText(path);
            Assert.That(line, Does.Contain("SCN | Q | True | Unchanged | False | None | None"));
        }
        finally { File.Delete(path); }
    }

    private static LoaderSynchronizationService CreateService(LoaderFakes fakes) => new(fakes, fakes, fakes, fakes, fakes);

    private static PreparedBaselineScenario Prepared(string raw, int row, string tags, string scenarioId = "SCN")
    {
        var metadata = new RequestXmlMetadataReader().Read(raw);
        return new PreparedBaselineScenario { CsvRow = new BaselineScenarioCsvRow { RowNumber = row, ScenarioId = scenarioId, XmlRequest = raw, TestTags = tags }, NormalizedScenarioId = scenarioId.Trim(), RequestMetadata = metadata, NormalizedTags = new TagNormalizer().Normalize(tags), XmlFingerprint = new XmlFingerprintService().CreateFingerprint(metadata.Document) };
    }

    private static BaselineDatabaseSnapshot Snapshot(string raw, string quoteRef, string tags) => new(new[] { new ExistingRequestRow("SCN", quoteRef, raw, new TagNormalizer().Normalize(tags)) }, new[] { new ExistingResponseRow("SCN", quoteRef, "<response />", "BUILD-OLD", null) });

    private sealed class LoaderFakes : ILoaderRepository, ILoaderRouteResolver, ILoaderApiClient, ILoaderResponseValidator, ILoaderEvidenceWriter
    {
        public BaselineDatabaseSnapshot Snapshot { get; set; } = new(Array.Empty<ExistingRequestRow>(), Array.Empty<ExistingResponseRow>());
        public string[] AllIds { get; set; } = Array.Empty<string>();
        public LoaderApiException? ApiException { get; set; }
        public List<LoaderApiRequest> ApiRequests { get; } = [];
        public List<InsertBaselineScenarioCommand> InsertCommands { get; } = [];
        public List<UpdateBaselineScenarioCommand> UpdateCommands { get; } = [];
        public List<string> UpdateTagsCalls { get; } = [];
        public List<DeleteObsoleteScenarioCommand> DeleteCommands { get; } = [];
        public List<LoaderEvidence> Evidence { get; } = [];
        public List<string> Events { get; } = [];
        public BaselineDatabaseSnapshot GetByScenarioId(string scenarioId) => Snapshot;
        public IReadOnlyList<string> GetAllScenarioIds() => AllIds;
        public void Insert(InsertBaselineScenarioCommand command) { Events.Add("insert"); InsertCommands.Add(command); }
        public void Update(UpdateBaselineScenarioCommand command) { Events.Add("update"); UpdateCommands.Add(command); }
        public void UpdateTags(string scenarioId, IReadOnlyList<string> normalizedTags) { Events.Add("update-tags"); UpdateTagsCalls.Add(scenarioId); }
        public void DeleteObsolete(DeleteObsoleteScenarioCommand command) { Events.Add("delete-response-before-request"); DeleteCommands.Add(command); }
        public RouteDefinition Resolve(string schemeCode) => new(schemeCode, "EndpointA");
        public LoaderApiResponse Fetch(LoaderApiRequest request, RouteDefinition route) { Events.Add("api"); if (ApiException is not null) throw ApiException; ApiRequests.Add(request); return new LoaderApiResponse("<response><PlaceholderAmount>1</PlaceholderAmount></response>"); }
        public void Validate(LoaderApiResponse response, RouteDefinition route) => Events.Add("validate");
        public void Save(LoaderEvidence evidence) => Evidence.Add(evidence);
    }
}
