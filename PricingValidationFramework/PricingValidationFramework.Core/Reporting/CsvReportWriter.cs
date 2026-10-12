namespace PricingValidationFramework.Core.Reporting;

using System.Globalization;
using System.Text;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

public class CsvReportWriter
{
	public async Task WriteIceReportAsync(
		string outputPath,
		string buildId,
		IReadOnlyCollection<IceValidationReportRow> rows,
		CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

		await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
		await writer.WriteLineAsync("BuildId,ScenarioId,QuoteRef,SchemeCode,ProductCode,IceValue,BaselineValue,Result,ProfileId,FieldName,XPath,Error");

		foreach (var row in rows.OrderBy(row => row.ScenarioId, StringComparer.Ordinal))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var values = new[]
			{
				buildId,
				row.ScenarioId,
				row.QuoteRef,
				row.SchemeCode,
				row.ProductCode,
				row.IceValue is null ? string.Empty : row.IceValue.Value.ToString(CultureInfo.InvariantCulture),
				row.BaselineValue is null ? string.Empty : row.BaselineValue.Value.ToString(CultureInfo.InvariantCulture),
				ToCsvValue(row.Result), row.ProfileId, row.FieldName, row.XPath, row.Error
			};

			await writer.WriteLineAsync(string.Join(',', values.Select(Escape)));
		}

		await writer.FlushAsync(cancellationToken);
	}

	public Task WriteRadarReportAsync(
		string outputPath,
		string buildId,
		IReadOnlyCollection<RadarValidationReportRow> rows,
		CancellationToken cancellationToken = default)
	{
		return WriteRadarReportRowsAsync(outputPath, buildId,
			rows.OrderBy(row => row.ScenarioId, StringComparer.Ordinal), cancellationToken);
	}

	public async Task WriteRadarReportRowsAsync(
		string outputPath,
		string buildId,
		IEnumerable<RadarValidationReportRow> sortedRows,
		CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

		await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
		await writer.WriteLineAsync("ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml");

		foreach (var row in sortedRows)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var fields = row.FieldComparisons.OrderBy(field => field.FieldKey, StringComparer.Ordinal).ToArray();
			// Scenarios that never reached comparison still need an outcome row.
			for (var index = 0; index < Math.Max(1, fields.Length); index++)
			{
				var field = fields.Length == 0 ? null : fields[index];
				var detail = new[]
				{
					row.ScenarioId, row.QuoteRef, row.ProductCode, row.SchemeCode,
					row.ProfileId, field?.FieldKey ?? string.Empty,
					string.IsNullOrEmpty(field?.ExpectedPath) ? field?.ActualPath ?? string.Empty : field.ExpectedPath,
					FormatDecimal(field?.Expected), FormatDecimal(field?.Actual), FormatDecimal(field?.Delta),
					row.MinThreshold.ToString(CultureInfo.InvariantCulture), row.MaxThreshold.ToString(CultureInfo.InvariantCulture),
					field is null ? string.Empty : ToCsvValue(field.Result), ToCsvValue(row.Result),
					row.FailureStage ?? string.Empty, row.Error ?? string.Empty,
					index == 0 ? NormalizeXml(row.RequestXml) : string.Empty,
					index == 0 ? NormalizeXml(row.BaselineXml) : string.Empty,
					index == 0 ? NormalizeXml(row.RadarResponseXml) : string.Empty
				};
				await writer.WriteLineAsync(string.Join(',', detail.Select(Escape)));
			}
		}

		await writer.FlushAsync(cancellationToken);
	}

	private static string FormatDecimal(decimal? value)
	{
		return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private static string ToCsvValue(ScenarioResult result)
	{
		return result switch
		{
			ScenarioResult.Pass => "PASS",
			ScenarioResult.Fail => "FAIL",
			ScenarioResult.Error => "ERROR",
			_ => "ERROR"
		};
	}

	public static string NormalizeXml(string xml)
	{
		return string.IsNullOrWhiteSpace(xml)
			? string.Empty
			: System.Text.RegularExpressions.Regex.Replace(xml, @"\s+", " ").Trim();
	}

	private static string Escape(string value)
	{
		return value.Contains(',', StringComparison.Ordinal) ||
			value.Contains('"', StringComparison.Ordinal) ||
			value.Contains('\r', StringComparison.Ordinal) ||
			value.Contains('\n', StringComparison.Ordinal)
			? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
			: value;
	}
}
