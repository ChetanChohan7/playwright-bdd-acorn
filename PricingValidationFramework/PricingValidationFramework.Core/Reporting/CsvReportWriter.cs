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

	public async Task WriteRadarReportAsync(
		string outputPath,
		string buildId,
		IReadOnlyCollection<RadarValidationReportRow> rows,
		CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

		await using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
		await writer.WriteLineAsync("BuildId,ScenarioId,QuoteRef,SchemeCode,ProductCode,RequestXml,RadarResponseXml,RadarValue,BaselineValue,Difference,MinThreshold,MaxThreshold,Result");

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
				NormalizeXml(row.RequestXml),
				NormalizeXml(row.RadarResponseXml),
				row.RadarValue is null ? string.Empty : row.RadarValue.Value.ToString(CultureInfo.InvariantCulture),
				row.BaselineValue is null ? string.Empty : row.BaselineValue.Value.ToString(CultureInfo.InvariantCulture),
				row.Difference is null ? string.Empty : row.Difference.Value.ToString(CultureInfo.InvariantCulture),
				row.MinThreshold.ToString(CultureInfo.InvariantCulture),
				row.MaxThreshold.ToString(CultureInfo.InvariantCulture),
				ToCsvValue(row.Result)
			};

			await writer.WriteLineAsync(string.Join(',', values.Select(Escape)));
		}

		await writer.FlushAsync(cancellationToken);
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
