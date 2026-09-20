using System.Xml;
using System.Xml.Linq;
using FuzzyPricingMatcher.Tests.Validation;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class RequestXmlMetadataReader : IRequestXmlMetadataReader
{
    public RequestXmlMetadata Read(string rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            throw new RequestXmlValidationException("Request XML is empty.");

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                ConformanceLevel = ConformanceLevel.Document
            };
            using var stringReader = new StringReader(rawXml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(xmlReader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            var schemeElements = document.Descendants().Where(element => element.Name.LocalName.Equals("SchemeCode", StringComparison.OrdinalIgnoreCase)).ToList();
            var policyElements = document.Descendants().Where(element => element.Name.LocalName.Equals("PolicyReference", StringComparison.OrdinalIgnoreCase)).ToList();

            if (schemeElements.Count != 1)
                throw schemeElements.Count == 0
                    ? new MissingSchemeCodeException("Request XML is missing SchemeCode.")
                    : new DuplicateXmlMetadataElementException("Request XML contains multiple SchemeCode elements.");
            if (policyElements.Count != 1)
                throw policyElements.Count == 0
                    ? new MissingPolicyReferenceException("Request XML is missing PolicyReference.")
                    : new DuplicateXmlMetadataElementException("Request XML contains multiple PolicyReference elements.");

            var schemeCode = schemeElements[0].Value.Trim();
            var quoteReference = policyElements[0].Value.Trim();
            if (schemeCode.Length == 0)
                throw new MissingSchemeCodeException("Request XML contains an empty SchemeCode.");
            if (quoteReference.Length == 0)
                throw new MissingPolicyReferenceException("Request XML contains an empty PolicyReference.");

            return new RequestXmlMetadata { SchemeCode = schemeCode, QuoteReference = quoteReference, Document = document };
        }
        catch (XmlException exception)
        {
            throw new RequestXmlValidationException($"Request XML is malformed at line {exception.LineNumber}, position {exception.LinePosition}: {exception.Message}", exception);
        }
    }
}