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
		await writer.WriteLineAsync("BuildId,ScenarioId,QuoteRef,SchemeCode,ProductCode,IceValue,BaselineValue,Result");

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
				ToCsvValue(row.Result)
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
		await writer.WriteLineAsync("RecordType,BuildId,ScenarioId,QuoteRef,ProductCode,SchemeCode,SchemaProfile,FieldKey,ExpectedPath,ActualPath,Expected,Actual,Delta,MinDelta,MaxDelta,FieldResult,OverallResult,ComparedFields,PassedFields,FailedFields,FailureStage,Error,RequestXml,BaselineXml,ApiXml");

		foreach (var row in sortedRows)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var fields = row.FieldComparisons;
			var summary = new[]
			{
				"SUMMARY", buildId, row.ScenarioId, row.QuoteRef, row.ProductCode, row.SchemeCode,
				row.SchemaProfile, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
				string.Empty, row.MinThreshold.ToString(CultureInfo.InvariantCulture), row.MaxThreshold.ToString(CultureInfo.InvariantCulture),
				string.Empty, ToCsvValue(row.Result), fields.Count.ToString(CultureInfo.InvariantCulture),
				fields.Count(field => field.Result == ScenarioResult.Pass).ToString(CultureInfo.InvariantCulture),
				fields.Count(field => field.Result == ScenarioResult.Fail).ToString(CultureInfo.InvariantCulture),
				row.FailureStage ?? string.Empty, row.Error ?? string.Empty,
				NormalizeXml(row.RequestXml),
				NormalizeXml(row.BaselineXml), NormalizeXml(row.RadarResponseXml)
			};
			await writer.WriteLineAsync(string.Join(',', summary.Select(Escape)));

			foreach (var field in fields.OrderBy(field => field.FieldKey, StringComparer.Ordinal))
			{
				var detail = new[]
				{
					"FIELD", buildId, row.ScenarioId, row.QuoteRef, row.ProductCode, row.SchemeCode,
					row.SchemaProfile, field.FieldKey, field.ExpectedPath, field.ActualPath,
					FormatDecimal(field.Expected), FormatDecimal(field.Actual), FormatDecimal(field.Delta),
					row.MinThreshold.ToString(CultureInfo.InvariantCulture), row.MaxThreshold.ToString(CultureInfo.InvariantCulture),
					ToCsvValue(field.Result), ToCsvValue(row.Result), string.Empty, string.Empty, string.Empty,
					string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
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
