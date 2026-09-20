using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Validation;

public sealed record XmlResponseValidationRequest(string RawXml, string ScenarioId, string QuoteRef, string DocumentType, string SchemaFileName);

public sealed record XmlValidationResult(XDocument Document, string SchemaFileName, string DocumentType);

public sealed class XmlResponseValidationException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

public interface IXmlSchemaValidator
{
    XmlValidationResult Validate(XmlResponseValidationRequest request);
}