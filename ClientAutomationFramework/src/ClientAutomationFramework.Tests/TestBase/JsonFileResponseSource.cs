using System.Text.Json;

namespace ClientAutomationFramework.Tests.TestBase;

// TEMPORARY: stands in for ClientAutomationFramework.Core.Database.ResponseDataReader while the
// real database is still being set up. Reads the same shape a "get last response" DB query
// would return, but from data/xml-response.json instead of XML_Response. Once the database is
// ready, delete this and go back to TestSetup.ResponseReader.GetLastResponseAsync everywhere
// this is used.
public sealed record StoredScenarioResponse
{
    public required string ScenarioId { get; init; }
    public required string QuoteRef { get; init; }
    public required string XmlResponse { get; init; }
    public string Version { get; init; } = string.Empty;
    public string? PassFail { get; init; }
    public DateTime UpdatedOn { get; init; }
}

public static class JsonFileResponseSource
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static IReadOnlyList<StoredScenarioResponse> LoadAll(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Stored response data file not found: {path}", path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<StoredScenarioResponse>>(json, Options) ?? [];
    }

    /// Mirrors ResponseDataReader.GetLastResponseAsync: the most recent row for scenario_id,
    /// regardless of quote_ref.
    public static StoredScenarioResponse? GetLast(string path, string scenarioId) =>
        LoadAll(path)
            .Where(row => string.Equals(row.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.UpdatedOn)
            .FirstOrDefault();
}
