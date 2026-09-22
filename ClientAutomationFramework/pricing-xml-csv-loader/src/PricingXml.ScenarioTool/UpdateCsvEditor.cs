using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PricingXml.ScenarioTool;

public static class UpdateCsvEditor
{
    private enum EditResult
    {
        ConditionNotMet,
        Updated,
        SkippedMissing,
    }

    public static void Run(string csvPath, IReadOnlyList<FieldEdit> edits)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"CSV not found: {csvPath}. Run 'build-csv' first.");
            return;
        }

        if (edits.Count == 0)
        {
            Console.WriteLine("No edits supplied — nothing to do.");
            return;
        }

        var rows = ScenarioCsvIo.Read(csvPath);
        var updatedCounts = edits.ToDictionary(DescribeEdit, _ => 0);
        var skippedMissingCounts = edits.ToDictionary(DescribeEdit, _ => 0);
        var skippedUnparseable = 0;

        foreach (var row in rows)
        {
            // PreserveWhitespace keeps the original file's indentation intact,
            // so an edit only changes the field it touches, not the whole document's formatting.
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
            var changed = false;

            foreach (var edit in edits)
            {
                var key = DescribeEdit(edit);
                switch (ApplyEdit(doc, edit))
                {
                    case EditResult.Updated:
                        updatedCounts[key]++;
                        changed = true;
                        break;
                    case EditResult.SkippedMissing:
                        skippedMissingCounts[key]++;
                        break;
                    case EditResult.ConditionNotMet:
                        break; // not applicable to this row — not worth reporting
                }
            }

            if (changed)
                row.Xml = XmlDocText.Serialize(doc);
        }

        // Back up the CSV before overwriting it — it's the source of truth,
        // so a bad edits.json shouldn't be able to silently destroy it.
        File.Copy(csvPath, csvPath + ".bak", overwrite: true);

        ScenarioCsvIo.WriteAll(csvPath, rows);

        var unparseableSummary = skippedUnparseable > 0
            ? $" ({skippedUnparseable} row(s) skipped — could not parse XML.)"
            : "";
        foreach (var edit in edits)
        {
            var key = DescribeEdit(edit);
            Console.WriteLine(
                $"update-csv: {key} — {updatedCounts[key]} row(s) updated, {skippedMissingCounts[key]} skipped (field missing on a matching row).");
        }
        if (unparseableSummary.Length > 0)
            Console.WriteLine(unparseableSummary.Trim());
        Console.WriteLine($"Backup of previous CSV written to {csvPath}.bak");
    }

    private static EditResult ApplyEdit(XDocument doc, FieldEdit edit)
    {
        if (edit.ConditionXPath != null)
        {
            var conditionValue = doc.XPathSelectElement(edit.ConditionXPath)?.Value;
            if (conditionValue != edit.ConditionValue)
                return EditResult.ConditionNotMet;
        }

        var parent = doc.XPathSelectElement(edit.ParentXPath);
        if (parent == null)
            return EditResult.ConditionNotMet;

        var target = parent.Element(edit.ElementName);
        if (target != null)
        {
            target.Value = edit.Value;
            return EditResult.Updated;
        }

        if (edit.AddIfMissing)
        {
            XmlIndentHelper.AppendWithIndent(parent, new XElement(edit.ElementName, edit.Value));
            return EditResult.Updated;
        }

        return EditResult.SkippedMissing;
    }

    private static string DescribeEdit(FieldEdit edit) => $"{edit.ParentXPath}/{edit.ElementName}";
}
