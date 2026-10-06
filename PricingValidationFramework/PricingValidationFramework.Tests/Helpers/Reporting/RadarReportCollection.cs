using System.Text.Json;
using PricingValidationFramework.Core.Models.Reporting;

namespace PricingValidationFramework.Tests.Helpers.Reporting;

public sealed class RadarReportCollection : IDisposable
{
	private readonly object gate = new();
	private readonly Dictionary<string, (long Offset, int Length)> rows = new(StringComparer.OrdinalIgnoreCase);
	private readonly FileStream spool = new(
		Path.Combine(Path.GetTempPath(), $"radar-report-{Guid.NewGuid():N}.tmp"),
		FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
		FileOptions.DeleteOnClose | FileOptions.RandomAccess);
	private bool disposed;

	public int Count
	{
		get
		{
			lock (gate)
			{
				return rows.Count;
			}
		}
	}

	public void Add(RadarValidationReportRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		var payload = JsonSerializer.SerializeToUtf8Bytes(row);
		lock (gate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (rows.ContainsKey(row.ScenarioId))
			{
				throw new InvalidOperationException("A terminal Radar report row already exists for this ScenarioId.");
			}

			var offset = spool.Length;
			spool.Position = offset;
			try
			{
				spool.Write(payload);
				rows.Add(row.ScenarioId, (offset, payload.Length));
			}
			catch
			{
				spool.SetLength(offset);
				throw;
			}
		}
	}

	public IEnumerable<RadarValidationReportRow> ReadSortedRows()
	{
		(long Offset, int Length)[] locations;
		lock (gate)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			locations = rows.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => row.Value).ToArray();
		}

		foreach (var location in locations)
		{
			var payload = new byte[location.Length];
			lock (gate)
			{
				ObjectDisposedException.ThrowIf(disposed, this);
				spool.Position = location.Offset;
				spool.ReadExactly(payload);
			}

			yield return JsonSerializer.Deserialize<RadarValidationReportRow>(payload)
				?? throw new InvalidDataException("A Radar report spool record is empty.");
		}
	}

	public void Dispose()
	{
		lock (gate)
		{
			if (!disposed)
			{
				spool.Dispose();
				rows.Clear();
				disposed = true;
			}
		}
	}
}