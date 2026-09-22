using System.Xml.Linq;

namespace PricingXml.ScenarioTool;

internal static class XmlDocText
{
    // XDocument.ToString() never re-emits the <?xml ...?> declaration, even though
    // Parse() captured it — restore it manually if the source had one. With
    // LoadOptions.PreserveWhitespace, the original whitespace between the declaration
    // and the root element is already part of `body`, so only add a separating
    // newline if that whitespace wasn't there.
    public static string Serialize(XDocument doc)
    {
        var body = doc.ToString(SaveOptions.DisableFormatting);
        if (doc.Declaration == null)
            return body;

        return body.StartsWith('\n') || body.StartsWith('\r')
            ? doc.Declaration + body
            : doc.Declaration + "\n" + body;
    }
}
