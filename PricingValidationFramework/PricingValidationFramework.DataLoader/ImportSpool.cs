namespace PricingValidationFramework.DataLoader;

using System.Text.Json;
using PricingValidationFramework.Core.Models.Database;

internal sealed class ImportSpool : IDisposable
{
    private readonly FileStream stream = new(
        Path.Combine(Path.GetTempPath(), $"scenario-import-{Guid.NewGuid():N}.tmp"),
        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);
    private readonly Dictionary<string, (long Offset, int Length)> requests = new(StringComparer.OrdinalIgnoreCase);

    public bool AddRequest(ScenarioRequestImport request)
    {
        if (requests.ContainsKey(request.ScenarioId))
        {
            return false;
        }
        var payload = JsonSerializer.SerializeToUtf8Bytes(request);
        var offset = stream.Length;
        stream.Position = offset;
        stream.Write(payload);
        requests.Add(request.ScenarioId, (offset, payload.Length));
        return true;
    }

    public IEnumerable<List<ScenarioRequestImport>> ReadBatches(int batchSize, CancellationToken cancellationToken)
    {
        var batch = new List<ScenarioRequestImport>(batchSize);
        foreach (var location in requests.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(Read(location));
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<ScenarioRequestImport>(batchSize);
            }
        }
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private ScenarioRequestImport Read((long Offset, int Length) location)
    {
        var payload = new byte[location.Length];
        stream.Position = location.Offset;
        stream.ReadExactly(payload);
        return JsonSerializer.Deserialize<ScenarioRequestImport>(payload)
            ?? throw new InvalidDataException("An import spool record is empty.");
    }

    public void Dispose() => stream.Dispose();
}
