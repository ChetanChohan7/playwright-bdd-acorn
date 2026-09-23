namespace PricingValidationFramework.Core.Reporting;

using System.Globalization;
using System.Text;
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
				row.IceValue.ToString(CultureInfo.InvariantCulture),
				row.BaselineValue.ToString(CultureInfo.InvariantCulture),
				row.Result
			};

			await writer.WriteLineAsync(string.Join(',', values.Select(Escape)));
		}

		await writer.FlushAsync(cancellationToken);
	}

	private static string Escape(string value)
	{
		return value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
			? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
			: value;
	}
}
