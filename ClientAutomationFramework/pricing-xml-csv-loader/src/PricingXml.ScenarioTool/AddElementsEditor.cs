using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PricingXml.ScenarioTool;

public static class AddElementsEditor
{
    public static void Run(string csvPath, IReadOnlyList<ElementAdd> edits)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"CSV not found: {csvPath}. Run 'build-csv' first.");
            return;
        }

        if (edits.Count == 0)
        {
            Console.WriteLine("No element-adds supplied — nothing to do.");
            return;
        }

        var rows = ScenarioCsvIo.Read(csvPath);

        foreach (var edit in edits)
        {
            List<XElement> templates;
            try
            {
                templates = ParseTemplates(edit.Xml);
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"add-elements: {edit.ParentXPath} — skipped, could not parse Xml: {ex.Message}");
                continue;
            }

            if (templates.Count == 0)
            {
                Console.WriteLine($"add-elements: {edit.ParentXPath} — skipped, no elements found in Xml.");
                continue;
            }

            var rowsChanged = 0;
            var skippedCondition = 0;
            var missingParent = 0;
            var skippedUnparseable = 0;

            foreach (var row in rows)
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Parse(row.Xml, LoadOptions.PreserveWhitespace);
                }
                catch (XmlException)
                {
                    // Don't let one malformed row's XML abort the whole batch.
                    skippedUnparseable++;
                    continue;
                }

                if (edit.ConditionXPath != null)
                {
                    var conditionValue = doc.XPathSelectElement(edit.ConditionXPath)?.Value;
                    if (conditionValue != edit.ConditionValue) { skippedCondition++; continue; }
                }

                var parent = doc.XPathSelectElement(edit.ParentXPath);
                if (parent == null) { missingParent++; continue; }

                // Fresh copies per row — an XElement instance can't be attached under more
                // than one parent/document, so each row needs its own clone of every template.
                foreach (var template in templates)
                    XmlIndentHelper.AppendWithIndent(parent, new XElement(template));

                row.Xml = XmlDocText.Serialize(doc);
                rowsChanged++;
            }

            var conditionSummary = edit.ConditionXPath != null
                ? $", {skippedCondition} skipped (condition not met)"
                : "";
            var unparseableSummary = skippedUnparseable > 0
                ? $", {skippedUnparseable} row(s) skipped (could not parse XML)"
                : "";
            Console.WriteLine(
                $"add-elements: {edit.ParentXPath} — {templates.Count} element(s) added to {rowsChanged} row(s){conditionSummary}, {missingParent} row(s) skipped (parent not found){unparseableSummary}.");
        }

        // Back up the CSV before overwriting it, same as update-csv — a bad elements.json
        // shouldn't be able to silently destroy the source of truth.
        File.Copy(csvPath, csvPath + ".bak", overwrite: true);
        ScenarioCsvIo.WriteAll(csvPath, rows);
        Console.WriteLine($"Backup of previous CSV written to {csvPath}.bak");
    }

    // Wraps the raw XML in a throwaway root so multiple top-level sibling elements (e.g.
    // several <book> blocks pasted one after another) parse as one document, mirroring the
    // UI's array-mode input — a stray leading <?xml ...?> prolog is stripped first since
    // it's only valid at the very start of a document, not nested inside a wrapper.
    private static List<XElement> ParseTemplates(string xml)
    {
        var stripped = Regex.Replace(xml.Trim(), @"^\s*<\?xml[^>]*\?>\s*", "");
        if (stripped.Length == 0)
            return [];

        var wrapperDoc = XDocument.Parse($"<__items__>{stripped}</__items__>", LoadOptions.PreserveWhitespace);
        return wrapperDoc.Root!.Elements().ToList();
    }
}
