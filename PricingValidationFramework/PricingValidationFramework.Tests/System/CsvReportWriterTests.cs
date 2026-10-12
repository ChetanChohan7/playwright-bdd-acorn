using System.Globalization;
using CsvHelper;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Reporting;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class CsvReportWriterTests
{
    private const string Header = "ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml";

    [Test]
    public async Task Five_fields_with_three_failures_should_produce_one_flat_report_with_xml_once_per_scenario()
    {
        var output = Path.Combine(Path.GetTempPath(), $"flat-report-{Guid.NewGuid():N}.csv");
        var profile = new PricingProfile
        {
            ProfileId = "Route001",
            ProductCode = "HOME",
            SchemeCodes = ["ABC"],
            IceField = "grossPremium",
            Fields = new Dictionary<string, string>
            {
                ["grossPremium"] = "/Response/Totals/GrossPremium",
                ["tax"] = "/Response/Totals/Tax",
                ["netPremium"] = "/Response/Totals/NetPremium",
                ["fee"] = "/Response/Totals/Fee",
                ["discount"] = "/Response/Totals/Discount"
            }
        };
        const string baseline = "<Response><Totals><GrossPremium>125.00</GrossPremium><Tax>20.00</Tax><NetPremium>100.00</NetPremium><Fee>5.00</Fee><Discount>10.00</Discount></Totals></Response>";
        const string response = "<Response><Totals><GrossPremium>127.00</GrossPremium><Tax>21.00</Tax><NetPremium>99.00</NetPremium><Fee>5.25</Fee><Discount>10.00</Discount></Totals></Response>";
        const string request = "<Request quote=\"Q-1001\">a,b</Request>";
        var comparison = new PricingComparisonService([profile]).Compare("HOME", "ABC", baseline, response, -0.50m, 0.50m);
        var first = RadarReportingHelper.BuildComparisonRow("test", "SC-001", "Q-1001", "ABC", "HOME",
            request, baseline, response, -0.50m, 0.50m, comparison);
        var second = RadarReportingHelper.BuildComparisonRow("test", "SC-002", "Q-1002", "ABC", "HOME",
            "<Request/>", baseline, response, -0.50m, 0.50m, comparison);
        try
        {
            await new CsvReportWriter().WriteRadarReportAsync(output, "test", [second, first]);
            var records = ReadCsv(await File.ReadAllTextAsync(output));
            Assert.Multiple(() =>
            {
                Assert.That(records, Has.Count.EqualTo(11));
                Assert.That(records[0], Is.EqualTo(Header.Split(',')));
                Assert.That(records.All(record => record.Length == 19), Is.True);
                Assert.That(records.Skip(1).Take(5).Select(record => record[0]), Is.All.EqualTo("SC-001"));
                Assert.That(records.Skip(6).Select(record => record[0]), Is.All.EqualTo("SC-002"));
                Assert.That(records.Skip(1).Take(5).Count(record => record[12] == "FAIL"), Is.EqualTo(3));
                Assert.That(records.Skip(1).Take(5).Count(record => record[12] == "PASS"), Is.EqualTo(2));
                Assert.That(records.Skip(1).Select(record => record[13]), Is.All.EqualTo("FAIL"));
                Assert.That(records[3].Take(16), Is.EqualTo(new[]
                {
                    "SC-001", "Q-1001", "HOME", "ABC", "Route001", "grossPremium", "/Response/Totals/GrossPremium",
                    "125.00", "127.00", "2.00", "-0.50", "0.50", "FAIL", "FAIL", "", ""
                }));
                Assert.That(records[4][9], Is.EqualTo("-1.00"));
                Assert.That(records[1].Skip(16), Is.EqualTo(new[] { request, baseline, response }));
                Assert.That(records[6].Skip(16), Is.EqualTo(new[] { "<Request/>", baseline, response }));
                Assert.That(records.Skip(2).Take(4).SelectMany(record => record.Skip(16)), Is.All.Empty);
                Assert.That(records.Skip(7).SelectMany(record => record.Skip(16)), Is.All.Empty);
            });
        }
        finally { File.Delete(output); }
    }

    [TestCase(ScenarioResult.Error, "RequestExecution", "Request failed, please retry.")]
    [TestCase(ScenarioResult.Pass, "BaselineCreated", null)]
    public async Task Scenarios_without_comparisons_should_still_have_one_outcome_row(ScenarioResult result, string stage, string? error)
    {
        var output = Path.Combine(Path.GetTempPath(), $"outcome-{Guid.NewGuid():N}.csv");
        try
        {
            await new CsvReportWriter().WriteRadarReportAsync(output, "test", [new RadarValidationReportRow
            {
                ScenarioId = "S1", Result = result, FailureStage = stage, Error = error, RequestXml = "<Request/>"
            }]);
            var records = ReadCsv(await File.ReadAllTextAsync(output));
            Assert.Multiple(() =>
            {
                Assert.That(records, Has.Count.EqualTo(2));
                Assert.That(records[1], Has.Length.EqualTo(19));
                Assert.That(records[1].Skip(5).Take(5), Is.All.Empty);
                Assert.That(records[1][12], Is.Empty);
                Assert.That(records[1][13], Is.EqualTo(result == ScenarioResult.Pass ? "PASS" : "ERROR"));
                Assert.That(records[1][14], Is.EqualTo(stage));
                Assert.That(records[1][15], Is.EqualTo(error ?? ""));
                Assert.That(records[1][16], Is.EqualTo("<Request/>"));
            });
        }
        finally { File.Delete(output); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Reports_should_quote_special_fields_and_preserve_line_breaks(bool ice)
    {
        var output = Path.Combine(Path.GetTempPath(), $"quoted-report-{Guid.NewGuid():N}.csv");
        var values = new[] { "plain", "", "comma,value", "say \"hi\"", "carriage\rreturn", "line\nfeed", "line\r\nbreak", "a,\"b\"\r\nc" };
        try
        {
            var writer = new CsvReportWriter();
            if (ice)
            {
                await writer.WriteIceReportAsync(output, "build", values.Select((value, index) =>
                    new IceValidationReportRow { ScenarioId = $"S{index}", QuoteRef = value, Result = ScenarioResult.Pass }).ToArray());
            }
            else
            {
                await writer.WriteRadarReportAsync(output, "build", values.Select((value, index) =>
                    new RadarValidationReportRow { ScenarioId = $"S{index}", QuoteRef = value, Result = ScenarioResult.Pass }).ToArray());
            }
            var records = ReadCsv(await File.ReadAllTextAsync(output));
            Assert.That(records, Has.Count.EqualTo(9));
            Assert.That(records.Skip(1).Select(record => record[ice ? 2 : 1]), Is.EqualTo(values));
            Assert.That(records.Skip(1).Select(record => record[ice ? 7 : 13]), Is.All.EqualTo("PASS"));
        }
        finally { File.Delete(output); }
    }

    [Test]
    public async Task Ice_report_should_include_selected_field_and_extraction_error()
    {
        var output = Path.Combine(Path.GetTempPath(), $"ice-report-{Guid.NewGuid():N}.csv");
        try
        {
            await new CsvReportWriter().WriteIceReportAsync(output, "build", [new IceValidationReportRow
            {
                ScenarioId = "S1", ProfileId = "HOME", FieldName = "premium", XPath = "/Response/Premium",
                Error = "Field 'premium' did not match.", Result = ScenarioResult.Error
            }]);
            var records = ReadCsv(await File.ReadAllTextAsync(output));
            Assert.That(records[0], Has.Length.EqualTo(12));
            Assert.That(records[1].Skip(7), Is.EqualTo(new[]
            {
                "ERROR", "HOME", "premium", "/Response/Premium", "Field 'premium' did not match."
            }));
        }
        finally { File.Delete(output); }
    }

    private static List<string[]> ReadCsv(string content)
    {
        using var reader = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
        var records = new List<string[]>();
        while (reader.Read())
            records.Add(Enumerable.Range(0, reader.Parser.Count).Select(index => reader.GetField(index)!).ToArray());
        return records;
    }
}
