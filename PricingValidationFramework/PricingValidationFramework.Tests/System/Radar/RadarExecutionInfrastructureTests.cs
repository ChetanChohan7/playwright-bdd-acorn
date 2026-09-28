using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Validation;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class RadarExecutionInfrastructureTests
{
	[Test]
	public void Scenario_case_generation_should_create_safe_named_cases_and_retain_tags()
	{
		var cases = RadarScenarioTestCases.Create(
		[
			CreateScenario("SCENARIO-1", "smoke;nightly"),
			CreateScenario("SCENARIO 2", "regression")
		]);

		Assert.Multiple(() =>
		{
			Assert.That(cases, Has.Count.EqualTo(2));
			Assert.That(cases[0].TestName, Is.EqualTo("Radar_scenario_SCENARIO-1"));
			Assert.That(cases[0].TestName, Does.Not.Contain("secret-quote"));
			Assert.That(cases[0].TestName, Does.Not.Contain("HOME"));
			Assert.That(cases[0].Properties["Category"], Does.Contain("smoke"));
			Assert.That(cases[0].Properties["Category"], Does.Contain("nightly"));
			Assert.That(cases[1].TestName, Is.EqualTo("Radar_scenario_SCENARIO_2"));
		});
	}

	[Test]
	public void Scenario_case_generation_should_return_one_failure_case_for_duplicate_ids()
	{
		var cases = RadarScenarioTestCases.Create(
		[
			CreateScenario("Duplicate", string.Empty),
			CreateScenario("DUPLICATE", string.Empty)
		]);

		Assert.Multiple(() =>
		{
			Assert.That(cases, Has.Count.EqualTo(1));
			Assert.That(cases[0].TestName, Is.EqualTo("Radar_scenario_discovery_should_succeed"));
			Assert.That(cases[0].Arguments[1], Is.EqualTo("Duplicate ScenarioId values were returned for the selected workload."));
		});
	}

	[Test]
	public void Scenario_case_generation_should_return_one_failure_case_when_discovery_is_empty()
	{
		var cases = RadarScenarioTestCases.Create([]);

		Assert.Multiple(() =>
		{
			Assert.That(cases, Has.Count.EqualTo(1));
			Assert.That(cases[0].Properties["Category"], Does.Contain("RadarDiscovery"));
		});
	}

	[Test]
	public void Report_collection_should_accept_concurrent_rows_once_and_sort_them()
	{
		var collection = new RadarReportCollection();
		Parallel.ForEach(new[] { "SCENARIO-3", "SCENARIO-1", "SCENARIO-2" }, scenarioId =>
			collection.Add(CreateRow(scenarioId)));

		Assert.Multiple(() =>
		{
			Assert.That(collection.Count, Is.EqualTo(3));
			Assert.That(collection.GetSortedRows().Select(row => row.ScenarioId),
				Is.EqualTo(new[] { "SCENARIO-1", "SCENARIO-2", "SCENARIO-3" }));
			Assert.That(() => collection.Add(CreateRow("SCENARIO-1")),
				Throws.InvalidOperationException.With.Message.Contains("terminal Radar report row"));
			Assert.That(() => collection.Add(CreateRow("scenario-1")),
				Throws.InvalidOperationException.With.Message.Contains("terminal Radar report row"));
		});
	}

	[Test]
	public async Task Radar_report_should_use_sanitized_build_id_filename()
	{
		const string buildId = "build/id:42";
		const string safeFileName = "Radar_build_id_42.csv";
		var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports", safeFileName);

		await RadarReportingHelper.WriteReportAsync(buildId, [CreateRow("SCENARIO-1")], CancellationToken.None);

		try
		{
			Assert.That(File.Exists(reportPath), Is.True);
			Assert.That(await File.ReadAllTextAsync(reportPath), Does.Contain(buildId));
		}
		finally
		{
			File.Delete(reportPath);
		}
	}

	[Test]
	public async Task Radar_report_failure_should_remove_temporary_file_and_preserve_collected_rows()
	{
		const string buildId = "atomic-failure";
		var reportDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports");
		var finalPath = Path.Combine(reportDirectory, $"Radar_{buildId}.csv");
		Directory.CreateDirectory(finalPath);
		var collection = new RadarReportCollection();
		collection.Add(CreateRow("SCENARIO-1"));

		try
		{
			Assert.That(async () =>
				await RadarReportingHelper.WriteReportAsync(buildId, collection.GetSortedRows(), CancellationToken.None),
				Throws.InstanceOf<Exception>());
			Assert.That(collection.Count, Is.EqualTo(1));
			Assert.That(Directory.GetFiles(reportDirectory, $".Radar_{buildId}.csv.*.tmp"), Is.Empty);
		}
		finally
		{
			Directory.Delete(finalPath);
		}
	}

	[TestCase(0, 4)]
	[TestCase(-1, 4)]
	[TestCase(2, 0)]
	[TestCase(2, -1)]
	public void Rate_limiter_settings_should_reject_non_positive_values(int requestsPerSecond, int queueLimit)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new RadarRateLimitSettings
		{
			RequestsPerSecond = requestsPerSecond,
			QueueLimit = queueLimit
		}.Validate());
	}

	[Test]
	public void Rate_limiter_settings_should_default_to_confirmed_values()
	{
		var settings = new RadarRateLimitSettings();

		Assert.Multiple(() =>
		{
			Assert.That(settings.RequestsPerSecond, Is.EqualTo(2));
			Assert.That(settings.QueueLimit, Is.EqualTo(4));
			Assert.That(typeof(RadarRateLimitSettings).GetProperty("Enabled"), Is.Null);
		});
	}

	[Test]
	public async Task Endpoint_limiters_should_have_independent_budgets_and_bounded_cancellable_queues()
	{
		using var limiter = new RadarRequestRateLimiter(
			new RadarRateLimitSettings(),
			["PricingA", "PricingB", "PricingC"]);
		await limiter.WaitAsync("PricingA");
		await limiter.WaitAsync("PricingA");

		using var cancellationTokenSource = new CancellationTokenSource();
		var queuedRequests = Enumerable.Range(0, 4)
			.Select(_ => limiter.WaitAsync("pricinga", cancellationTokenSource.Token).AsTask())
			.ToArray();
		Assert.That(queuedRequests, Has.All.Property("IsCompleted").False);

		await limiter.WaitAsync("PricingB");
		await limiter.WaitAsync("PricingB");
		Assert.ThrowsAsync<RadarRequestRateLimitException>(async () => await limiter.WaitAsync("PricingA"));

		cancellationTokenSource.Cancel();
		Assert.That(async () => await Task.WhenAll(queuedRequests), Throws.InstanceOf<OperationCanceledException>());
	}

	[Test]
	public void Limiter_registry_should_reject_unknown_and_duplicate_logical_endpoints()
	{
		using var limiter = new RadarRequestRateLimiter(new RadarRateLimitSettings(), ["PricingA"]);

		Assert.Multiple(() =>
		{
			Assert.ThrowsAsync<RadarRequestRateLimitException>(async () => await limiter.WaitAsync("PricingB"));
			Assert.Throws<ArgumentException>(() => new RadarRequestRateLimiter(
				new RadarRateLimitSettings(), ["PricingA", "pricinga"]));
		});
	}

	[TestCase("RadarRateLimitSettings__RequestsPerSecond", "0")]
	[TestCase("RadarRateLimitSettings__QueueLimit", "-1")]
	[TestCase("RetrySettings__ApiRetryAfterMaxDelaySeconds", "0")]
	[TestCase("RetrySettings__ApiRetryAfterMaxDelaySeconds", "-1")]
	public void Radar_setup_should_reject_invalid_limiter_configuration_before_database_discovery(
		string environmentVariable,
		string invalidValue)
	{
		var originalValue = Environment.GetEnvironmentVariable(environmentVariable);
		Environment.SetEnvironmentVariable(environmentVariable, invalidValue);

		try
		{
			Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
				await RadarTestSetup.DiscoverScenariosAsync());
		}
		finally
		{
			Environment.SetEnvironmentVariable(environmentVariable, originalValue);
		}
	}

	[Test]
	public void Radar_setup_should_reject_a_route_to_an_unknown_endpoint_before_database_discovery()
	{
		const string variableName = "RadarSettings__Routes__Route001__EndpointName";
		var originalValue = Environment.GetEnvironmentVariable(variableName);
		Environment.SetEnvironmentVariable(variableName, "PricingUnknown");

		try
		{
			var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
				await RadarTestSetup.DiscoverScenariosAsync());
			Assert.That(exception!.Message, Does.Contain("Every Radar route"));
		}
		finally
		{
			Environment.SetEnvironmentVariable(variableName, originalValue);
		}
	}

	[Test]
	public void Radar_setup_should_reject_case_insensitive_duplicate_endpoints_before_database_discovery()
	{
		var radarSettings = new RadarSettings
		{
			Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
			{
				["PricingA"] = new(),
				["pricinga"] = new()
			}
		};

		Assert.That(
			() => RadarTestSetup.ValidateRadarConfiguration(radarSettings, new RadarRateLimitSettings()),
			Throws.InvalidOperationException.With.Message.Contains("unique ignoring case"));
	}

	private static ScenarioRequest CreateScenario(string scenarioId, string testTags)
	{
		return new ScenarioRequest
		{
			ScenarioId = scenarioId,
			QuoteRef = "secret-quote",
			ProductCode = "HOME",
			SchemeCode = "ABC",
			XmlRequest = "<Request>secret XML</Request>",
			TestTags = testTags
		};
	}

	private static RadarValidationReportRow CreateRow(string scenarioId)
	{
		return new RadarValidationReportRow
		{
			ScenarioId = scenarioId,
			Result = ScenarioResult.Pass
		};
	}
}