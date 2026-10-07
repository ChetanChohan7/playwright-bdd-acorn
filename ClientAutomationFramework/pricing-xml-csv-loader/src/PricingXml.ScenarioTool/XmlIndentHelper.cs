using System.Xml.Linq;

namespace PricingXml.ScenarioTool;

internal static class XmlIndentHelper
{
    // Appends newElement to parent while preserving indentation: finds the whitespace text
    // node the file already uses between siblings, copies it before the new element, and
    // inserts both ahead of the trailing whitespace that precedes the closing tag (so that
    // whitespace still separates the new element from it). Falls back to a plain Add if the
    // document has no such whitespace to copy (e.g. it was originally one line).
    public static void AppendWithIndent(XElement parent, XElement newElement)
    {
        bool IsWhitespaceWithNewline(XNode node) =>
            node is XText text && text.Value.Contains('\n') && string.IsNullOrWhiteSpace(text.Value);

        var indentNode = parent.Nodes().FirstOrDefault(IsWhitespaceWithNewline) as XText;
        if (indentNode == null)
        {
            parent.Add(newElement);
            return;
        }

        var lastNode = parent.Nodes().LastOrDefault();
        var trailingWhitespace = lastNode != null && IsWhitespaceWithNewline(lastNode) ? (XText)lastNode : null;

        if (trailingWhitespace != null)
        {
            trailingWhitespace.AddBeforeSelf(new XText(indentNode.Value));
            trailingWhitespace.AddBeforeSelf(newElement);
        }
        else
        {
            parent.Add(new XText(indentNode.Value));
            parent.Add(newElement);
        }
    }
}
