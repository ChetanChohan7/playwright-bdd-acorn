using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace FuzzyPricingMatcher.Tests.Validation;

public sealed class XmlSchemaValidator : IXmlSchemaValidator
{
    private readonly ISchemaRegistry registry;

    public XmlSchemaValidator(ISchemaRegistry registry) => this.registry = registry;

    public XmlValidationResult Validate(XmlResponseValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawXml))
            throw new XmlResponseValidationException($"ScenarioId '{request.ScenarioId}', QuoteRef '{request.QuoteRef}', document type '{request.DocumentType}', XSD '{request.SchemaFileName}': response XML is empty.");
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, ConformanceLevel = ConformanceLevel.Document };
            using var stringReader = new StringReader(request.RawXml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new XmlResponseValidationException(Message(request, $"malformed XML at line {exception.LineNumber}, position {exception.LinePosition}: {exception.Message}"), exception);
        }

        SchemaRegistration registration;
        try { registration = registry.Resolve(request.SchemaFileName); }
        catch (Exception exception) when (exception is SchemaRegistryException)
        { throw new XmlResponseValidationException(Message(request, exception.Message), exception); }

        var validationError = string.Empty;
        var validationLine = 0;
        var validationPosition = 0;
        var rootName = new XmlQualifiedName(document.Root?.Name.LocalName ?? string.Empty, document.Root?.Name.NamespaceName ?? string.Empty);
        if (!registration.SchemaSet.GlobalElements.Contains(rootName))
            throw new XmlResponseValidationException(Message(request, $"schema validation failed: root element '{rootName.Name}' is not declared in namespace '{rootName.Namespace}'."));
        document.Validate(registration.SchemaSet, (_, args) =>
        {
            if (validationError.Length == 0)
            {
                validationError = args.Message;
                if (args.Exception is not null) { validationLine = args.Exception.LineNumber; validationPosition = args.Exception.LinePosition; }
            }
        });
        if (validationError.Length > 0)
            throw new XmlResponseValidationException(Message(request, $"schema validation failed at line {validationLine}, position {validationPosition}: {validationError}"));
        return new XmlValidationResult(document, request.SchemaFileName, request.DocumentType);
    }

    private static string Message(XmlResponseValidationRequest request, string detail) => $"ScenarioId '{request.ScenarioId}', QuoteRef '{request.QuoteRef}', document type '{request.DocumentType}', XSD '{request.SchemaFileName}': {detail}";
}