using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Reporting;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class CsvReportWriterTests
{
	[Test]
	public async Task WriteIceReportAsync_should_quote_special_fields_and_preserve_line_breaks()
	{
		var outputPath = Path.Combine(Path.GetTempPath(), $"ice-csv-{Guid.NewGuid():N}.csv");
		var values = new[]
		{
			"plain",
			string.Empty,
			"comma,value",
			"say \"hi\"",
			"carriage\rreturn",
			"line\nfeed",
			"line\r\nbreak",
			"a,\"b\"\r\nc"
		};
		var rows = values.Select((value, index) => new IceValidationReportRow
		{
			ScenarioId = $"S{index:00}",
			QuoteRef = value,
			SchemeCode = "ABC",
			ProductCode = "HOME",
			Result = ScenarioResult.Pass
		}).ToArray();

		try
		{
			await new CsvReportWriter().WriteIceReportAsync(outputPath, "build", rows);
			var csv = await File.ReadAllTextAsync(outputPath);

			Assert.That(csv, Does.StartWith("BuildId,ScenarioId,QuoteRef,SchemeCode,ProductCode,IceValue,BaselineValue,Result" + Environment.NewLine));
			Assert.Multiple(() =>
			{
				Assert.That(csv, Does.Contain("build,S00,plain,ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S01,,ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S02,\"comma,value\",ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S03,\"say \"\"hi\"\"\",ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S04,\"carriage\rreturn\",ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S05,\"line\nfeed\",ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S06,\"line\r\nbreak\",ABC,HOME,,,PASS"));
				Assert.That(csv, Does.Contain("build,S07,\"a,\"\"b\"\"\r\nc\",ABC,HOME,,,PASS"));
			});
		}
		finally
		{
			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}
		}
	}

	[Test]
	public async Task WriteRadarReportAsync_should_quote_special_fields_and_preserve_line_breaks()
	{
		var outputPath = Path.Combine(Path.GetTempPath(), $"radar-csv-{Guid.NewGuid():N}.csv");
		var values = new[]
		{
			"plain",
			string.Empty,
			"comma,value",
			"say \"hi\"",
			"carriage\rreturn",
			"line\nfeed",
			"line\r\nbreak",
			"a,\"b\"\r\nc"
		};
		var rows = values.Select((value, index) => new RadarValidationReportRow
		{
			ScenarioId = $"S{index:00}",
			QuoteRef = value,
			SchemeCode = "ABC",
			ProductCode = "HOME",
			RequestXml = "<request />",
			RadarResponseXml = "<response />",
			RadarValue = 1m,
			BaselineValue = 2m,
			Difference = -1m,
			MinThreshold = -2m,
			MaxThreshold = 2m,
			Result = ScenarioResult.Pass
		}).ToArray();

		try
		{
			await new CsvReportWriter().WriteRadarReportAsync(outputPath, "build", rows);
			var csv = await File.ReadAllTextAsync(outputPath);

			Assert.That(csv, Does.StartWith("BuildId,ScenarioId,QuoteRef,SchemeCode,ProductCode,RequestXml,RadarResponseXml,RadarValue,BaselineValue,Difference,MinThreshold,MaxThreshold,Result" + Environment.NewLine));
			Assert.Multiple(() =>
			{
				Assert.That(csv, Does.Contain("build,S00,plain,ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S01,,ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S02,\"comma,value\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S03,\"say \"\"hi\"\"\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S04,\"carriage\rreturn\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S05,\"line\nfeed\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S06,\"line\r\nbreak\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
				Assert.That(csv, Does.Contain("build,S07,\"a,\"\"b\"\"\r\nc\",ABC,HOME,<request />,<response />,1,2,-1,-2,2,PASS"));
			});
		}
		finally
		{
			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}
		}
	}
}