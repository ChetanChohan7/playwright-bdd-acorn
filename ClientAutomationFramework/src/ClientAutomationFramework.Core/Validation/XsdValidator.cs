using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ClientAutomationFramework.Core.Validation;

public sealed record XsdValidationResult(bool Valid, string Error);

/// Validates a raw XML string against a local XSD file. DTD processing is prohibited and no
/// external XmlResolver is used, so the document can't pull in anything outside the schema file.
public sealed class XsdValidator(string xsdPath)
{
    private readonly XmlSchemaSet schemas = LoadSchema(xsdPath);

    public XsdValidationResult Validate(string rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return new XsdValidationResult(false, "Response XML is empty.");

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new StringReader(rawXml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            return new XsdValidationResult(false, $"Malformed XML: {exception.Message}");
        }

        var error = string.Empty;
        document.Validate(schemas, (_, args) =>
        {
            if (error.Length == 0)
                error = args.Message;
        });
        return error.Length == 0 ? new XsdValidationResult(true, string.Empty) : new XsdValidationResult(false, error);
    }

    private static XmlSchemaSet LoadSchema(string xsdPath)
    {
        if (!File.Exists(xsdPath))
            throw new FileNotFoundException($"Schema file '{xsdPath}' was not found.", xsdPath);
        var schemas = new XmlSchemaSet();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(xsdPath, settings);
        schemas.Add(null, reader);
        schemas.Compile();
        return schemas;
    }
}
