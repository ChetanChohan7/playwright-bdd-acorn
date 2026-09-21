using System.Xml.Linq;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Validation;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class SchemaValidationTests
{
    private const string SchemaFile = "Scheme01-response.xsd";
    private const string Namespace = "urn:fuzzypricing:placeholder:scheme01";

    [Test]
    [Category("Unit")]
    public void Valid_response_validates_and_registry_caches_one_compiled_schema()
    {
        var registry = CreateRegistry(SchemaFile);
        var first = registry.Resolve(SchemaFile);
        var second = registry.Resolve(SchemaFile);
        var result = new XmlSchemaValidator(registry).Validate(Request("<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\"><PlaceholderAmount>12.50</PlaceholderAmount></PlaceholderResponse>"));
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(second));
            Assert.That(result.Document.Root!.Name.NamespaceName, Is.EqualTo(Namespace));
        });
    }

    [Test]
    [Category("Unit")]
    public void Malformed_wrong_namespace_missing_element_and_invalid_decimal_fail_without_full_xml()
    {
        var validator = new XmlSchemaValidator(CreateRegistry(SchemaFile));
        Assert.Multiple(() =>
        {
            Assert.That(() => validator.Validate(Request("<PlaceholderResponse>")), Throws.TypeOf<XmlResponseValidationException>().With.Message.Contains("line"));
            Assert.That(() => validator.Validate(Request("<PlaceholderResponse xmlns=\"urn:wrong\"><PlaceholderAmount>1</PlaceholderAmount></PlaceholderResponse>")), Throws.TypeOf<XmlResponseValidationException>().With.Message.Contains("Scheme01-response.xsd"));
            Assert.That(() => validator.Validate(Request("<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\" />")), Throws.TypeOf<XmlResponseValidationException>());
            var exception = Assert.Throws<XmlResponseValidationException>(() => validator.Validate(Request("<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\"><PlaceholderAmount>bad</PlaceholderAmount></PlaceholderResponse>")));
            Assert.That(exception!.Message, Does.Not.Contain("PlaceholderResponse xmlns"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Missing_and_invalid_schema_files_are_rejected()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas");
        var missing = new SchemaRegistry(directory, new[] { "missing.xsd" });
        Assert.That(() => missing.Resolve("missing.xsd"), Throws.TypeOf<SchemaFileNotFoundException>());
        Assert.That(() => new SchemaRegistry(directory, new[] { SchemaFile, "scheme01-response.xsd" }), Throws.TypeOf<DuplicateSchemaRegistrationException>());

        var invalidPath = Path.Combine(Path.GetTempPath(), $"invalid-{Guid.NewGuid():N}.xsd");
        File.WriteAllText(invalidPath, "<not-schema>");
        try
        {
            var invalid = new SchemaRegistry(Path.GetDirectoryName(invalidPath)!, new[] { Path.GetFileName(invalidPath) });
            Assert.That(() => invalid.Resolve(Path.GetFileName(invalidPath)), Throws.TypeOf<SchemaCompilationException>());
        }
        finally { File.Delete(invalidPath); }
    }

    [Test]
    [Category("Unit")]
    public void External_schema_resolution_is_disabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"external-{Guid.NewGuid():N}.xsd");
        File.WriteAllText(path, "<?xml version=\"1.0\"?><xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:import namespace=\"urn:remote\" schemaLocation=\"https://example.invalid/remote.xsd\" /></xs:schema>");
        try
        {
            var registry = new SchemaRegistry(Path.GetDirectoryName(path)!, new[] { Path.GetFileName(path) });
            Assert.That(() => registry.Resolve(Path.GetFileName(path)), Throws.TypeOf<SchemaCompilationException>());
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Category("Unit")]
    public void Processor_registry_is_case_insensitive_rejects_duplicates_and_unknown_names()
    {
        var amountReader = new PlaceholderResponseAmountReader();
        var registry = new ComparisonAmountReaderRegistry(new[] { new ComparisonAmountReaderRegistration("PlaceholderResponseProcessor", amountReader) });
        Assert.Multiple(() =>
        {
            Assert.That(registry.Resolve("placeholderresponseprocessor"), Is.SameAs(amountReader));
            Assert.That(() => registry.Resolve("unknown"), Throws.InvalidOperationException);
            Assert.That(() => new ComparisonAmountReaderRegistry(new[] { new ComparisonAmountReaderRegistration("P", amountReader), new ComparisonAmountReaderRegistration("p", amountReader) }), Throws.InvalidOperationException);
        });
    }

    [Test]
    [Category("Unit")]
    public void Placeholder_processor_rejects_missing_and_invalid_amount_but_accepts_zero()
    {
        var amountReader = new PlaceholderResponseAmountReader();
        Assert.Multiple(() =>
        {
            Assert.That(amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse><PlaceholderAmount>0</PlaceholderAmount></PlaceholderResponse>")), Is.EqualTo(0m));
            Assert.That(() => amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse />")), Throws.InvalidOperationException);
            Assert.That(() => amountReader.ReadAmount(XDocument.Parse("<PlaceholderResponse><PlaceholderAmount>invalid</PlaceholderAmount></PlaceholderResponse>")), Throws.InvalidOperationException);
        });
    }

    [Test]
    [Category("Unit")]
    public void Validation_happens_before_processor_and_same_processor_handles_api_and_stored_documents()
    {
        var amountReader = new CountingAmountReader();
        var service = new ExternalResponseValidationService(new XmlSchemaValidator(CreateRegistry(SchemaFile)), new ComparisonAmountReaderRegistry(new[] { new ComparisonAmountReaderRegistration("counting", amountReader) }));
        var valid = Request("<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\"><PlaceholderAmount>1</PlaceholderAmount></PlaceholderResponse>");
        service.ValidateAndReadAmount(valid, "COUNTING");
        service.ValidateAndReadAmount(valid with { DocumentType = "StoredBaseline" }, "counting");
        Assert.That(amountReader.Count, Is.EqualTo(2));

        Assert.That(() => service.ValidateAndReadAmount(Request("<PlaceholderResponse xmlns=\"urn:fuzzypricing:placeholder:scheme01\" />"), "counting"), Throws.TypeOf<XmlResponseValidationException>());
        Assert.That(amountReader.Count, Is.EqualTo(2));
    }

    private static SchemaRegistry CreateRegistry(params string[] files) => new(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas"), files);

    private static XmlResponseValidationRequest Request(string raw) => new(raw, "SCN-1", "Q-1", "API response", SchemaFile);

    private sealed class CountingAmountReader : IResponseAmountReader
    {
        public int Count { get; private set; }
        public decimal ReadAmount(XDocument validatedResponseDocument) { Count++; return 1m; }
    }
}
