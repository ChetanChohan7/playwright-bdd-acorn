using System.Text.Json;
using PricingXml.ScenarioTool;

var (command, options) = ParseArgs(args);

var inputDir = options.GetValueOrDefault("--input-dir", "request_xmls");
var csvPath = options.GetValueOrDefault("--csv", "scenarios.csv");

switch (command)
{
    case "build-csv":
        ScenarioCsvBuilder.Run(inputDir, csvPath);
        return 0;

    case "update-csv":
        if (!options.TryGetValue("--edits", out var editsPath))
        {
            Console.WriteLine("update-csv requires --edits <path-to-edits.json>");
            return 1;
        }
        UpdateCsvEditor.Run(csvPath, LoadEdits(editsPath));
        return 0;

    case "add-elements":
        if (!options.TryGetValue("--elements", out var elementsPath))
        {
            Console.WriteLine("add-elements requires --elements <path-to-elements.json>");
            return 1;
        }
        AddElementsEditor.Run(csvPath, LoadElementAdds(elementsPath));
        return 0;

    default:
        Console.WriteLine(
            "Usage: PricingXml.ScenarioTool <build-csv|update-csv|add-elements> " +
            "[--input-dir <dir>] [--csv <path>] [--edits <path>] [--elements <path>]");
        return 1;
}

static (string command, Dictionary<string, string> options) ParseArgs(string[] args)
{
    if (args.Length == 0)
        return (string.Empty, new Dictionary<string, string>());

    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 1; i + 1 < args.Length; i += 2)
        options[args[i]] = args[i + 1];

    return (args[0], options);
}

static List<FieldEdit> LoadEdits(string editsPath)
{
    if (!File.Exists(editsPath))
    {
        Console.WriteLine($"Edits file not found: {editsPath}");
        return [];
    }

    var json = File.ReadAllText(editsPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    return JsonSerializer.Deserialize<List<FieldEdit>>(json, options) ?? [];
}

static List<ElementAdd> LoadElementAdds(string elementsPath)
{
    if (!File.Exists(elementsPath))
    {
        Console.WriteLine($"Elements file not found: {elementsPath}");
        return [];
    }

    var json = File.ReadAllText(elementsPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    return JsonSerializer.Deserialize<List<ElementAdd>>(json, options) ?? [];
}
