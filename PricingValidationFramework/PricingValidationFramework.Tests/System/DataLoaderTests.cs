using System.Globalization;
using CsvHelper;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.DataLoader;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class DataLoaderTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"loader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public async Task Loader_should_insert_linked_requests_and_json_wrapped_responses_in_one_batch()
    {
        var requests = WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
            [["S1", "Q1", "HOME", "ABC", "<Request><Label>a,\"b\"\nnext</Label></Request>", "smoke"]]);
        var responses = WriteCsv("responses.csv", ["Scenario_id", "XML_Response", "Build_id", "Status"],
            [["S1", "{\"response\":\"<Response><Premium>10.00</Premium></Response>\"}", "", "PASS"]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, responses, false, BuildId: "build-42"));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(summary.InsertedResponses, Is.EqualTo(1));
            Assert.That(repository.Batches.Single().Requests.Single().QuoteRef, Is.EqualTo("Q1"));
            Assert.That(repository.Batches.Single().Responses.Single().BuildId, Is.EqualTo("build-42"));
            Assert.That(repository.Batches.Single().Responses.Single().XmlResponse, Is.EqualTo("<Response><Premium>10.00</Premium></Response>"));
        });
    }

    [Test]
    public async Task Validate_should_not_read_or_write_the_database()
    {
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), null, true));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Response_only_import_should_require_an_existing_request()
    {
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(null, ResponseFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("matching request"));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Identical_records_should_be_skipped_without_replacing_build_metadata()
    {
        var repository = new FakeRepository
        {
            Requests = [new("S1", "Q1", "HOME", "ABC", "<Request />", "smoke")],
            Responses = [new("S1", "<Response />", "previous-build", "PASS")]
        };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), ResponseFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(summary.SkippedResponses, Is.EqualTo(1));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Conflicting_baselines_should_reject_the_import_before_any_inserts()
    {
        var repository = new FakeRepository
        {
            Responses = [new("S1", "<DifferentResponse />", "previous-build", "PASS")]
        };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), ResponseFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("conflicts"));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Response_only_import_should_link_to_an_existing_request()
    {
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", "<Request />", "smoke")] };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(null, ResponseFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.Zero);
            Assert.That(summary.InsertedResponses, Is.EqualTo(1));
        });
    }

    [TestCase("")]
    [TestCase("unknown")]
    public async Task Responses_should_require_an_explicit_valid_status(string status)
    {
        var responses = WriteCsv("responses.csv", ["Scenario_id", "XML_Response", "Build_id", "Status"],
            [["S1", "<Response />", "build", status]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), responses, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("Status"));
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [TestCase("<Request>")]
    [TestCase("<!DOCTYPE Request [<!ENTITY external SYSTEM 'file:///outside'>]><Request>&external;</Request>")]
    public async Task Malformed_xml_and_external_dtds_should_be_rejected_before_database_access(string xml)
    {
        var requests = WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
            [["S1", "Q1", "HOME", "ABC", xml, "smoke"]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, null, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Duplicate_scenario_ids_should_be_rejected_case_insensitively(bool responseRows)
    {
        var path = responseRows
            ? WriteCsv("responses.csv", ["Scenario_id", "XML_Response", "Build_id", "Status"],
                [["S1", "<Response />", "build", "PASS"], ["s1", "<Response />", "build", "PASS"]])
            : WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
                [["S1", "Q1", "HOME", "ABC", "<Request />", ""], ["s1", "Q1", "HOME", "ABC", "<Request />", ""]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(
            new ImportOptions(responseRows ? null : path, responseRows ? path : null, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Issues.Single().Reason, Does.Contain("Duplicate"));
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task A_conflict_in_a_later_batch_should_prevent_all_database_writes()
    {
        var requests = WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
            [["S0", "Q0", "HOME", "ABC", "<Request />", ""], ["S1", "Q1", "HOME", "ABC", "<Request />", ""]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", "<DifferentRequest />", "")] };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, null, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.ReadCount, Is.EqualTo(2));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task A_failed_batch_should_report_previous_commits_without_exposing_exception_payloads()
    {
        var requests = WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
            [["S0", "Q0", "HOME", "ABC", "<Request />", ""], ["S1", "Q1", "HOME", "ABC", "<Request />", ""]]);
        var repository = new FakeRepository { FailOnBatch = 2 };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, null, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(summary.Issues.Single().Reason, Does.Not.Contain("sensitive-payload"));
        });
    }

    [Test]
    public async Task Cancellation_should_stop_the_loader_without_database_writes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), null, false), cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Cancelled, Is.True);
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(1001)]
    public void Invalid_batch_sizes_should_be_rejected(int batchSize)
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new ScenarioDataLoader().RunAsync(
            new ImportOptions(RequestFile(), null, true, batchSize)));
    }

    [Test]
    public async Task Loader_should_process_20000_linked_scenarios_in_bounded_batches()
    {
        var requests = WriteCsv("requests.csv", ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
            Enumerable.Range(0, 20000).Select(index => new[] { $"S{index:D5}", $"Q{index:D5}", "HOME", "ABC", "<Request />", "volume" }));
        var responses = WriteCsv("responses.csv", ["Scenario_id", "XML_Response", "Build_id", "Status"],
            Enumerable.Range(0, 20000).Select(index => new[] { $"S{index:D5}", "<Response />", "volume", "PASS" }));
        var repository = new FakeRepository();
        var elapsed = global::System.Diagnostics.Stopwatch.StartNew();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, responses, false, 500));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True, string.Join("; ", summary.Issues.Select(issue => issue.Reason)));
            Assert.That(summary.InsertedRequests, Is.EqualTo(20000));
            Assert.That(summary.InsertedResponses, Is.EqualTo(20000));
            Assert.That(summary.CommittedBatches, Is.EqualTo(40));
            Assert.That(repository.ReadCount, Is.EqualTo(40));
            Assert.That(repository.Batches.All(batch => batch.Requests.Count <= 500 && batch.Responses.Count <= 500), Is.True);
            Assert.That(repository.Batches.All(batch => batch.Requests.Select(row => row.ScenarioId)
                .SequenceEqual(batch.Responses.Select(row => row.ScenarioId))), Is.True);
        });
        TestContext.Out.WriteLine($"Validated and planned 20000 linked scenarios in {elapsed.Elapsed.TotalSeconds:F2} seconds using a fake repository; no live SQL timing is included.");
    }

    private string RequestFile() => WriteCsv("requests.csv",
        ["Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags"],
        [["S1", "Q1", "HOME", "ABC", "<Request />", "smoke"]]);

    private string ResponseFile() => WriteCsv("responses.csv",
        ["Scenario_id", "XML_Response", "Build_id", "Status"], [["S1", "<Response />", "build", "PASS"]]);

    private string WriteCsv(string name, string[] headers, IEnumerable<string[]> rows)
    {
        var path = Path.Combine(directory, name);
        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var field in headers)
        {
            csv.WriteField(field);
        }
        csv.NextRecord();
        foreach (var row in rows)
        {
            foreach (var field in row)
            {
                csv.WriteField(field);
            }
            csv.NextRecord();
        }
        return path;
    }

    private sealed class FakeRepository : IScenarioImportRepository
    {
        public IReadOnlyList<ScenarioRequestImport> Requests { get; init; } = [];
        public IReadOnlyList<ScenarioResponseImport> Responses { get; init; } = [];
        public int ReadCount { get; private set; }
        public List<ScenarioImportSnapshot> Batches { get; } = [];
        public int FailOnBatch { get; init; }
        private int insertAttempts;

        public Task<ScenarioImportSnapshot> ReadExistingAsync(IReadOnlyCollection<string> scenarioIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(new ScenarioImportSnapshot(Requests.Where(row => scenarioIds.Contains(row.ScenarioId)).ToArray(),
                Responses.Where(row => scenarioIds.Contains(row.ScenarioId)).ToArray()));
        }

        public Task InsertBatchAsync(IReadOnlyCollection<ScenarioRequestImport> requests,
            IReadOnlyCollection<ScenarioResponseImport> responses, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            insertAttempts++;
            if (insertAttempts == FailOnBatch)
            {
                throw new InvalidOperationException("sensitive-payload");
            }
            Batches.Add(new ScenarioImportSnapshot(requests.ToArray(), responses.ToArray()));
            return Task.CompletedTask;
        }
    }
}