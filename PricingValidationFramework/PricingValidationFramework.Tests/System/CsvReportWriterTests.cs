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
			BaselineXml = "<baseline />",
			RadarResponseXml = "<response />",
			MinThreshold = -2m,
			MaxThreshold = 2m,
			Result = ScenarioResult.Pass
		}).ToArray();

		try
		{
			await new CsvReportWriter().WriteRadarReportAsync(outputPath, "build", rows);
			var csv = await File.ReadAllTextAsync(outputPath);

			Assert.That(csv, Does.StartWith("RecordType,BuildId,ScenarioId,QuoteRef,ProductCode,SchemeCode,SchemaProfile,FieldKey,ExpectedPath,ActualPath,Expected,Actual,Delta,MinDelta,MaxDelta,FieldResult,OverallResult,ComparedFields,PassedFields,FailedFields,FailureStage,Error,RequestXml,BaselineXml,ApiXml" + Environment.NewLine));
			Assert.Multiple(() =>
			{
				Assert.That(csv, Does.Contain("SUMMARY,build,S00,plain,HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S01,,HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S02,\"comma,value\",HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S03,\"say \"\"hi\"\"\",HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S04,\"carriage\rreturn\",HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S05,\"line\nfeed\",HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S06,\"line\r\nbreak\",HOME,ABC"));
				Assert.That(csv, Does.Contain("SUMMARY,build,S07,\"a,\"\"b\"\"\r\nc\",HOME,ABC"));
				Assert.That(csv, Does.Contain(",<request />,<baseline />,<response />"));
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
	public async Task WriteRadarReportAsync_should_write_summary_then_dynamic_field_rows_in_one_file()
	{
		var outputPath = Path.Combine(Path.GetTempPath(), $"radar-details-{Guid.NewGuid():N}.csv");
		var row = new RadarValidationReportRow
		{
			ScenarioId = "S1",
			QuoteRef = "Q1",
			ProductCode = "HOME",
			SchemeCode = "ABC",
			SchemaProfile = "HOME-ABC",
			RequestXml = "<request />",
			BaselineXml = "<baseline />",
			RadarResponseXml = "<response />",
			MinThreshold = -0.01m,
			MaxThreshold = 0.01m,
			Result = ScenarioResult.Fail,
			FieldComparisons =
			[
				new("AnnualNet", "/Baseline/Tom", "/Response/Potter", 412.30m, 412.31m, 0.01m, ScenarioResult.Pass),
				new("AdminFee", "/Baseline/Bob", "/Response/Motter", 25m, 30m, 5m, ScenarioResult.Fail)
			]
		};

		try
		{
			await new CsvReportWriter().WriteRadarReportAsync(outputPath, "build", [row]);
			var lines = await File.ReadAllLinesAsync(outputPath);

			Assert.Multiple(() =>
			{
				Assert.That(lines, Has.Length.EqualTo(4));
				Assert.That(lines[1], Does.StartWith("SUMMARY,build,S1,Q1,HOME,ABC,HOME-ABC"));
				Assert.That(lines[2], Does.StartWith("FIELD,build,S1,Q1,HOME,ABC,HOME-ABC,AdminFee,/Baseline/Bob,/Response/Motter,25,30,5,-0.01,0.01,FAIL,FAIL"));
				Assert.That(lines[3], Does.StartWith("FIELD,build,S1,Q1,HOME,ABC,HOME-ABC,AnnualNet,/Baseline/Tom,/Response/Potter,412.30,412.31,0.01,-0.01,0.01,PASS,FAIL"));
				Assert.That(lines[1], Does.EndWith(",<request />,<baseline />,<response />"));
				Assert.That(lines[2], Does.EndWith(",,"));
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