namespace PricingValidationFramework.DataLoader;

using System.Text.Json;
using PricingValidationFramework.Core.Models.Database;

internal sealed record ScenarioImportEntry(string ScenarioId, ScenarioRequestImport? Request, ScenarioResponseImport? Response);

internal sealed class ImportSpool : IDisposable
{
    private readonly FileStream stream = new(
        Path.Combine(Path.GetTempPath(), $"scenario-import-{Guid.NewGuid():N}.tmp"),
        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);
    private readonly Dictionary<string, (long Offset, int Length)> requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Offset, int Length)> responses = new(StringComparer.OrdinalIgnoreCase);

    public bool ContainsRequest(string scenarioId) => requests.ContainsKey(scenarioId);

    public bool AddRequest(ScenarioRequestImport request) => Add(requests, request.ScenarioId, request);
    public bool AddResponse(ScenarioResponseImport response) => Add(responses, response.ScenarioId, response);

    private bool Add<TRecord>(Dictionary<string, (long Offset, int Length)> index, string scenarioId, TRecord row)
    {
        if (index.ContainsKey(scenarioId))
        {
            return false;
        }
        var payload = JsonSerializer.SerializeToUtf8Bytes(row);
        var offset = stream.Length;
        stream.Position = offset;
        stream.Write(payload);
        index.Add(scenarioId, (offset, payload.Length));
        return true;
    }

    public IEnumerable<List<ScenarioImportEntry>> ReadBatches(int batchSize, CancellationToken cancellationToken)
    {
        var batch = new List<ScenarioImportEntry>(batchSize);
        foreach (var scenarioId in requests.Keys.Concat(responses.Keys.Where(scenarioId => !requests.ContainsKey(scenarioId))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(new ScenarioImportEntry(scenarioId,
                Read<ScenarioRequestImport>(requests, scenarioId), Read<ScenarioResponseImport>(responses, scenarioId)));
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<ScenarioImportEntry>(batchSize);
            }
        }
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private TRecord? Read<TRecord>(Dictionary<string, (long Offset, int Length)> index, string scenarioId)
        where TRecord : class
    {
        if (!index.TryGetValue(scenarioId, out var location))
        {
            return null;
        }
        var payload = new byte[location.Length];
        stream.Position = location.Offset;
        stream.ReadExactly(payload);
        return JsonSerializer.Deserialize<TRecord>(payload)
            ?? throw new InvalidDataException("An import spool record is empty.");
    }

    public void Dispose() => stream.Dispose();
}