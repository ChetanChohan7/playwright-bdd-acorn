using System.Net;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Reporting;
using Microsoft.Extensions.Logging.Abstractions;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class RadarPricingServiceTests
{
	private static PricingProfileConfiguration LoadProfiles() => PricingProfileConfiguration.Load(
		Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "Radar", "Fixtures", "mappings.json"));

	[TestCase("ABC")]
	[TestCase("XYS")]
	[TestCase("POL")]
	public async Task RunAsync_should_use_shared_route_key_and_return_field_comparison(string schemeCode)
	{
		HttpRequestMessage? capturedRequest = null;
		using var httpClient = new HttpClient(new StubHandler(request =>
		{
			capturedRequest = request;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(CreateXml("412.31", "49.48", "461.79", "25.00"), global::System.Text.Encoding.UTF8, "application/xml")
			};
		}));
		using var apiClient = new RadarApiClient(
			new NoWaitRateLimiter(),
			httpClient,
			NullLogger<RadarApiClient>.Instance,
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 });
		var settings = CreateSettings();
		var comparisonService = new PricingComparisonService(LoadProfiles().Profiles);
		var pricingService = new RadarPricingService(settings, apiClient, new RadarUrlBuilder(), comparisonService);

		var result = await pricingService.RunAsync(
			CreateScenario(schemeCode),
			CreateXml("412.30", "49.48", "461.78", "25.00"),
			"2024-03-01Z09:30:45",
			-0.01m,
			0.01m);

		Assert.Multiple(() =>
		{
			Assert.That(result.RouteId, Is.EqualTo("Route001"));
			Assert.That(result.Comparison.Result, Is.EqualTo(ScenarioResult.Pass));
			Assert.That(result.Comparison.Fields, Has.Count.EqualTo(4));
			Assert.That(capturedRequest, Is.Not.Null);
			Assert.That(capturedRequest!.RequestUri!.Query, Does.Contain("KeyName=shared-home"));
		});

		var reportRow = RadarReportingHelper.BuildComparisonRow(
			"build-42",
			"SCENARIO-1",
			"QUOTE-1",
			schemeCode,
			"HOME",
			"<Request />",
			CreateXml("412.30", "49.48", "461.78", "25.00"),
			result.RadarResponseXml,
			-0.01m,
			0.01m,
			result.Comparison);
		Assert.Multiple(() =>
		{
			Assert.That(reportRow.Result, Is.EqualTo(ScenarioResult.Pass));
			Assert.That(reportRow.ProfileId, Is.EqualTo("Route001"));
			Assert.That(reportRow.FieldComparisons, Has.Count.EqualTo(4));
			Assert.That(reportRow.BaselineXml, Does.Contain("412.30"));
			Assert.That(reportRow.RadarResponseXml, Does.Contain("412.31"));
		});
	}

	[Test]
	public async Task RunAsync_should_not_call_radar_when_the_scheme_has_no_profile()
	{
		var requestCount = 0;
		using var httpClient = new HttpClient(new StubHandler(_ =>
		{
			requestCount++;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(CreateXml("1", "2", "3", "4"))
			};
		}));
		using var apiClient = new RadarApiClient(
			new NoWaitRateLimiter(),
			httpClient,
			NullLogger<RadarApiClient>.Instance,
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 });
		var service = new RadarPricingService(CreateSettings(), apiClient, new RadarUrlBuilder(), CreateComparisonService());

		var result = await service.RunAsync(
			CreateScenario("POL"),
			CreateXml("1", "2", "3", "4"),
			"2024-03-01Z09:30:45",
			-0.01m,
			0.01m);

		Assert.Multiple(() =>
		{
			Assert.That(result.Comparison.Result, Is.EqualTo(ScenarioResult.Error));
			Assert.That(result.Comparison.FailureStage, Is.EqualTo("ProfileResolution"));
			Assert.That(requestCount, Is.Zero);
		});
	}

	[Test]
	public async Task RunAsync_should_not_call_radar_when_baseline_extraction_fails()
	{
		var requestCount = 0;
		using var httpClient = new HttpClient(new StubHandler(_ =>
		{
			requestCount++;
			return new HttpResponseMessage(HttpStatusCode.OK);
		}));
		using var apiClient = new RadarApiClient(
			new NoWaitRateLimiter(),
			httpClient,
			NullLogger<RadarApiClient>.Instance,
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 });
		var service = new RadarPricingService(CreateSettings(), apiClient, new RadarUrlBuilder(), CreateComparisonService());

		var result = await service.RunAsync(
			CreateScenario("ABC"),
			CreateXml("invalid", "49.48", "461.78", "25.00"),
			"2024-03-01Z09:30:45",
			-0.01m,
			0.01m);

		Assert.Multiple(() =>
		{
			Assert.That(result.Comparison.Result, Is.EqualTo(ScenarioResult.Error));
			Assert.That(result.Comparison.FailureStage, Is.EqualTo("BaselineExtraction"));
			Assert.That(requestCount, Is.Zero);
		});
	}

	[Test]
	public async Task CreateBaselineAsync_should_pass_the_radar_response_without_comparing_or_validating_it()
	{
		HttpRequestMessage? capturedRequest = null;
		using var httpClient = new HttpClient(new StubHandler(request =>
		{
			capturedRequest = request;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("<UnmappedResponse><Premium>1</Premium></UnmappedResponse>", global::System.Text.Encoding.UTF8, "application/xml")
			};
		}));
		using var apiClient = new RadarApiClient(
			new NoWaitRateLimiter(),
			httpClient,
			NullLogger<RadarApiClient>.Instance,
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 });
		var service = new RadarPricingService(CreateSettings(), apiClient, new RadarUrlBuilder(), CreateComparisonService());

		var result = await service.CreateBaselineAsync(CreateScenario("ABC"), "2024-03-01Z09:30:45");

		Assert.Multiple(() =>
		{
			Assert.That(result.RouteId, Is.EqualTo("Route001"));
			Assert.That(result.RadarResponseXml, Is.EqualTo("<UnmappedResponse><Premium>1</Premium></UnmappedResponse>"));
			Assert.That(result.Comparison.Result, Is.EqualTo(ScenarioResult.Pass));
			Assert.That(result.Comparison.FailureStage, Is.EqualTo(RadarPricingService.BaselineCreatedStage));
			Assert.That(result.Comparison.Fields, Is.Empty);
			Assert.That(capturedRequest!.RequestUri!.Query, Does.Contain("KeyName=shared-home"));
		});
	}

	[Test]
	public async Task CreateBaselineAsync_should_not_call_radar_when_no_route_matches()
	{
		var requestCount = 0;
		using var httpClient = new HttpClient(new StubHandler(_ =>
		{
			requestCount++;
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<Response />") };
		}));
		using var apiClient = new RadarApiClient(
			new NoWaitRateLimiter(),
			httpClient,
			NullLogger<RadarApiClient>.Instance,
			new RetrySettings { ApiRetryCount = 1, ApiRetryDelaySeconds = 0 });
		var service = new RadarPricingService(CreateSettings(), apiClient, new RadarUrlBuilder(), CreateComparisonService());

		var result = await service.CreateBaselineAsync(CreateScenario("UNMAPPED"), "2024-03-01Z09:30:45");

		Assert.Multiple(() =>
		{
			Assert.That(result.Comparison.Result, Is.EqualTo(ScenarioResult.Error));
			Assert.That(result.Comparison.FailureStage, Is.EqualTo("RouteResolution"));
			Assert.That(result.RadarResponseXml, Is.Empty);
			Assert.That(requestCount, Is.Zero);
		});
	}

	private RadarSettings CreateSettings()
	{
		return new RadarSettings
		{
			Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
			{
				["Endpoint1"] = new()
				{
					BaseUrl = "https://radar.example.test/quote",
					ApiKeyHeaderName = "X-API-KEY",
					ApiKeyValue = "test-key"
				}
			},
			Routes = new Dictionary<string, RadarRouteSettings>(StringComparer.Ordinal)
			{
				["Route001"] = new()
				{
					ProductCode = "HOME",
					SchemeCodes = ["ABC", "XYS", "POL"],
					EndpointName = "Endpoint1",
					RouteKey = "shared-home"
				}
			}
		};
	}

	private PricingComparisonService CreateComparisonService()
	{
		var profile = LoadProfiles().Profiles.Single();
		profile.SchemeCodes = ["ABC"];
		return new PricingComparisonService([profile]);
	}

	private static ScenarioRequest CreateScenario(string schemeCode)
	{
		return new ScenarioRequest
		{
			ScenarioId = "SCENARIO-1",
			QuoteRef = "QUOTE-1",
			ProductCode = "HOME",
			SchemeCode = schemeCode,
			XmlRequest = "<Request />"
		};
	}

	private static string CreateXml(string annualNet, string insuranceTax, string annualGross, string adminFee)
	{
		return $"""
			<PricingResponse>
			  <Premium>
			    <AnnualNet>{annualNet}</AnnualNet>
			    <InsuranceTax>{insuranceTax}</InsuranceTax>
			    <AnnualGross>{annualGross}</AnnualGross>
			  </Premium>
			  <Fees><AdminFee>{adminFee}</AdminFee></Fees>
			</PricingResponse>
			""";
	}

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(responseFactory(request));
		}
	}

	private sealed class NoWaitRateLimiter : IRadarRequestRateLimiter
	{
		public Task<HttpResponseMessage> SendAsync(
			string endpointName,
			Func<Task<HttpResponseMessage>> sendAsync,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return sendAsync();
		}
	}
}