using System.Globalization;
using CsvHelper;

namespace PricingXml.ScenarioTool;

public static class ScenarioCsvBuilder
{
    public static void Run(string inputDir, string csvPath)
    {
        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"Input directory not found: {inputDir}");
            return;
        }

        var rows = ReadExisting(csvPath);
        var rowsById = rows.ToDictionary(r => r.ScenarioId, StringComparer.OrdinalIgnoreCase);

        var files = Directory.GetFiles(inputDir, "scenario-*.xml")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var file in files)
        {
            var scenarioId = Path.GetFileNameWithoutExtension(file);
            var xml = File.ReadAllText(file);

            if (!rowsById.TryGetValue(scenarioId, out var existing))
            {
                var row = new ScenarioRow { ScenarioId = scenarioId, Xml = xml };
                rows.Add(row);
                rowsById[scenarioId] = row;
                added++;
            }
            else if (existing.Xml != xml)
            {
                existing.Xml = xml;
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        if (updated > 0 && File.Exists(csvPath))
        {
            // Only back up when something existing is actually about to change — a pure-append
            // run (the common case) doesn't need one, since nothing already in the file is at risk.
            File.Copy(csvPath, csvPath + ".bak", overwrite: true);
        }

        WriteAll(csvPath, rows);

        Console.WriteLine($"build-csv: {added} new scenario(s) added, {updated} updated, {unchanged} unchanged.");
        if (updated > 0)
            Console.WriteLine($"Backup of previous CSV written to {csvPath}.bak");
    }

    private static List<ScenarioRow> ReadExisting(string csvPath)
    {
        if (!File.Exists(csvPath))
            return [];

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<ScenarioRow>().ToList();
    }

    private static void WriteAll(string csvPath, List<ScenarioRow> rows)
    {
        using var writer = new StreamWriter(csvPath, append: false);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rows);
    }
}
