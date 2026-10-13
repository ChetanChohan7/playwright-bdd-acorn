namespace PricingValidationFramework.Core.Seeding;

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Validation;

public interface IBaselineSeedStore
{
	Task<IReadOnlyList<ScenarioRequest>> GetScenariosWithoutBaselineAsync(CancellationToken cancellationToken = default);

	/// False when the scenario already has a baseline; the existing row is never replaced.
	Task<bool> TryInsertBaselineAsync(ScenarioResponse baseline, CancellationToken cancellationToken = default);
}

public sealed class SqlBaselineSeedStore(RequestDataReader requestReader, ResultUpdater resultUpdater) : IBaselineSeedStore
{
	public Task<IReadOnlyList<ScenarioRequest>> GetScenariosWithoutBaselineAsync(CancellationToken cancellationToken = default) =>
		requestReader.GetScenariosWithoutBaselineAsync(cancellationToken);

	public Task<bool> TryInsertBaselineAsync(ScenarioResponse baseline, CancellationToken cancellationToken = default) =>
		resultUpdater.TryInsertBaselineAsync(baseline, cancellationToken);
}

/// Creates the first baseline for every [Xml].[Request] scenario with no [Xml].[Response] row:
/// calls Radar at the given request time and stores the response as a PASS baseline, without
/// comparing it. Scenarios that already have a baseline (of any status) are never read or changed.
public sealed class BaselineSeeder(IBaselineSeedStore store, RadarPricingService pricingService)
{
	public const int MaxParallelLimit = 16;

	public async Task<BaselineSeedSummary> RunAsync(
		string? requestTime,
		int maxParallel = 4,
		Action<BaselineSeedResult>? onResult = null,
		CancellationToken cancellationToken = default)
	{
		if (maxParallel is < 1 or > MaxParallelLimit)
		{
			throw new ArgumentOutOfRangeException(nameof(maxParallel), $"maxParallel must be between 1 and {MaxParallelLimit}.");
		}

		var summary = new BaselineSeedSummary { RequestTime = RequestTimeFormatter.Resolve(requestTime) };
		var results = new ConcurrentDictionary<string, BaselineSeedResult>(StringComparer.Ordinal);
		IReadOnlyList<ScenarioRequest> scenarios = [];
		try
		{
			scenarios = await store.GetScenariosWithoutBaselineAsync(cancellationToken);
			summary.ScenariosWithoutBaseline = scenarios.Count;

			await Parallel.ForEachAsync(
				scenarios,
				new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = cancellationToken },
				async (scenario, token) =>
				{
					var result = await SeedAsync(scenario, summary.RequestTime, token);
					results[scenario.ScenarioId] = result;
					onResult?.Invoke(result);
				});
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			summary.Cancelled = true;
		}

		// Report in the order the scenarios were read (creation date, then ID).
		summary.Results.AddRange(scenarios
			.Where(scenario => results.ContainsKey(scenario.ScenarioId))
			.Select(scenario => results[scenario.ScenarioId]));
		return summary;
	}

	private async Task<BaselineSeedResult> SeedAsync(ScenarioRequest scenario, string requestTime, CancellationToken cancellationToken)
	{
		string routeId = string.Empty;
		try
		{
			var run = await pricingService.CreateBaselineAsync(scenario, requestTime, cancellationToken);
			routeId = run.RouteId;
			if (run.Comparison.Result != ScenarioResult.Pass)
			{
				return Result(scenario, routeId, BaselineSeedOutcome.Failed, run.Comparison.FailureStage, run.Comparison.Error);
			}

			var inserted = await store.TryInsertBaselineAsync(new ScenarioResponse
			{
				ScenarioId = scenario.ScenarioId,
				QuoteRef = scenario.QuoteRef,
				XmlResponse = run.RadarResponseXml,
				Status = "PASS"
			}, cancellationToken);

			return inserted
				? Result(scenario, routeId, BaselineSeedOutcome.Seeded)
				: Result(scenario, routeId, BaselineSeedOutcome.AlreadyExists, "BaselineInsert",
					"A baseline was written by another run after this one started; it was left unchanged.");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (RadarRequestRateLimitException exception)
		{
			return Result(scenario, routeId, BaselineSeedOutcome.Failed, "RateLimitPermit", exception.Message);
		}
		catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
		{
			return Result(scenario, routeId, BaselineSeedOutcome.Failed, "Http429",
				"Radar kept returning HTTP 429 after the configured retries.");
		}
		catch (Exception exception)
		{
			// Radar call failures are already returned as results; this is the database insert.
			return Result(scenario, routeId, BaselineSeedOutcome.Failed, "BaselineInsert",
				$"{exception.GetType().Name}: {exception.Message}");
		}
	}

	private static BaselineSeedResult Result(
		ScenarioRequest scenario,
		string routeId,
		BaselineSeedOutcome outcome,
		string? stage = null,
		string? error = null) =>
		new(scenario.ScenarioId, scenario.ProductCode, scenario.SchemeCode, routeId, outcome, stage, error);
}

[JsonConverter(typeof(JsonStringEnumConverter<BaselineSeedOutcome>))]
public enum BaselineSeedOutcome
{
	Seeded,
	AlreadyExists,
	Failed
}

/// One scenario's outcome. Holds no XML, so the summary is safe to publish.
public sealed record BaselineSeedResult(
	string ScenarioId,
	string ProductCode,
	string SchemeCode,
	string RouteId,
	BaselineSeedOutcome Outcome,
	string? Stage,
	string? Error);

public sealed class BaselineSeedSummary
{
	public string Mode => "seed";
	public string RequestTime { get; set; } = string.Empty;
	public int ScenariosWithoutBaseline { get; set; }
	public int Seeded => Results.Count(result => result.Outcome == BaselineSeedOutcome.Seeded);
	public int AlreadyExisted => Results.Count(result => result.Outcome == BaselineSeedOutcome.AlreadyExists);
	public int Failed => Results.Count(result => result.Outcome == BaselineSeedOutcome.Failed);
	public int NotRun => ScenariosWithoutBaseline - Results.Count;
	public bool Cancelled { get; set; }
	public bool Succeeded => !Cancelled && Failed == 0 && Issues.Count == 0;
	public List<string> Issues { get; } = [];
	public List<BaselineSeedResult> Results { get; } = [];
}
