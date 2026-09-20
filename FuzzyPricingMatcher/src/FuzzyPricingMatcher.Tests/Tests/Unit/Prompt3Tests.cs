using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Models;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Validation;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class Prompt3Tests
{
    private static readonly string ValidRequest = "<Request><SchemeCode>SCHEME_01</SchemeCode><PolicyReference>POL-1</PolicyReference><Value>1</Value></Request>";

    [Test]
    [Category("Unit")]
    public void Csv_reader_handles_quoted_multiline_xml_commas_quotes_and_tags()
    {
        var xml = "<Request><SchemeCode>S-1</SchemeCode><PolicyReference>P-1</PolicyReference><Text>comma, and \"quote\"\nline</Text></Request>";
        var path = WriteCsv($"Scenario_id,XML_request,Test_tags\nSCN-1,\"{xml.Replace("\"", "\"\"")}\",\"smoke, pricing,smoke\"\n");
        try
        {
            var record = new CsvScenarioReader().Read(path).Single();
            Assert.Multiple(() =>
            {
                Assert.That(record.CsvRow.XmlRequest, Does.Contain("comma, and \"quote\""));
                Assert.That(record.CsvRow.XmlRequest, Does.Contain("\nline"));
                Assert.That(record.NormalizedTags, Is.EqualTo(new[] { "pricing", "smoke" }));
            });
        }
        finally { File.Delete(path); }
    }

    [TestCase("Scenario_id,Test_tags", "XML_request")]
    [TestCase("Scenario_id,XML_request", "Test_tags")]
    [TestCase("XML_request,Test_tags", "Scenario_id")]
    [Category("Unit")]
    public void Csv_reader_rejects_missing_headers(string headers, string missingHeader)
    {
        var path = WriteCsv(headers + "\n");
        try { Assert.That(() => new CsvScenarioReader().Read(path), Throws.TypeOf<CsvValidationException>().With.Message.Contains(missingHeader)); }
        finally { File.Delete(path); }
    }

    [Test]
    [Category("Unit")]
    public void Csv_reader_preserves_row_numbers_and_rejects_missing_values()
    {
        var path = WriteCsv("Scenario_id,XML_request,Test_tags\n,\"" + ValidRequest + "\",smoke\n");
        try { Assert.That(() => new CsvScenarioReader().Read(path), Throws.TypeOf<CsvValidationException>().With.Message.Contains("row 2")); }
        finally { File.Delete(path); }
    }

    [Test]
    [Category("Unit")]
    public void Headings_only_template_is_allowed_for_reading_but_not_live_loading()
    {
        var path = WriteCsv("Scenario_id,XML_request,Test_tags\n");
        try
        {
            var reader = new CsvScenarioReader();
            Assert.That(reader.Read(path), Is.Empty);
            Assert.That(() => reader.Read(path, requireDataRows: true), Throws.TypeOf<CsvValidationException>());
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Category("Unit")]
    public void Scenario_ids_are_trimmed_and_duplicates_include_all_rows_while_unique_rows_remain()
    {
        var records = new[]
        {
            new BaselineScenarioCsvRow { ScenarioId = " SCN-1001 ", RowNumber = 2 },
            new BaselineScenarioCsvRow { ScenarioId = "scn-1001", RowNumber = 3 },
            new BaselineScenarioCsvRow { ScenarioId = "SCN-1002", RowNumber = 4 }
        };
        var duplicates = new DuplicateScenarioDetector().Detect(records);
        Assert.Multiple(() =>
        {
            Assert.That(new ScenarioIdNormalizer().Normalize("  SCN-1001  "), Is.EqualTo("SCN-1001"));
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0].NormalizedScenarioId, Is.EqualTo("SCN-1001"));
            Assert.That(duplicates[0].RowNumbers, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(records.Select(record => record.ScenarioId), Does.Contain("SCN-1002"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Empty_scenario_ids_are_rejected()
    {
        Assert.That(() => new ScenarioIdNormalizer().Normalize("  "), Throws.TypeOf<CsvValidationException>());
    }

    [Test]
    [Category("Unit")]
    public void Request_metadata_supports_namespaces_preserves_raw_xml_and_rejects_invalid_forms()
    {
        var raw = "<r:Request xmlns:r=\"urn:test\"><r:SchemeCode>S-1</r:SchemeCode><r:PolicyReference>P-1</r:PolicyReference></r:Request>";
        var metadata = new RequestXmlMetadataReader().Read(raw);
        Assert.Multiple(() =>
        {
            Assert.That(metadata.SchemeCode, Is.EqualTo("S-1"));
            Assert.That(metadata.QuoteReference, Is.EqualTo("P-1"));
            Assert.That(metadata.Document.ToString(SaveOptions.DisableFormatting), Does.Contain("urn:test"));
        });
        Assert.That(() => new RequestXmlMetadataReader().Read("<Request><SchemeCode>"), Throws.TypeOf<RequestXmlValidationException>().With.Message.Contains("line"));
        Assert.That(() => new RequestXmlMetadataReader().Read("<!DOCTYPE Request [<!ELEMENT Request ANY>]><Request />"), Throws.TypeOf<RequestXmlValidationException>());
        Assert.That(() => new RequestXmlMetadataReader().Read("<Request><PolicyReference>P</PolicyReference></Request>"), Throws.TypeOf<MissingSchemeCodeException>());
        Assert.That(() => new RequestXmlMetadataReader().Read("<Request><SchemeCode>S</SchemeCode><SchemeCode>S2</SchemeCode><PolicyReference>P</PolicyReference></Request>"), Throws.TypeOf<DuplicateXmlMetadataElementException>());
        Assert.That(() => new RequestXmlMetadataReader().Read("<Request><SchemeCode>S</SchemeCode><PolicyReference></PolicyReference></Request>"), Throws.TypeOf<MissingPolicyReferenceException>());
    }

    [Test]
    [Category("Unit")]
    public void Tags_are_case_insensitive_sorted_unique_and_empty_safe()
    {
        var tags = new TagNormalizer().Normalize("smoke, pricing,Smoke,,PRICING");
        Assert.Multiple(() =>
        {
            Assert.That(tags, Is.EqualTo(new[] { "pricing", "smoke" }));
            Assert.That(new TagNormalizer().Normalize(null), Is.Empty);
        });
    }

    [Test]
    [Category("Unit")]
    public void Fingerprints_ignore_indentation_but_detect_values_elements_and_namespaces()
    {
        var service = new XmlFingerprintService();
        var first = XDocument.Parse("<Request><SchemeCode>S</SchemeCode><PolicyReference>P</PolicyReference><Value>1</Value></Request>");
        var formatted = XDocument.Parse("<Request>\n  <SchemeCode>S</SchemeCode>\n  <PolicyReference>P</PolicyReference>\n  <Value>1</Value>\n</Request>");
        Assert.Multiple(() =>
        {
            Assert.That(service.CreateFingerprint(first), Is.EqualTo(service.CreateFingerprint(formatted)));
            Assert.That(service.CreateFingerprint(first), Is.Not.EqualTo(service.CreateFingerprint(XDocument.Parse(first.ToString().Replace(">1<", ">2<")))));
            Assert.That(service.CreateFingerprint(first), Is.Not.EqualTo(service.CreateFingerprint(XDocument.Parse("<x:Request xmlns:x=\"urn:other\"><x:SchemeCode>S</x:SchemeCode><x:PolicyReference>P</x:PolicyReference><x:Value>1</x:Value></x:Request>"))));
        });
    }

    [Test]
    [Category("Unit")]
    public void Change_detector_reports_all_outcomes_and_api_requirements()
    {
        var metadata = new RequestXmlMetadataReader().Read(ValidRequest);
        var current = new PreparedBaselineScenario { CsvRow = new BaselineScenarioCsvRow { XmlRequest = ValidRequest }, NormalizedScenarioId = "S", RequestMetadata = metadata, NormalizedTags = new[] { "smoke" }, XmlFingerprint = new XmlFingerprintService().CreateFingerprint(metadata.Document) };
        var detector = new RequestChangeDetector();
        Assert.Multiple(() =>
        {
            Assert.That(detector.Compare(current, null), Is.EqualTo(new RequestChangeResult(RequestChangeOutcome.New, true)));
            Assert.That(detector.Compare(current, new ExistingBaselineRecord("different", new[] { "smoke" })), Is.EqualTo(new RequestChangeResult(RequestChangeOutcome.XmlChanged, true)));
            Assert.That(detector.Compare(current, new ExistingBaselineRecord(current.XmlFingerprint, new[] { "pricing" })), Is.EqualTo(new RequestChangeResult(RequestChangeOutcome.TagsChangedOnly, false)));
            Assert.That(detector.Compare(current, new ExistingBaselineRecord(current.XmlFingerprint, new[] { "smoke" })), Is.EqualTo(new RequestChangeResult(RequestChangeOutcome.Unchanged, false)));
        });
    }

    [Test]
    [Category("Unit")]
    public void Placeholder_processor_uses_invariant_decimal_and_rejects_bad_values()
    {
        var amountReader = new PlaceholderResponseAmountReader();
        Assert.That(amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse><PlaceholderAmount>12.50</PlaceholderAmount></PlaceholderResponse>")), Is.EqualTo(12.50m));
        Assert.Multiple(() =>
        {
            Assert.That(() => amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse />")), Throws.InvalidOperationException);
            Assert.That(() => amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse><PlaceholderAmount /></PlaceholderResponse>")), Throws.InvalidOperationException);
            Assert.That(() => amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse><PlaceholderAmount>bad</PlaceholderAmount></PlaceholderResponse>")), Throws.InvalidOperationException);
        });
    }

    [Test]
    [Category("Unit")]
    public void All_route_schemas_compile_and_validate_placeholder_xml()
    {
        var configuration = MatcherConfiguration.Load(TestContext.CurrentContext.TestDirectory);
        var schemaFiles = Directory.GetFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas"), "Scheme*-response.xsd");
        Assert.That(schemaFiles, Has.Length.EqualTo(17));
        foreach (var schemaFile in schemaFiles)
        {
            var schemas = new XmlSchemaSet();
            using var schemaReader = XmlReader.Create(schemaFile);
            schemas.Add(null, schemaReader);
            schemas.Compile();
            var namespaceName = XDocument.Load(schemaFile).Root!.Attribute("targetNamespace")!.Value;
            var document = XDocument.Parse($"<PlaceholderResponse xmlns=\"{namespaceName}\"><PlaceholderAmount>1.25</PlaceholderAmount></PlaceholderResponse>");
            Assert.DoesNotThrow(() => document.Validate(schemas, null));
        }
        Assert.That(configuration.Routes.Values.Select(route => route.ResponseSchemaFile), Is.All.Matches<string>(file => File.Exists(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas", file))));
    }

    private static string WriteCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fuzzy-pricing-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }
}
