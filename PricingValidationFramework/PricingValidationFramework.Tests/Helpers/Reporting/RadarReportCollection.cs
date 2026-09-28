using System.Collections.Concurrent;
using PricingValidationFramework.Core.Models.Reporting;

namespace PricingValidationFramework.Tests.Helpers.Reporting;

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