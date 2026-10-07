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
    public async Task Loader_should_insert_requests_with_quoted_multiline_xml()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC", "<Label>a,\"b\"\nnext</Label>")]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var request = repository.Batches.Single().Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(request.QuoteRef, Is.EqualTo("Q1"));
            Assert.That(request.ProductCode, Is.EqualTo("HOME"));
            Assert.That(request.SchemeCode, Is.EqualTo("ABC"));
            Assert.That(request.TestTags, Is.Empty);
            Assert.That(request.XmlRequest.Replace("\r\n", "\n", StringComparison.Ordinal), Does.Contain("a,\"b\"\nnext"));
        });
    }

    [Test]
    public async Task Identifiers_should_be_read_from_the_policy_element_of_the_request_xml()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Message>\n   <Policy>\n      <PolicyReference> QU07446731 </PolicyReference>\n" +
            "      <ProductCode>CommercialVehicle_v2</ProductCode>\n      <SchemeCode>AcornInsureCV</SchemeCode>\n   </Policy>\n</Message>";
        var requests = WriteCsv("requests.csv", RequestHeaders, [["CV-1", xml]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var request = repository.Batches.Single().Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(request.QuoteRef, Is.EqualTo("QU07446731"));
            Assert.That(request.ProductCode, Is.EqualTo("CommercialVehicle_v2"));
            Assert.That(request.SchemeCode, Is.EqualTo("AcornInsureCV"));
            Assert.That(request.XmlRequest, Does.StartWith("<Message>"));
        });
    }

    [TestCase("<Message><Policy><ProductCode>P</ProductCode><SchemeCode>S</SchemeCode></Policy></Message>", "/Message/Policy/PolicyReference")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><SchemeCode>S</SchemeCode></Policy></Message>", "/Message/Policy/ProductCode")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><ProductCode>P</ProductCode></Policy></Message>", "/Message/Policy/SchemeCode")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><ProductCode>P</ProductCode><SchemeCode> </SchemeCode></Policy></Message>", "SchemeCode (Scheme_code) is required")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><PolicyReference>Q2</PolicyReference><ProductCode>P</ProductCode><SchemeCode>S</SchemeCode></Policy></Message>", "more than one /Message/Policy/PolicyReference")]
    [TestCase("<Message />", "/Message/Policy")]
    [TestCase("<Request />", "root element must be <Message>")]
    public async Task Requests_missing_an_identifier_should_be_rejected_before_database_access(string xml, string expectedReason)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", xml]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain(expectedReason));
            Assert.That(repository.ReadCount, Is.Zero);
        });
    }

    [Test]
    public async Task A_folder_should_load_the_newest_versioned_csv_by_its_filename_timestamp()
    {
        WriteCsv("scenarios-20260923-092037-291.csv", RequestHeaders, [["S-OLD", PolicyXml("Q1", "HOME", "ABC")]]);
        var newest = WriteCsv("scenarios-20260923-092114-865.csv", RequestHeaders, [["S-NEW", PolicyXml("Q2", "HOME", "ABC")]]);
        WriteCsv("scenarios.csv", RequestHeaders, [["S-UNVERSIONED", PolicyXml("Q3", "HOME", "ABC")]]);
        WriteCsv("requests.csv", RequestHeaders, [["S-FALLBACK", PolicyXml("Q4", "HOME", "ABC")]]);
        // Modified times must not matter: make the newest-named file look oldest on disk.
        File.SetLastWriteTimeUtc(newest, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(directory, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestFile, Is.EqualTo(newest));
            Assert.That(repository.Batches.Single().Single().ScenarioId, Is.EqualTo("S-NEW"));
        });
    }

    [Test]
    public async Task A_folder_without_versioned_csvs_should_fall_back_to_requests_csv()
    {
        var requests = RequestFile();
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(directory, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestFile, Is.EqualTo(requests));
        });
    }

    [Test]
    public async Task A_file_path_should_be_used_as_given()
    {
        var older = WriteCsv("scenarios-20260101-000000-000.csv", RequestHeaders, [["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        WriteCsv("scenarios-20260923-092114-865.csv", RequestHeaders, [["S2", PolicyXml("Q2", "HOME", "ABC")]]);

        var summary = await new ScenarioDataLoader(new FakeRepository()).RunAsync(new ImportOptions(older, true));

        Assert.That(summary.RequestFile, Is.EqualTo(older));
    }

    [TestCase(true, "No scenarios-{timestamp}.csv or requests.csv was found")]
    [TestCase(false, "was not found")]
    public async Task A_folder_with_no_csv_or_a_missing_path_should_be_reported(bool folderExists, string expectedReason)
    {
        var path = folderExists ? directory : Path.Combine(directory, "missing");
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(path, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain(expectedReason));
            Assert.That(repository.ReadCount, Is.Zero);
        });
    }

    [Test]
    public async Task Validate_should_not_read_or_write_the_database()
    {
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), true));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Identical_requests_should_be_skipped_even_when_the_stored_row_has_tags()
    {
        var repository = new FakeRepository
        {
            Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "smoke")]
        };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Changed_xml_should_update_the_request_and_preserve_existing_tags()
    {
        var oldXml = PolicyXml("Q1", "HOME", "ABC", "<Label>bob</Label>");
        var newXml = PolicyXml("Q2", "HOME", "ABC", "<Label>tom</Label>");
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", newXml]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", oldXml, "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.Zero);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(repository.Batches, Is.Empty);
            Assert.That(repository.Requests.Single().XmlRequest, Does.Contain("<Label>tom</Label>"));
            Assert.That(repository.Requests.Single().QuoteRef, Is.EqualTo("Q2"));
            Assert.That(repository.Requests.Single().TestTags, Is.EqualTo("smoke"));
        });

        var rerun = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        Assert.That(rerun.SkippedRequests, Is.EqualTo(1));
        Assert.That(rerun.UpdatedRequests, Is.Zero);
        Assert.That(repository.WriteCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Mixed_inserts_updates_and_skips_should_use_one_write_batch()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")],
             ["S1", PolicyXml("Q1", "HOME", "ABC", "<Label>tom</Label>")],
             ["s2", PolicyXml("Q2", "HOME", "ABC")]]);
        var repository = new FakeRepository
        {
            Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC", "<Label>bob</Label>"), "smoke"),
                new("S2", "Q2", "HOME", "ABC", PolicyXml("Q2", "HOME", "ABC"), "regression")]
        };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(repository.WriteCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_large_multiline_request_should_update_without_truncation()
    {
        var label = string.Join("\n", Enumerable.Repeat(new string('x', 100), 460));
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC", $"<Label>{label}</Label>")]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var document = global::System.Xml.Linq.XElement.Parse(repository.Requests.Single().XmlRequest);

        Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
        Assert.That(document.Element("Policy")!.Element("Label")!.Value, Is.EqualTo(label));
    }

    [Test]
    public async Task A_later_write_failure_should_report_only_committed_updates()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC", "<Label>tom</Label>")],
             ["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository
        {
            Requests = [new("S0", "Q0", "HOME", "ABC", PolicyXml("Q0", "HOME", "ABC", "<Label>bob</Label>"), "smoke")],
            FailOnBatch = 2
        };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.InsertedRequests, Is.Zero);
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(repository.Requests.Single().XmlRequest, Does.Contain("<Label>tom</Label>"));
        });
    }

    [TestCase("MOTOR", "ABC")]
    [TestCase("HOME", "XYZ")]
    [TestCase("home", "ABC")]
    public async Task Product_or_scheme_changes_should_reject_the_import(string productCode, string schemeCode)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", PolicyXml("Q1", productCode, schemeCode)]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("product or scheme"));
            Assert.That(repository.WriteCount, Is.Zero);
        });
    }

    [TestCase("<Request>")]
    [TestCase("<!DOCTYPE Request [<!ENTITY external SYSTEM 'file:///outside'>]><Request>&external;</Request>")]
    public async Task Malformed_xml_and_external_dtds_should_be_rejected_before_database_access(string xml)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", xml]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Duplicate_scenario_ids_should_be_rejected_case_insensitively()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC")], ["s1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

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
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")], ["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "MOTOR", "ABC", PolicyXml("Q1", "MOTOR", "ABC"), "")] };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("conflicts"));
            Assert.That(repository.ReadCount, Is.EqualTo(2));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task A_failed_batch_should_report_previous_commits_without_exposing_exception_payloads()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")], ["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository { FailOnBatch = 2 };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

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
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), false), cancellation.Token);

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
            new ImportOptions(RequestFile(), true, batchSize)));
    }

    [Test]
    public async Task Loader_should_process_20000_scenarios_in_bounded_batches()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            Enumerable.Range(0, 20000).Select(index => new[] { $"S{index:D5}", PolicyXml($"Q{index:D5}", "HOME", "ABC") }));
        var repository = new FakeRepository();
        var elapsed = global::System.Diagnostics.Stopwatch.StartNew();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 500));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True, string.Join("; ", summary.Issues.Select(issue => issue.Reason)));
            Assert.That(summary.InsertedRequests, Is.EqualTo(20000));
            Assert.That(summary.CommittedBatches, Is.EqualTo(40));
            Assert.That(repository.ReadCount, Is.EqualTo(40));
            Assert.That(repository.Batches.All(batch => batch.Count <= 500), Is.True);
        });
        TestContext.Out.WriteLine($"Validated and planned 20000 scenarios in {elapsed.Elapsed.TotalSeconds:F2} seconds using a fake repository; no live SQL timing is included.");
    }

    private static readonly string[] RequestHeaders = ["scenario_id", "xml"];

    // Same /Message/Policy layout as the real request XMLs (e.g. CV-Request-updated.xml).
    private static string PolicyXml(string quoteRef, string productCode, string schemeCode, string extra = "") =>
        $"<Message><Policy><PolicyReference>{quoteRef}</PolicyReference><ProductCode>{productCode}</ProductCode>" +
        $"<SchemeCode>{schemeCode}</SchemeCode>{extra}</Policy></Message>";

    private string RequestFile() => WriteCsv("requests.csv", RequestHeaders, [["S1", PolicyXml("Q1", "HOME", "ABC")]]);

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
        private readonly List<ScenarioRequestImport> storedRequests = [];
        public IReadOnlyList<ScenarioRequestImport> Requests
        {
            get => storedRequests;
            init => storedRequests.AddRange(value);
        }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public List<IReadOnlyList<ScenarioRequestImport>> Batches { get; } = [];
        public int FailOnBatch { get; init; }

        public Task<ScenarioImportSnapshot> ReadExistingAsync(IReadOnlyCollection<string> scenarioIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(new ScenarioImportSnapshot(Requests.Where(row => scenarioIds.Contains(row.ScenarioId, StringComparer.OrdinalIgnoreCase)).ToArray()));
        }

        public Task<ScenarioImportBatchResult> ApplyBatchAsync(
            IReadOnlyCollection<ScenarioRequestImport> inserts,
            IReadOnlyCollection<ScenarioRequestImport> updates,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteCount + 1 == FailOnBatch)
            {
                throw new InvalidOperationException("sensitive-payload");
            }
            foreach (var update in updates)
            {
                var index = storedRequests.FindIndex(row => string.Equals(row.ScenarioId, update.ScenarioId, StringComparison.OrdinalIgnoreCase));
                storedRequests[index] = storedRequests[index] with { XmlRequest = update.XmlRequest, QuoteRef = update.QuoteRef };
            }
            if (inserts.Count > 0)
            {
                Batches.Add(inserts.ToArray());
                storedRequests.AddRange(inserts);
            }
            WriteCount++;
            return Task.FromResult(new ScenarioImportBatchResult(inserts.Count, updates.Count));
        }
    }
}
