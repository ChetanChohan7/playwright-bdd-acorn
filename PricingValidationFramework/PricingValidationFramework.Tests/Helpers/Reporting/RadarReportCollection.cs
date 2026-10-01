using System.Collections.Concurrent;
using PricingValidationFramework.Core.Models.Reporting;

namespace PricingValidationFramework.Tests.Helpers.Reporting;

/// Radar scenarios run concurrently (rate-limited, not sequential like Ice's), so unlike Ice's
/// plain list of report rows, this needs to be thread-safe - and since two scenario runs could
/// race to report the same ScenarioId, Add() guards against a duplicate winning silently.
public sealed class RadarReportCollection
{
	private readonly ConcurrentDictionary<string, RadarValidationReportRow> rows = new(StringComparer.OrdinalIgnoreCase);

	public int Count => rows.Count;

	public void Add(RadarValidationReportRow row)
	{
		ArgumentNullException.ThrowIfNull(row);

		if (!rows.TryAdd(row.ScenarioId, row))
		{
			throw new InvalidOperationException("A terminal Radar report row already exists for this ScenarioId.");
		}
	}

	public IReadOnlyList<RadarValidationReportRow> GetSortedRows()
	{
		return rows.Values
			.OrderBy(row => row.ScenarioId, StringComparer.Ordinal)
			.ToArray();
	}
}