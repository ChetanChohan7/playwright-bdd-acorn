using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ClientAutomationFramework.Core.Extraction;

public static class XmlValueExtractor
{
    /// Reads premiumPriceComponent[@code='premium']/calculatedAmount/@termAmount. Takes the LAST
    /// matching node in the document, not the first: a response can carry one techPrice per
    /// add-on plus one for the main cover, and the main cover's node - the one that lines up
    /// with the JSON side's underwrittenDataPoints entry - is always last.
    public static decimal? ExtractPremium(string? rawXml)
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

        var match = document.Descendants()
            .Where(element => element.Name.LocalName == "calculatedAmount"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "premiumPriceComponent"
                    && string.Equals((string?)ancestor.Attribute("code"), "premium", StringComparison.OrdinalIgnoreCase)))
            .LastOrDefault();

        var value = match?.Attribute("termAmount")?.Value;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
