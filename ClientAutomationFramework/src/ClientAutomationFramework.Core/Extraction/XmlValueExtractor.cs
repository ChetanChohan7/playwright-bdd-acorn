using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ClientAutomationFramework.Core.Extraction;

public static class XmlValueExtractor
{
    /// General-purpose: finds the first (or, with useLastMatch, the last) element named
    /// elementLocalName whose ancestor chain includes an element named parentLocalName carrying
    /// parentFilterAttribute=parentFilterValue (when given), then reads attributeName off it as
    /// a decimal.
    public static decimal? ExtractDecimalAttribute(string? rawXml, string elementLocalName, string attributeName, string? parentLocalName = null, string? parentFilterAttribute = null, string? parentFilterValue = null, bool useLastMatch = false)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return null;

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var stringReader = new StringReader(rawXml);
            using var reader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        var candidates = document.Descendants().Where(element => element.Name.LocalName == elementLocalName);
        if (parentLocalName is not null)
        {
            candidates = candidates.Where(element => element.Ancestors()
                .Any(ancestor => ancestor.Name.LocalName == parentLocalName
                    && (parentFilterAttribute is null || string.Equals((string?)ancestor.Attribute(parentFilterAttribute), parentFilterValue, StringComparison.OrdinalIgnoreCase))));
        }

        var match = useLastMatch ? candidates.LastOrDefault() : candidates.FirstOrDefault();
        var value = match?.Attribute(attributeName)?.Value;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// Convenience wrapper for techPrice/priceComponents/premiumPriceComponent[@code='premium']/calculatedAmount@termAmount.
    /// Takes the LAST matching node in the document, not the first: a response can carry one
    /// techPrice per add-on plus one for the main cover, and the main cover's node - the one
    /// that lines up with the JSON side's underwrittenDataPoints entry - is always last.
    public static decimal? ExtractPremium(string? rawXml) =>
        ExtractDecimalAttribute(rawXml, "calculatedAmount", "termAmount", "premiumPriceComponent", "code", "premium", useLastMatch: true);
}
