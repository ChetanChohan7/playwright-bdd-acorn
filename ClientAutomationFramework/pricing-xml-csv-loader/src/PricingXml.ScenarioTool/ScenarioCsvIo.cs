using System.Globalization;
using CsvHelper;

namespace PricingXml.ScenarioTool;

internal static class ScenarioCsvIo
{
    public static List<ScenarioRow> Read(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<ScenarioRow>().ToList();
    }

    public static void WriteAll(string csvPath, List<ScenarioRow> rows)
    {
        using var writer = new StreamWriter(csvPath, append: false);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rows);
    }
}
