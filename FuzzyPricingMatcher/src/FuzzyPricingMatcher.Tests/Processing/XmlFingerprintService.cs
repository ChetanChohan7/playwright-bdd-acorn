using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class XmlFingerprintService
{
    public string CreateFingerprint(XDocument document)
    {
        var canonical = new StringBuilder();
        if (document.Root is null)
            throw new ArgumentException("XML document has no root element.", nameof(document));
        AppendElement(document.Root, canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendElement(XElement element, StringBuilder output)
    {
        output.Append('<').Append('{').Append(element.Name.NamespaceName).Append('}').Append(element.Name.LocalName);
        foreach (var attribute in element.Attributes().OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal).ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal))
            output.Append('|').Append('{').Append(attribute.Name.NamespaceName).Append('}').Append(attribute.Name.LocalName).Append('=').Append(attribute.Value);
        output.Append('>');
        var hasMeaningfulText = element.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value));
        foreach (var node in element.Nodes())
        {
            if (node is XElement child)
                AppendElement(child, output);
            else if (node is XText text && (hasMeaningfulText || !element.Elements().Any()))
                output.Append("#").Append(text.Value);
        }
        output.Append("</>");
    }
}