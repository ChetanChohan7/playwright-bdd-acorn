# Manual update: all changes since commit 2dd63e1

Every file changed on `loader-values-batches-update` since commit `2dd63e1`, with what to do to each. It covers:

- **Seed changes:**
  - the DataLoader `seed` command
  - Radar compares only (a missing baseline is an ERROR)
  - the data-loader pipeline's Seed baselines stage and `seedOnly` parameter
- **Follow-up changes:**
  - ICE as a stage of the Radar pipeline, checking the scenarios that passed in that run's Radar report
  - scenario tags (`test_tags` CSV column, `SCENARIO_TAG` filter)
  - local-run settings for the Radar and ICE tests, and VS Code debugging
  - `.gitignore` entries for local settings

Paths are relative to the `PricingValidationFramework` folder, the one containing `PricingValidationFramework.slnx`, except `.gitignore`, which is one folder up at the repository root.

## Where are you starting from?

- **Commit 2dd63e1** (e.g. made by the original `create-pricing-validation-framework.bat`): do every row of the table.
- **Seed changes already applied** (from the earlier version of this document or `apply-seed-changes.bat`): do only the rows marked **yes** under *Changed after seed*. For `README.md` and `ARCHITECTURE.md`, use the *If you already applied the seed changes* edits.

`apply-seed-changes.bat` does all of this automatically from either starting point. It checks the folder first and backs up every file it replaces.

Then run `dotnet test PricingValidationFramework.slnx`; it should report 336 passed. If a file has local edits of yours (e.g. `RadarTestSetup.cs` from debugging), pasting the whole file replaces them, so copy them back afterwards. If you already have your own `.vscode\launch.json` or `tasks.json`, merge the new entries into yours instead of replacing the file. If you have your own `data\import\local-settings.json`, copy any new keys from the example into it.

## Summary

| # | Action | File | Changed after seed |
|---|---|---|---|
| 1 | Delete | `PricingValidationFramework.Tests\System\Radar\RadarBaselineOnlySettingTests.cs` | no |
| 2 | Add | `.vscode\launch.json` | **yes** |
| 3 | Add | `.vscode\tasks.json` | **yes** |
| 4 | Add | `PricingValidationFramework.Core\Configuration\RadarSettingsValidator.cs` | **yes** |
| 5 | Add | `PricingValidationFramework.Core\Seeding\BaselineSeeder.cs` | no |
| 6 | Add | `PricingValidationFramework.Core\Validation\ScenarioTags.cs` | **yes** |
| 7 | Add | `PricingValidationFramework.DataLoader\SeedCommand.cs` | **yes** |
| 8 | Add | `PricingValidationFramework.Tests\Helpers\Reporting\RadarReportScenarios.cs` | **yes** |
| 9 | Add | `PricingValidationFramework.Tests\System\Ice\IceDiscoveryFilterTests.cs` | **yes** |
| 10 | Add | `PricingValidationFramework.Tests\System\LocalRunSettingsTests.cs` | **yes** |
| 11 | Add | `PricingValidationFramework.Tests\System\Radar\BaselineSeederTests.cs` | no |
| 12 | Add | `PricingValidationFramework.Tests\System\SeedCommandTests.cs` | no |
| 13 | Add | `data\import\examples\local-settings.example.json` | **yes** |
| 14 | Replace whole file (+69 / -5 lines) | `PricingValidationFramework.Core\Database\BaselineDataReader.cs` | **yes** |
| 15 | Replace whole file (+12 / -0 lines) | `PricingValidationFramework.Core\Database\IBaselineDataReader.cs` | **yes** |
| 16 | Replace whole file (+33 / -0 lines) | `PricingValidationFramework.Core\Database\RequestDataReader.cs` | **yes** |
| 17 | Replace whole file (+12 / -5 lines) | `PricingValidationFramework.Core\Database\ResultUpdater.cs` | no |
| 18 | Replace whole file (+3 / -1 lines) | `PricingValidationFramework.Core\ExternalAPIAccess\UrlBuilders\RequestTimeFormatter.cs` | **yes** |
| 19 | Replace whole file (+4 / -4 lines) | `PricingValidationFramework.Core\Models\Common\PipelineSettings.cs` | **yes** |
| 20 | Replace whole file (+6 / -4 lines) | `PricingValidationFramework.DataLoader\CsvScenarioReader.cs` | **yes** |
| 21 | Replace whole file (+3 / -0 lines) | `PricingValidationFramework.DataLoader\PricingValidationFramework.DataLoader.csproj` | no |
| 22 | Replace whole file (+11 / -1 lines) | `PricingValidationFramework.DataLoader\Program.cs` | no |
| 23 | Replace whole file (+54 / -6 lines) | `PricingValidationFramework.DataLoader\README.md` | **yes** |
| 24 | Replace whole file (+4 / -2 lines) | `PricingValidationFramework.DataLoader\ScenarioDataLoader.cs` | **yes** |
| 25 | Replace whole file (+43 / -4 lines) | `PricingValidationFramework.Tests\Helpers\Setup\IceTestSetup.cs` | **yes** |
| 26 | Replace whole file (+18 / -110 lines) | `PricingValidationFramework.Tests\Helpers\Setup\RadarTestSetup.cs` | **yes** |
| 27 | Replace whole file (+58 / -3 lines) | `PricingValidationFramework.Tests\Helpers\Setup\TestConfigurationLoader.cs` | **yes** |
| 28 | Replace whole file (+6 / -3 lines) | `PricingValidationFramework.Tests\Integration\Ice\IceValidationTests.cs` | **yes** |
| 29 | Replace whole file (+87 / -81 lines) | `PricingValidationFramework.Tests\Integration\Radar\RadarValidationTests.cs` | **yes** |
| 30 | Replace whole file (+53 / -0 lines) | `PricingValidationFramework.Tests\System\DataLoaderTests.cs` | **yes** |
| 31 | Replace whole file (+63 / -0 lines) | `PricingValidationFramework.Tests\System\DatabaseQueryContractTests.cs` | **yes** |
| 32 | Replace whole file (+7 / -5 lines) | `PricingValidationFramework.Tests\System\Radar\RadarExecutionInfrastructureTests.cs` | **yes** |
| 33 | Replace whole file (+145 / -157 lines) | `azure-pipelines-data-loader.yml` | no |
| 34 | Replace whole file (+12 / -0 lines) | `azure-pipelines-ice.yml` | **yes** |
| 35 | Replace whole file (+160 / -60 lines) | `azure-pipelines-radar.yml` | **yes** |
| 36 | Find/replace edits | `ARCHITECTURE.md` | **yes** |
| 37 | Find/replace edits | `README.md` | **yes** |
| 38 | Find/replace edits | `..\.gitignore` (repository root) | **yes** |

## 1. Delete

### `PricingValidationFramework.Tests\System\Radar\RadarBaselineOnlySettingTests.cs`

Delete this file. It tested the removed `RADAR_BASELINE_ONLY` flag.

## 2. Add new files

Create any missing folders. If you already have the file (from the seed changes), replace its contents.

### `.vscode\launch.json`

```json
{
  // Open the PricingValidationFramework folder (the one holding this .vscode folder) in VS Code,
  // then pick a configuration in Run and Debug. 1-3 build the DataLoader first (tasks.json).
  // Database, Radar and run settings come from data/import/local-settings.json: copy
  // data/import/examples/local-settings.example.json there and fill it in. It is ignored by Git.
  // The Radar and ICE tests read the same file.
  //
  // To debug the Radar or ICE tests: Terminal > Run Task > "Radar tests: wait for debugger" (or
  // "ICE tests: ..."). It builds, then prints "Process Id: NNNN, Name: testhost" and waits. Start
  // "4. Attach to Radar/ICE test host" and pick that testhost process; breakpoints then hit.
  "version": "0.2.0",
  "configurations": [
    {
      "name": "1. DataLoader: validate (no database)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build DataLoader",
      "program": "${workspaceFolder}/PricingValidationFramework.DataLoader/bin/Debug/net10.0/PricingValidationFramework.DataLoader.dll",
      "args": ["validate", "--requests", "data/import", "--report", "TestResults/DataLoader/validation-summary.json"],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "2. DataLoader: import into [Xml].[Request]",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build DataLoader",
      "program": "${workspaceFolder}/PricingValidationFramework.DataLoader/bin/Debug/net10.0/PricingValidationFramework.DataLoader.dll",
      "args": ["import", "--requests", "data/import", "--settings", "data/import/local-settings.json", "--report", "TestResults/DataLoader/import-summary.json"],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "3. DataLoader: seed baselines into [Xml].[Response]",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build DataLoader",
      "program": "${workspaceFolder}/PricingValidationFramework.DataLoader/bin/Debug/net10.0/PricingValidationFramework.DataLoader.dll",
      "args": ["seed", "--settings", "data/import/local-settings.json", "--max-parallel", "1", "--report", "TestResults/DataLoader/seed-summary.json"],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "4. Attach to Radar/ICE test host",
      "type": "coreclr",
      "request": "attach",
      "processId": "${command:pickProcess}"
    }
  ]
}
```

### `.vscode\tasks.json`

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build DataLoader",
      "type": "process",
      "command": "dotnet",
      "args": [
        "build",
        "${workspaceFolder}/PricingValidationFramework.DataLoader/PricingValidationFramework.DataLoader.csproj"
      ],
      "problemMatcher": "$msCompile",
      "group": "build"
    },
    {
      "label": "Radar tests: run",
      "type": "process",
      "command": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj",
        "--filter",
        "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Radar.RadarValidationTests",
        "--",
        "NUnit.ExplicitMode=Relaxed"
      ],
      "problemMatcher": "$msCompile",
      "group": "test"
    },
    {
      "label": "Radar tests: wait for debugger",
      "type": "process",
      "command": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj",
        "--filter",
        "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Radar.RadarValidationTests",
        "--",
        "NUnit.ExplicitMode=Relaxed"
      ],
      "options": {
        "env": {
          "VSTEST_HOST_DEBUG": "1"
        }
      },
      "problemMatcher": "$msCompile",
      "group": "test"
    },
    {
      "label": "ICE tests: run",
      "type": "process",
      "command": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj",
        "--filter",
        "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Ice.IceValidationTests",
        "--",
        "NUnit.ExplicitMode=Relaxed"
      ],
      "problemMatcher": "$msCompile",
      "group": "test"
    },
    {
      "label": "ICE tests: wait for debugger",
      "type": "process",
      "command": "dotnet",
      "args": [
        "test",
        "${workspaceFolder}/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj",
        "--filter",
        "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Ice.IceValidationTests",
        "--",
        "NUnit.ExplicitMode=Relaxed"
      ],
      "options": {
        "env": {
          "VSTEST_HOST_DEBUG": "1"
        }
      },
      "problemMatcher": "$msCompile",
      "group": "test"
    }
  ]
}
```

### `PricingValidationFramework.Core\Configuration\RadarSettingsValidator.cs`

```csharp
namespace PricingValidationFramework.Core.Configuration;

/// Shared by the Radar tests and the DataLoader seed command, so both reject the same bad
/// endpoint and route settings before any scenario is read or Radar is called.
public static class RadarSettingsValidator
{
	/// Drops endpoints whose BaseUrl, header name and key are all empty (unused pipeline variables, or
	/// placeholders left blank in a local settings file), so only the endpoints in use need values.
	/// A partly filled endpoint is kept, and Validate rejects it.
	public static void RemoveBlankEndpoints(RadarSettings radarSettings)
	{
		ArgumentNullException.ThrowIfNull(radarSettings);
		radarSettings.Endpoints = (radarSettings.Endpoints ?? []).Where(endpoint => endpoint.Value is not null &&
				!(string.IsNullOrWhiteSpace(endpoint.Value.BaseUrl) &&
				  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyHeaderName) &&
				  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyValue)))
			.ToDictionary(endpoint => endpoint.Key, endpoint => endpoint.Value, StringComparer.Ordinal);
	}

	/// <param name="requireEndpointForEveryRoute">
	/// True requires every route's endpoint to be configured. False (Radar runs and seeding) lets a
	/// route name an endpoint that isn't configured; its scenarios fail with EndpointResolution
	/// instead, so a run can configure only the endpoints it needs.
	/// </param>
	public static void Validate(
		RadarSettings radarSettings,
		RadarRateLimitSettings rateLimitSettings,
		bool requireEndpointForEveryRoute = true)
	{
		ArgumentNullException.ThrowIfNull(radarSettings);
		ArgumentNullException.ThrowIfNull(rateLimitSettings);
		rateLimitSettings.Validate();

		if (radarSettings.Endpoints is null || radarSettings.Endpoints.Count == 0)
		{
			throw new InvalidOperationException("At least one Radar logical endpoint must be configured.");
		}

		var endpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var endpointName in radarSettings.Endpoints.Keys)
		{
			if (string.IsNullOrWhiteSpace(endpointName))
			{
				throw new InvalidOperationException("Radar logical endpoint names must not be empty.");
			}

			if (endpointName.Any(character =>
				!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.')))
			{
				throw new InvalidOperationException("Radar logical endpoint names contain unsupported characters.");
			}

			if (!endpointNames.Add(endpointName))
			{
				throw new InvalidOperationException("Radar logical endpoint names must be unique ignoring case.");
			}

			var endpoint = radarSettings.Endpoints[endpointName];
			if (endpoint is null ||
				!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var baseUri) ||
				(baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an absolute HTTP or HTTPS BaseUrl.");
			}

			if (string.IsNullOrWhiteSpace(endpoint.ApiKeyHeaderName))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an ApiKeyHeaderName.");
			}

			if (string.IsNullOrWhiteSpace(endpoint.ApiKeyValue))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an ApiKeyValue.");
			}
		}

		if (radarSettings.Routes is null)
		{
			throw new InvalidOperationException("Radar routes are missing.");
		}

		if (radarSettings.Routes.Count == 0)
		{
			throw new InvalidOperationException("At least one Radar route must be configured.");
		}

		foreach (var route in radarSettings.Routes)
		{
			if (string.IsNullOrWhiteSpace(route.Value?.ProductCode) ||
				route.Value.SchemeCodes is null ||
				route.Value.SchemeCodes.Count == 0 ||
				route.Value.SchemeCodes.Any(string.IsNullOrWhiteSpace))
			{
				throw new InvalidOperationException("Every Radar route must have a ProductCode and at least one non-empty SchemeCode.");
			}

			if (string.IsNullOrWhiteSpace(route.Value.EndpointName) ||
				(requireEndpointForEveryRoute && !endpointNames.Contains(route.Value.EndpointName)))
			{
				throw new InvalidOperationException("Every Radar route must reference a configured logical endpoint.");
			}

			if (string.IsNullOrWhiteSpace(route.Value.RouteKey))
			{
				throw new InvalidOperationException("Every Radar route must have a RouteKey.");
			}
		}
	}
}
```

### `PricingValidationFramework.Core\Seeding\BaselineSeeder.cs`

```csharp
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
```

### `PricingValidationFramework.Core\Validation\ScenarioTags.cs`

```csharp
namespace PricingValidationFramework.Core.Validation;

/// The one rule for scenario tags, shared by the loader (writing Test_tags) and the Radar/ICE
/// SCENARIO_TAG filter (reading them): letters, digits, '_', '-' and '.', at most 64 characters.
/// Test_tags stores them comma-separated, e.g. "smoke,regression".
public static class ScenarioTags
{
	public const int MaxTagLength = 64;

	public static bool IsValid(string? tag) =>
		!string.IsNullOrWhiteSpace(tag) &&
		tag.Length <= MaxTagLength &&
		tag.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

	/// Splits on ',' or ';', trims, removes duplicates (ignoring case) and joins with ','. Empty input
	/// gives "". Throws InvalidDataException naming the first invalid tag.
	public static string Normalize(string? tags)
	{
		var parts = (tags ?? string.Empty)
			.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var invalid = parts.FirstOrDefault(tag => !IsValid(tag));
		if (invalid is not null)
		{
			throw new InvalidDataException(
				$"Test tag '{invalid}' is invalid: use letters, digits, '_', '-' or '.' (at most {MaxTagLength} characters), separated by commas.");
		}

		return string.Join(',', parts);
	}

	/// For a filter value: trimmed, or empty when not set. Throws when it isn't a single valid tag.
	public static string ValidateFilter(string? tag, string settingName)
	{
		if (string.IsNullOrWhiteSpace(tag))
		{
			return string.Empty;
		}

		var trimmed = tag.Trim();
		return IsValid(trimmed)
			? trimmed
			: throw new InvalidOperationException(
				$"{settingName} must be one tag of letters, digits, '_', '-' or '.' (at most {MaxTagLength} characters).");
	}
}
```

### `PricingValidationFramework.DataLoader\SeedCommand.cs`

```csharp
namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Seeding;
using PricingValidationFramework.Core.Validation;

/// Everything `DataLoader seed` needs, read from the optional --settings JSON with environment
/// variables on top (the same names the Radar pipeline uses, e.g. RadarSettings__Endpoints__Endpoint1__BaseUrl).
public sealed record SeedSettings(
    DatabaseSettings Database,
    RadarSettings Radar,
    RadarRateLimitSettings RateLimit,
    RetrySettings Retry,
    string MappingFile,
    string? RequestTime);

public static class SeedCommand
{
    public const string DefaultMappingFile = "Radar/mappings.json";
    public static readonly string DefaultReportPath = Path.Combine("TestResults", "DataLoader", "seed-summary.json");

    private static readonly string[] Options = ["--settings", "--report", "--request-time", "--max-parallel"];

    public static async Task<int> RunAsync(string[] optionArgs, CancellationToken cancellationToken)
    {
        var reportPath = DefaultReportPath;
        var requestTime = string.Empty;
        BaselineSeedSummary summary;
        try
        {
            var values = ParseOptions(optionArgs);
            reportPath = values.GetValueOrDefault("--report", reportPath);
            var maxParallel = int.Parse(values.GetValueOrDefault("--max-parallel", "4"), CultureInfo.InvariantCulture);
            var settings = LoadSettings(
                BuildConfiguration(values.GetValueOrDefault("--settings")),
                values.GetValueOrDefault("--request-time"),
                AppContext.BaseDirectory);
            // Resolve "now" once, so the summary and every Radar call use the same hour.
            requestTime = RequestTimeFormatter.Resolve(settings.RequestTime);

            Console.WriteLine($"seed: mapping={settings.MappingFile}, endpoints={string.Join(",", settings.Radar.Endpoints.Keys)}, maxParallel={maxParallel}.");
            summary = await RunAsync(settings with { RequestTime = requestTime }, maxParallel, cancellationToken);
        }
        catch (Exception exception)
        {
            summary = new BaselineSeedSummary { RequestTime = requestTime, Cancelled = exception is OperationCanceledException && cancellationToken.IsCancellationRequested };
            summary.Issues.Add($"The seed command could not complete ({exception.GetType().Name}): {exception.Message}");
            Console.Error.WriteLine(summary.Issues[0]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None);
        Console.WriteLine($"seed: requestTime={summary.RequestTime}, success={summary.Succeeded}, withoutBaseline={summary.ScenariosWithoutBaseline}, " +
            $"seeded={summary.Seeded}, alreadyExisted={summary.AlreadyExisted}, failed={summary.Failed}, notRun={summary.NotRun}, report={Path.GetFullPath(reportPath)}.");
        return summary.Cancelled ? 130 : summary.Succeeded ? 0 : 1;
    }

    private static async Task<BaselineSeedSummary> RunAsync(SeedSettings settings, int maxParallel, CancellationToken cancellationToken)
    {
        var connectionFactory = new SqlConnectionFactory(settings.Database);
        using var rateLimiter = new RadarRequestRateLimiter(settings.RateLimit, settings.Radar.Endpoints.Keys);
        using var apiClient = new RadarApiClient(
            rateLimiter,
            new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }),
            retrySettings: settings.Retry);
        var pricingService = new RadarPricingService(
            settings.Radar,
            apiClient,
            new RadarUrlBuilder(),
            // Seeding never compares, so no XPath profiles are loaded.
            new PricingComparisonService([]));
        var seeder = new BaselineSeeder(
            new SqlBaselineSeedStore(
                new RequestDataReader(connectionFactory, settings.Retry),
                new ResultUpdater(connectionFactory, settings.Retry)),
            pricingService);

        return await seeder.RunAsync(settings.RequestTime, maxParallel, result =>
            Console.WriteLine(result.Outcome == BaselineSeedOutcome.Seeded
                ? $"  SEEDED  {result.ScenarioId} ({result.ProductCode}/{result.SchemeCode}, route {result.RouteId})"
                : $"  {result.Outcome.ToString().ToUpperInvariant()}  {result.ScenarioId} ({result.ProductCode}/{result.SchemeCode}) {result.Stage}: {result.Error}"),
            cancellationToken);
    }

    public static IConfiguration BuildConfiguration(string? settingsPath)
    {
        var builder = new ConfigurationBuilder();
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            builder.AddJsonFile(Path.GetFullPath(settingsPath), optional: false, reloadOnChange: false);
        }

        return builder.AddEnvironmentVariables().Build();
    }

    public static SeedSettings LoadSettings(IConfiguration configuration, string? requestTimeOption, string baseDirectory)
    {
        var database = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>() ?? new DatabaseSettings();
        if (string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            throw new InvalidOperationException("DatabaseSettings:ConnectionString is required (DatabaseSettings__ConnectionString or the --settings file).");
        }

        var radar = configuration.GetSection("RadarSettings").Get<RadarSettings>() ?? new RadarSettings();
        // A local run only needs the endpoint its scenarios use; completely blank ones are ignored.
        RadarSettingsValidator.RemoveBlankEndpoints(radar);

        var mappingFile = PricingProfileConfiguration.ResolveMappingPath(
            configuration["PricingProfiles:MappingFile"] ?? DefaultMappingFile, baseDirectory);
        radar.Routes = PricingRouteConfiguration.LoadRoutes(mappingFile).CreateRadarRoutes();

        var rateLimit = configuration.GetSection("RadarRateLimitSettings").Get<RadarRateLimitSettings>() ?? new RadarRateLimitSettings();
        RadarSettingsValidator.Validate(radar, rateLimit, requireEndpointForEveryRoute: false);

        var retry = configuration.GetSection("RetrySettings").Get<RetrySettings>() ?? new RetrySettings();
        retry.ValidateRadarPipelineSettings();

        // --request-time, then RADAR_REQUEST_DATETIME (as Radar reads it), then the settings file, then now.
        // "now" (any case) means the current UTC hour.
        var requestTime = new[] { requestTimeOption, configuration["RADAR_REQUEST_DATETIME"], configuration["PipelineSettings:RequestTime"] }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        if (string.Equals(requestTime, "now", StringComparison.OrdinalIgnoreCase))
        {
            requestTime = null;
        }

        RequestTimeFormatter.Resolve(requestTime);

        return new SeedSettings(database, radar, rateLimit, retry, mappingFile, requestTime);
    }

    private static Dictionary<string, string> ParseOptions(string[] optionArgs)
    {
        if (optionArgs.Length % 2 != 0)
        {
            throw new ArgumentException("Options must be --name value pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < optionArgs.Length; index += 2)
        {
            if (!Options.Contains(optionArgs[index]) || !values.TryAdd(optionArgs[index], optionArgs[index + 1]))
            {
                throw new ArgumentException($"Unknown or duplicate seed option '{optionArgs[index]}'. Seed takes {string.Join(", ", Options)}.");
            }
        }

        return values;
    }
}
```

### `PricingValidationFramework.Tests\Helpers\Reporting\RadarReportScenarios.cs`

```csharp
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace PricingValidationFramework.Tests.Helpers.Reporting;

/// Reads a Radar_<BuildId>.csv report back, for ICE's "this Radar run" selection.
internal static class RadarReportScenarios
{
    /// The distinct ScenarioIds whose ScenarioResult is PASS, in report order. The report repeats a
    /// scenario on one row per compared field; setup/discovery rows are never PASS.
    public static async Task<IReadOnlyList<string>> ReadPassedScenarioIdsAsync(string reportPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Radar report '{reportPath}' was not found.", reportPath);
        }

        using var reader = new StreamReader(reportPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { ExceptionMessagesContainRawData = false });
        if (!await csv.ReadAsync() || !csv.ReadHeader() ||
            !csv.HeaderRecord!.Contains("ScenarioId") || !csv.HeaderRecord.Contains("ScenarioResult"))
        {
            throw new InvalidDataException($"Radar report '{reportPath}' has no ScenarioId and ScenarioResult columns.");
        }

        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenarioId = csv.GetField("ScenarioId");
            if (string.Equals(csv.GetField("ScenarioResult"), "PASS", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(scenarioId) && seen.Add(scenarioId))
            {
                ids.Add(scenarioId);
            }
        }

        return ids;
    }
}
```

### `PricingValidationFramework.Tests\System\Ice\IceDiscoveryFilterTests.cs`

```csharp
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceDiscoveryFilterTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"ice-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [TestCase("", "")]
    [TestCase("  ", " ")]
    public async Task Ice_should_select_every_product_when_no_filter_is_set(string product, string tag)
    {
        var reader = new RecordingReader();

        await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection(product, tag, null));

        Assert.That(reader.Calls, Is.EqualTo(new[] { "all" }));
    }

    [TestCase("Carrot", "", "filtered:Carrot|")]
    [TestCase("", "smoke", "filtered:|smoke")]
    [TestCase("Carrot", "smoke", "filtered:Carrot|smoke")]
    public async Task Ice_should_use_the_same_product_and_tag_filters_as_radar(string product, string tag, string expected)
    {
        var reader = new RecordingReader();

        await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection(product, tag, null));

        Assert.That(reader.Calls, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public async Task Ice_should_select_exactly_the_scenarios_that_passed_in_the_radar_report_ignoring_filters()
    {
        var report = WriteRadarReport(
            ("S1", "PASS"), ("S1", "PASS"), ("S2", "FAIL"), ("S3", "ERROR"), ("S4", "PASS"), ("RadarDiscovery", "ERROR"));
        var reader = new RecordingReader();

        var scenarios = await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection("Carrot", "smoke", report));

        Assert.Multiple(() =>
        {
            Assert.That(reader.Calls, Is.EqualTo(new[] { "ids:S1,S4" }));
            Assert.That(scenarios.Select(scenario => scenario.ScenarioId), Is.EqualTo(new[] { "S1", "S4" }));
        });
    }

    [Test]
    public async Task Ice_should_select_nothing_when_no_scenario_passed_in_the_radar_report()
    {
        var reader = new RecordingReader();

        var scenarios = await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection("", "", WriteRadarReport(("S1", "FAIL"))));

        Assert.Multiple(() =>
        {
            Assert.That(scenarios, Is.Empty);
            Assert.That(reader.Calls, Is.Empty, "no query for an empty selection");
        });
    }

    [Test]
    public void Ice_should_fail_clearly_when_the_radar_report_is_missing()
    {
        var missing = Path.Combine(directory, "Radar_1.csv");

        Assert.That(async () => await IceTestSetup.DiscoverScenariosAsync(new RecordingReader(), new IceTestSetup.IceSelection("", "", missing)),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("Radar_1.csv"));
    }

    private string WriteRadarReport(params (string ScenarioId, string Result)[] rows)
    {
        // The real header, with multiline quoted XML in the last column as Radar writes it.
        var path = Path.Combine(directory, $"Radar_{Guid.NewGuid():N}.csv");
        var lines = new List<string> { "ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml" };
        lines.AddRange(rows.Select(row => $"{row.ScenarioId},Q,HOME,ABC,P1,Premium,/x,1,1,0,0,0,{row.Result},{row.Result},,,\"<a>\nPASS,x</a>\",,"));
        File.WriteAllText(path, string.Join("\n", lines));
        return path;
    }

    private sealed class RecordingReader : IBaselineDataReader
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("all");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([]);
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetFilteredPassingBaselineScenariosAsync(
            string? productCode,
            string? scenarioTag,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"filtered:{productCode}|{scenarioTag}");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([]);
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselinesByScenarioIdsAsync(
            IReadOnlyCollection<string> scenarioIds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"ids:{string.Join(',', scenarioIds)}");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>(
                scenarioIds.Select(id => new IceBaselineScenario { ScenarioId = id, Status = "PASS" }).ToArray());
        }

        public Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScenarioResponse?>(null);
    }
}
```

### `PricingValidationFramework.Tests\System\LocalRunSettingsTests.cs`

```csharp
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
[NonParallelizable]
public class LocalRunSettingsTests
{
    private string directory = string.Empty;
    private string? originalLocalSettings;
    private string? originalScenarioTag;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"local-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        originalLocalSettings = Environment.GetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable);
        originalScenarioTag = Environment.GetEnvironmentVariable("SCENARIO_TAG");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, null);
        Environment.SetEnvironmentVariable("SCENARIO_TAG", null);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, originalLocalSettings);
        Environment.SetEnvironmentVariable("SCENARIO_TAG", originalScenarioTag);
        Directory.Delete(directory, true);
    }

    [TestCase(null, "")]
    [TestCase(" smoke ; regression,Smoke ", "smoke,regression")]
    [TestCase("a_b,c-d,e.f", "a_b,c-d,e.f")]
    public void Tags_should_be_normalised_to_a_comma_separated_list(string? tags, string expected)
    {
        Assert.That(ScenarioTags.Normalize(tags), Is.EqualTo(expected));
    }

    [TestCase("smoke test")]
    [TestCase("smoke|regression")]
    [TestCase("50%")]
    public void Invalid_tags_should_be_rejected(string tags)
    {
        Assert.That(() => ScenarioTags.Normalize(tags), Throws.InstanceOf<InvalidDataException>());
    }

    [TestCase(null, "")]
    [TestCase(" smoke ", "smoke")]
    public void Scenario_tag_filter_should_accept_one_tag_or_nothing(string? value, string expected)
    {
        Environment.SetEnvironmentVariable("SCENARIO_TAG", value);

        Assert.That(TestConfigurationLoader.ReadScenarioTag(new ConfigurationBuilder().Build()), Is.EqualTo(expected));
    }

    [TestCase("smoke,regression")]
    [TestCase("smo%ke")]
    public void Scenario_tag_filter_should_reject_more_than_one_tag_or_bad_characters(string value)
    {
        Environment.SetEnvironmentVariable("SCENARIO_TAG", value);

        Assert.That(() => TestConfigurationLoader.ReadScenarioTag(new ConfigurationBuilder().Build()),
            Throws.InvalidOperationException.With.Message.Contains("SCENARIO_TAG"));
    }

    [TestCase("now")]
    [TestCase(" NOW ")]
    [TestCase("")]
    public void Request_time_now_should_mean_the_current_utc_hour(string value)
    {
        var expected = RequestTimeFormatter.FormatUtcNow();

        Assert.That(RequestTimeFormatter.Resolve(value), Is.EqualTo(expected).Or.EqualTo(RequestTimeFormatter.FormatUtcNow()));
    }

    [Test]
    public void Blank_endpoints_should_be_dropped_and_partly_filled_ones_kept_for_validation()
    {
        var settings = new RadarSettings
        {
            Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
            {
                ["Endpoint1"] = new() { BaseUrl = "https://radar.example.test", ApiKeyHeaderName = "X", ApiKeyValue = "k" },
                ["Endpoint2"] = new() { BaseUrl = "", ApiKeyHeaderName = " ", ApiKeyValue = "" },
                ["Endpoint3"] = new() { BaseUrl = "https://radar3.example.test" }
            }
        };

        RadarSettingsValidator.RemoveBlankEndpoints(settings);

        Assert.That(settings.Endpoints.Keys, Is.EquivalentTo(new[] { "Endpoint1", "Endpoint3" }));
    }

    [Test]
    public void Local_settings_should_be_found_beside_the_solution_from_a_build_output_folder()
    {
        var output = Directory.CreateDirectory(Path.Combine(directory, "PricingValidationFramework.Tests", "bin", "Debug", "net10.0")).FullName;
        File.WriteAllText(Path.Combine(directory, "PricingValidationFramework.slnx"), "<Solution />");
        Directory.CreateDirectory(Path.Combine(directory, "data", "import"));

        Assert.That(TestConfigurationLoader.FindLocalSettings(output), Is.Null, "no file yet");

        var settings = Path.Combine(directory, "data", "import", "local-settings.json");
        File.WriteAllText(settings, "{}");
        Assert.That(TestConfigurationLoader.FindLocalSettings(output), Is.EqualTo(settings));
    }

    [Test]
    public void Local_settings_path_can_be_given_explicitly()
    {
        var settings = Path.Combine(directory, "mine.json");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, settings);

        Assert.That(TestConfigurationLoader.FindLocalSettings("/anywhere"), Is.EqualTo(settings));
    }

    [Test]
    public void Local_settings_should_only_be_loaded_when_a_live_fixture_asks_for_them()
    {
        var settings = Path.Combine(directory, "local-settings.json");
        File.WriteAllText(settings, """{ "PipelineSettings": { "MinThreshold": "-7" } }""");
        Environment.SetEnvironmentVariable(TestConfigurationLoader.LocalSettingsVariable, settings);

        Assert.Multiple(() =>
        {
            Assert.That(TestConfigurationLoader.Load()["PipelineSettings:MinThreshold"], Is.Not.EqualTo("-7"), "unit and system tests stay isolated");
            Assert.That(TestConfigurationLoader.Load(includeLocalSettings: true)["PipelineSettings:MinThreshold"], Is.EqualTo("-7"));
        });
    }
}
```

### `PricingValidationFramework.Tests\System\Radar\BaselineSeederTests.cs`

```csharp
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Seeding;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.System.Radar;

[TestFixture]
public class BaselineSeederTests
{
    private const string ApiKey = "seed-test-key-that-must-not-appear";

    [Test]
    public async Task Seed_should_store_each_radar_response_as_a_pass_baseline_at_the_request_time()
    {
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));
        var requests = new ConcurrentBag<Uri>();
        var seeder = CreateSeeder(store, request =>
        {
            requests.Add(request.RequestUri!);
            return Ok($"<Response><Id>{request.RequestUri!.Query.Length}</Id></Response>");
        });

        var summary = await seeder.RunAsync("2024-03-01Z09:30:45");

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestTime, Is.EqualTo("2024-03-01Z09:00:00"));
            Assert.That(summary.ScenariosWithoutBaseline, Is.EqualTo(2));
            Assert.That(summary.Seeded, Is.EqualTo(2));
            Assert.That(summary.Results.Select(result => result.ScenarioId), Is.EqualTo(new[] { "S1", "S2" }), "reported in read order");
            Assert.That(summary.Results.Select(result => result.RouteId), Is.All.EqualTo("Route001"));
            Assert.That(store.Inserted.Keys, Is.EquivalentTo(new[] { "S1", "S2" }));
            Assert.That(store.Inserted.Values.Select(baseline => baseline.Status), Is.All.EqualTo("PASS"));
            Assert.That(store.Inserted.Values.Select(baseline => baseline.XmlResponse), Is.All.StartsWith("<Response>"));
            Assert.That(requests.Select(uri => Uri.UnescapeDataString(uri.Query)), Is.All.Contain("2024-03-01Z09:00:00"));
            Assert.That(requests.Select(uri => uri.Query), Is.All.Contain("KeyName=shared-home"));
        });
    }

    [Test]
    public async Task Seed_should_do_nothing_when_every_scenario_already_has_a_baseline()
    {
        var calls = 0;
        var summary = await CreateSeeder(new FakeStore(), _ => { calls++; return Ok("<Response />"); }).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.ScenariosWithoutBaseline, Is.Zero);
            Assert.That(summary.Results, Is.Empty);
            Assert.That(calls, Is.Zero);
        });
    }

    [Test]
    public async Task Seed_should_report_an_unrouted_scenario_without_calling_radar_and_carry_on()
    {
        var store = new FakeStore(Scenario("S1", scheme: "UNMAPPED"), Scenario("S2"));
        var calls = 0;
        var summary = await CreateSeeder(store, _ => { Interlocked.Increment(ref calls); return Ok("<Response />"); }).RunAsync(null);

        var unrouted = summary.Results.Single(result => result.ScenarioId == "S1");
        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Failed, Is.EqualTo(1));
            Assert.That(summary.Seeded, Is.EqualTo(1));
            Assert.That(unrouted.Outcome, Is.EqualTo(BaselineSeedOutcome.Failed));
            Assert.That(unrouted.Stage, Is.EqualTo("RouteResolution"));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(store.Inserted.Keys, Is.EquivalentTo(new[] { "S2" }));
        });
    }

    [Test]
    public async Task Seed_should_report_a_route_whose_endpoint_is_not_configured()
    {
        var settings = CreateSettings();
        settings.Routes["Route001"].EndpointName = "Endpoint2";
        var store = new FakeStore(Scenario("S1"));

        var summary = await CreateSeeder(store, _ => Ok("<Response />"), settings).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Results.Single().Stage, Is.EqualTo("EndpointResolution"));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [Test]
    public async Task Seed_should_not_store_anything_when_radar_fails_and_should_not_leak_the_api_key()
    {
        var store = new FakeStore(Scenario("S1"));
        var summary = await CreateSeeder(store, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent($"bad key {ApiKey}")
        }).RunAsync(null);

        var result = summary.Results.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BaselineSeedOutcome.Failed));
            Assert.That(result.Stage, Is.EqualTo("RequestExecution"));
            Assert.That(result.Error, Does.Not.Contain(ApiKey));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [Test]
    public async Task Seed_should_report_http_429_for_that_scenario_and_carry_on()
    {
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));
        var summary = await CreateSeeder(store, request =>
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult().Contains("<Id>S1</Id>", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Results.Single(result => result.ScenarioId == "S1").Stage, Is.EqualTo("Http429"));
            Assert.That(summary.Results.Single(result => result.ScenarioId == "S2").Outcome, Is.EqualTo(BaselineSeedOutcome.Seeded));
        });
    }

    [Test]
    public async Task Seed_should_count_a_baseline_written_by_another_run_as_already_existing_not_failed()
    {
        var store = new FakeStore(Scenario("S1")) { InsertResult = false };

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.AlreadyExisted, Is.EqualTo(1));
            Assert.That(summary.Results.Single().Outcome, Is.EqualTo(BaselineSeedOutcome.AlreadyExists));
        });
    }

    [Test]
    public async Task Seed_should_report_a_failed_insert()
    {
        var store = new FakeStore(Scenario("S1")) { InsertFailure = new InvalidOperationException("insert failed") };

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Results.Single().Stage, Is.EqualTo("BaselineInsert"));
            Assert.That(summary.Results.Single().Error, Does.Contain("insert failed"));
        });
    }

    [Test]
    public async Task Seed_should_stop_and_report_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new FakeStore(Scenario("S1"), Scenario("S2"));

        var summary = await CreateSeeder(store, _ => Ok("<Response />")).RunAsync(null, cancellationToken: cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Cancelled, Is.True);
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.NotRun, Is.EqualTo(2));
            Assert.That(store.Inserted, Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(17)]
    public void Seed_should_reject_an_out_of_range_parallelism(int maxParallel)
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateSeeder(new FakeStore(), _ => Ok("<Response />")).RunAsync(null, maxParallel));
    }

    [Test]
    public void Seed_should_reject_a_badly_formatted_request_time_before_reading_scenarios()
    {
        var store = new FakeStore(Scenario("S1"));

        Assert.ThrowsAsync<ArgumentException>(() => CreateSeeder(store, _ => Ok("<Response />")).RunAsync("2024-03-01 09:00"));
        Assert.That(store.Reads, Is.Zero);
    }

    private static BaselineSeeder CreateSeeder(
        FakeStore store,
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        RadarSettings? settings = null)
    {
        var apiClient = new RadarApiClient(
            new NoWaitRateLimiter(),
            new HttpClient(new StubHandler(respond)),
            NullLogger<RadarApiClient>.Instance,
            new RetrySettings { ApiRetryCount = 0, ApiRetryDelaySeconds = 0, ApiRetryAfterMaxDelaySeconds = 0 },
            (_, _) => Task.CompletedTask);
        var service = new RadarPricingService(settings ?? CreateSettings(), apiClient, new RadarUrlBuilder(), new PricingComparisonService([]));
        return new BaselineSeeder(store, service);
    }

    private static RadarSettings CreateSettings() => new()
    {
        Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
        {
            ["Endpoint1"] = new() { BaseUrl = "https://radar.example.test/quote", ApiKeyHeaderName = "X-API-KEY", ApiKeyValue = ApiKey }
        },
        Routes = new Dictionary<string, RadarRouteSettings>(StringComparer.Ordinal)
        {
            ["Route001"] = new() { ProductCode = "HOME", SchemeCodes = ["ABC", "XYS"], EndpointName = "Endpoint1", RouteKey = "shared-home" }
        }
    };

    private static ScenarioRequest Scenario(string id, string scheme = "ABC") => new()
    {
        ScenarioId = id,
        QuoteRef = $"Q-{id}",
        ProductCode = "HOME",
        SchemeCode = scheme,
        XmlRequest = $"<Request><Id>{id}</Id></Request>"
    };

    private static HttpResponseMessage Ok(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml, global::System.Text.Encoding.UTF8, "application/xml")
    };

    private sealed class FakeStore(params ScenarioRequest[] scenarios) : IBaselineSeedStore
    {
        public ConcurrentDictionary<string, ScenarioResponse> Inserted { get; } = new();
        public bool InsertResult { get; init; } = true;
        public Exception? InsertFailure { get; init; }
        public int Reads { get; private set; }

        public Task<IReadOnlyList<ScenarioRequest>> GetScenariosWithoutBaselineAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<ScenarioRequest>>(scenarios);
        }

        public Task<bool> TryInsertBaselineAsync(ScenarioResponse baseline, CancellationToken cancellationToken = default)
        {
            if (InsertFailure is not null)
            {
                throw InsertFailure;
            }

            if (InsertResult)
            {
                Inserted[baseline.ScenarioId] = baseline;
            }

            return Task.FromResult(InsertResult);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class NoWaitRateLimiter : IRadarRequestRateLimiter
    {
        public Task<HttpResponseMessage> SendAsync(string endpointName, Func<Task<HttpResponseMessage>> sendAsync, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return sendAsync();
        }
    }
}
```

### `PricingValidationFramework.Tests\System\SeedCommandTests.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.DataLoader;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class SeedCommandTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"seed-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void Settings_should_use_the_shared_radar_mapping_and_ignore_blank_placeholder_endpoints()
    {
        var settings = Load(Valid());

        Assert.Multiple(() =>
        {
            Assert.That(settings.MappingFile, Is.EqualTo(Path.Combine(TestContext.CurrentContext.TestDirectory, "Radar", "mappings.json")));
            Assert.That(settings.Radar.Routes, Has.Count.EqualTo(16), "routes come from config/radar/mappings.json");
            Assert.That(settings.Radar.Endpoints.Keys, Is.EqualTo(new[] { "Endpoint1" }), "blank Endpoint2 and Endpoint3 are dropped");
            Assert.That(settings.Database.AuditUser, Is.EqualTo("testuser"));
            Assert.That(settings.RateLimit.RequestsPerSecond, Is.EqualTo(2), "defaults apply when the section is absent");
            Assert.That(settings.RequestTime, Is.Null, "no time given means now");
        });
    }

    [Test]
    public void Settings_should_reject_a_partly_filled_endpoint()
    {
        var values = Valid();
        values["RadarSettings:Endpoints:Endpoint2:BaseUrl"] = "https://radar2.example.test";

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("Endpoint2"));
    }

    [Test]
    public void Settings_should_require_a_connection_string()
    {
        var values = Valid();
        values.Remove("DatabaseSettings:ConnectionString");

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("ConnectionString"));
    }

    [Test]
    public void Settings_should_require_at_least_one_endpoint()
    {
        var values = Valid();
        values.Remove("RadarSettings:Endpoints:Endpoint1:BaseUrl");
        values.Remove("RadarSettings:Endpoints:Endpoint1:ApiKeyHeaderName");
        values.Remove("RadarSettings:Endpoints:Endpoint1:ApiKeyValue");

        Assert.That(() => Load(values), Throws.InvalidOperationException.With.Message.Contains("At least one Radar logical endpoint"));
    }

    [TestCase("2024-05-01Z10:00:00", "2024-04-01Z08:00:00", "2024-03-01Z07:00:00", "2024-05-01Z10:00:00")]
    [TestCase(null, "2024-04-01Z08:00:00", "2024-03-01Z07:00:00", "2024-04-01Z08:00:00")]
    [TestCase(null, null, "2024-03-01Z07:00:00", "2024-03-01Z07:00:00")]
    public void Request_time_should_prefer_the_option_then_the_radar_variable_then_the_settings_file(
        string? option, string? radarVariable, string? setting, string expected)
    {
        var values = Valid();
        values["RADAR_REQUEST_DATETIME"] = radarVariable;
        values["PipelineSettings:RequestTime"] = setting;

        Assert.That(Load(values, option).RequestTime, Is.EqualTo(expected));
    }

    [TestCase("now", "2024-04-01Z08:00:00")]
    [TestCase(" NOW ", null)]
    public void Now_should_mean_the_current_hour_even_when_a_later_source_has_a_time(string option, string? radarVariable)
    {
        var values = Valid();
        values["RADAR_REQUEST_DATETIME"] = radarVariable;

        Assert.That(Load(values, option).RequestTime, Is.Null);
    }

    [Test]
    public void Settings_should_reject_a_badly_formatted_request_time()
    {
        Assert.That(() => Load(Valid(), "01/05/2024 10:00"), Throws.ArgumentException);
    }

    [Test]
    public void Settings_file_values_should_be_overridden_by_environment_style_keys()
    {
        var file = Path.Combine(directory, "local-settings.json");
        File.WriteAllText(file, """
            { "DatabaseSettings": { "ConnectionString": "Server=from-file;", "AuditUser": "file-user" },
              "RadarSettings": { "Endpoints": { "Endpoint1": { "BaseUrl": "https://file.example.test", "ApiKeyHeaderName": "X-API-KEY", "ApiKeyValue": "k" } } } }
            """);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(file)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseSettings:AuditUser"] = "env-user" })
            .Build();

        var settings = SeedCommand.LoadSettings(configuration, null, TestContext.CurrentContext.TestDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Database.ConnectionString, Is.EqualTo("Server=from-file;"));
            Assert.That(settings.Database.AuditUser, Is.EqualTo("env-user"));
        });
    }

    [Test]
    public async Task Seed_should_exit_1_and_write_a_summary_when_an_option_is_invalid()
    {
        var report = Path.Combine(directory, "seed-summary.json");

        var exitCode = await SeedCommand.RunAsync(["--report", report, "--max-parallel", "four"], CancellationToken.None);

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("Mode").GetString(), Is.EqualTo("seed"));
            Assert.That(json.RootElement.GetProperty("Succeeded").GetBoolean(), Is.False);
            Assert.That(json.RootElement.GetProperty("Issues")[0].GetString(), Does.Contain("FormatException"));
        });
    }

    [Test]
    public async Task Seed_should_exit_1_before_touching_the_database_when_the_settings_file_is_missing()
    {
        var report = Path.Combine(directory, "seed-summary.json");

        var exitCode = await SeedCommand.RunAsync(
            ["--settings", Path.Combine(directory, "missing.json"), "--report", report], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(File.ReadAllText(report), Does.Contain("FileNotFoundException"));
        });
    }

    private static SeedSettings Load(Dictionary<string, string?> values, string? requestTime = null) =>
        SeedCommand.LoadSettings(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            requestTime,
            TestContext.CurrentContext.TestDirectory);

    private static Dictionary<string, string?> Valid() => new()
    {
        ["DatabaseSettings:ConnectionString"] = "Server=localhost;Database=Test;",
        ["DatabaseSettings:AuditUser"] = "testuser",
        ["RadarSettings:Endpoints:Endpoint1:BaseUrl"] = "https://radar.example.test",
        ["RadarSettings:Endpoints:Endpoint1:ApiKeyHeaderName"] = "X-API-KEY",
        ["RadarSettings:Endpoints:Endpoint1:ApiKeyValue"] = "test-key",
        ["RadarSettings:Endpoints:Endpoint2:BaseUrl"] = "",
        ["RadarSettings:Endpoints:Endpoint2:ApiKeyHeaderName"] = "",
        ["RadarSettings:Endpoints:Endpoint2:ApiKeyValue"] = "",
        ["RadarSettings:Endpoints:Endpoint3:BaseUrl"] = ""
    };
}
```

### `data\import\examples\local-settings.example.json`

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=example-servername.net;Database=DatabaseName;Authentication=Active Directory Default;Encrypt=True;",
    "AuditUser": ""
  },
  "RadarSettings": {
    "Endpoints": {
      "Endpoint1": {
        "BaseUrl": "https://radar-endpoint-1.example.test/quote",
        "ApiKeyHeaderName": "X-API-KEY",
        "ApiKeyValue": "REPLACE_WITH_ENDPOINT1_KEY"
      },
      "Endpoint2": {
        "BaseUrl": "",
        "ApiKeyHeaderName": "",
        "ApiKeyValue": ""
      },
      "Endpoint3": {
        "BaseUrl": "",
        "ApiKeyHeaderName": "",
        "ApiKeyValue": ""
      }
    }
  },
  "PipelineSettings": {
    "RequestTime": "now",
    "MinThreshold": "0",
    "MaxThreshold": "0",
    "TestTag": "",
    "ScenarioTag": "",
    "IceRadarReport": ""
  }
}
```

## 3. Replace whole files

### `PricingValidationFramework.Core\Database\BaselineDataReader.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class BaselineDataReader : IBaselineDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	private const string PassingBaselinesSelect = """
		WITH PassingBaselines AS
		(
			SELECT
				response.Scenario_id AS ScenarioId,
				request.Quote_ref AS QuoteRef,
				request.Product_code AS ProductCode,
				request.Scheme_code AS SchemeCode,
				response.XML_Response AS XmlResponse,
				response.Status AS Status,
				response.Last_updated AS LastUpdated,
				ROW_NUMBER() OVER
				(
					PARTITION BY request.Product_code, request.Scheme_code
					ORDER BY response.Last_updated DESC, response.Scenario_id DESC
				) AS RowNumber
			FROM [Xml].[Response] AS response
			INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id
			WHERE response.Status = 'PASS'
		""";
	private const string PassingBaselinesResult = """
		)
		SELECT ScenarioId, QuoteRef, ProductCode, SchemeCode, XmlResponse, Status, LastUpdated
		FROM PassingBaselines
		WHERE RowNumber = 1;
		""";
	private const string PassingBaselinesSql = PassingBaselinesSelect + "\n" + PassingBaselinesResult;

	// The same selection limited by Radar's optional filters (TEST_TAG product, SCENARIO_TAG tag), applied
	// before the ranking, so ICE checks the newest PASS baseline within the products/tags Radar ran.
	private const string FilteredPassingBaselinesSql =
		PassingBaselinesSelect + "\n\t\t\tAND (@ProductCode IS NULL OR request.Product_code = @ProductCode)" +
		"\n\t\t\tAND (@ScenarioTag IS NULL OR CHARINDEX(',' + @ScenarioTag + ',', ',' + REPLACE(REPLACE(ISNULL(request.Test_tags, ''), ';', ','), ' ', '') + ',') > 0)\n" + PassingBaselinesResult;

	// Exactly the given scenarios' PASS baselines, with no newest-per-product/scheme ranking: ICE
	// after Radar checks the scenarios that passed in that Radar run.
	private const string PassingBaselinesByIdSql = """
		SELECT
			response.Scenario_id AS ScenarioId,
			request.Quote_ref AS QuoteRef,
			request.Product_code AS ProductCode,
			request.Scheme_code AS SchemeCode,
			response.XML_Response AS XmlResponse,
			response.Status AS Status,
			response.Last_updated AS LastUpdated
		FROM [Xml].[Response] AS response
		INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id
		WHERE response.Status = 'PASS' AND response.Scenario_id IN @ScenarioIds;
		""";

	// IN @ScenarioIds becomes one parameter per ID; stay well under SQL Server's 2,100.
	internal const int MaxScenarioIdsPerQuery = 1000;

	// No Status filter: Radar reports "no baseline yet" (MissingBaseline: run DataLoader seed)
	// and "baseline exists but isn't PASS" as different errors.
	private const string BaselineByIdSql = """
		SELECT
			response.Scenario_id AS ScenarioId,
			request.Quote_ref AS QuoteRef,
			response.XML_Response AS XmlResponse,
			response.row_creationdate AS CreatedDate,
			response.Last_updated AS LastUpdated,
			response.Status AS Status
		FROM [Xml].[Response] AS response
		INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id
		WHERE response.Scenario_id = @ScenarioId;
		""";

	public BaselineDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
	{
		return QueryPassingBaselinesAsync(PassingBaselinesSql, null, cancellationToken);
	}

	/// Newest PASS baseline per product/scheme within the product and/or tag; a null or empty filter is not applied.
	public Task<IReadOnlyList<IceBaselineScenario>> GetFilteredPassingBaselineScenariosAsync(
		string? productCode,
		string? scenarioTag,
		CancellationToken cancellationToken = default)
	{
		return QueryPassingBaselinesAsync(FilteredPassingBaselinesSql, new
		{
			ProductCode = string.IsNullOrWhiteSpace(productCode) ? null : productCode,
			ScenarioTag = string.IsNullOrWhiteSpace(scenarioTag) ? null : scenarioTag.Trim()
		}, cancellationToken);
	}

	/// The PASS baselines of exactly these scenarios (any not PASS, or without a baseline, are left out).
	public async Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselinesByScenarioIdsAsync(
		IReadOnlyCollection<string> scenarioIds,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenarioIds);
		var baselines = new List<IceBaselineScenario>();
		foreach (var chunk in scenarioIds.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(MaxScenarioIdsPerQuery))
		{
			baselines.AddRange(await QueryPassingBaselinesAsync(PassingBaselinesByIdSql, new { ScenarioIds = chunk }, cancellationToken));
		}

		return baselines;
	}

	private async Task<IReadOnlyList<IceBaselineScenario>> QueryPassingBaselinesAsync(
		string sql,
		object? parameters,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				var scenarios = await connection.QueryAsync<IceBaselineScenario>(command);
				return scenarios.AsList();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	public async Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
		string scenarioId,
		CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(BaselineByIdSql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
				var baselines = (await connection.QueryAsync<ScenarioResponse>(command)).AsList();

				return baselines.Count switch
				{
					1 => baselines[0],
					0 => null,
					_ => throw new InvalidDataException($"Multiple baselines were found for ScenarioId '{scenarioId}'.")
				};
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}
```

### `PricingValidationFramework.Core\Database\IBaselineDataReader.cs`

Replace the entire contents with:

```csharp
using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    /// The same selection within Radar's filters: an exact Product_code (TEST_TAG) and/or one
    /// Test_tags tag (SCENARIO_TAG). A null or empty filter is not applied.
    Task<IReadOnlyList<IceBaselineScenario>> GetFilteredPassingBaselineScenariosAsync(
        string? productCode,
        string? scenarioTag,
        CancellationToken cancellationToken = default);

    /// The PASS baselines of exactly these scenarios, e.g. the ones that passed in a Radar run.
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselinesByScenarioIdsAsync(
        IReadOnlyCollection<string> scenarioIds,
        CancellationToken cancellationToken = default);

    /// The scenario's [Xml].[Response] row whatever its status, or null if it has no baseline yet.
    Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}
```

### `PricingValidationFramework.Core\Database\RequestDataReader.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class RequestDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	private const string ScenarioColumns = """
		Scenario_id AS ScenarioId,
		Quote_ref AS QuoteRef,
		Scheme_code AS SchemeCode,
		Product_code AS ProductCode,
		XML_request AS XmlRequest,
		Test_tags AS TestTags,
		row_creationdate AS CreatedDate
		""";
	private const string AllScenariosSql = $"SELECT {ScenarioColumns} FROM [Xml].[Request] ORDER BY row_creationdate, Scenario_id;";
	private const string ProductScenariosSql = $"SELECT {ScenarioColumns} FROM [Xml].[Request] WHERE Product_code = @ProductCode ORDER BY row_creationdate, Scenario_id;";
	// Radar's filters, both optional: an exact Product_code (TEST_TAG) and one tag from the comma- or
	// semicolon-separated Test_tags (SCENARIO_TAG). CHARINDEX, not LIKE, so "_" in a tag is literal.
	internal const string ScenarioTagMatch = "CHARINDEX(',' + @ScenarioTag + ',', ',' + REPLACE(REPLACE(ISNULL(Test_tags, ''), ';', ','), ' ', '') + ',') > 0";
	private const string FilteredScenariosSql = $"""
		SELECT {ScenarioColumns} FROM [Xml].[Request]
		WHERE (@ProductCode IS NULL OR Product_code = @ProductCode)
		  AND (@ScenarioTag IS NULL OR {ScenarioTagMatch})
		ORDER BY row_creationdate, Scenario_id;
		""";
	// Scenarios the seed command still has to baseline: no [Xml].[Response] row of any status.
	private const string ScenariosWithoutBaselineSql = $"""
		SELECT {ScenarioColumns} FROM [Xml].[Request] AS request
		WHERE NOT EXISTS (SELECT 1 FROM [Xml].[Response] AS response WHERE response.Scenario_id = request.Scenario_id)
		ORDER BY row_creationdate, Scenario_id;
		""";
	private const string ScenarioByIdSql = $"SELECT {ScenarioColumns} FROM [Xml].[Request] WHERE Scenario_id = @ScenarioId;";

	public RequestDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetAllScenariosAsync(CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(AllScenariosSql, null, cancellationToken);
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetScenariosByProductCodeAsync(string productCode, CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(ProductScenariosSql, new { ProductCode = productCode }, cancellationToken);
	}

	/// Scenarios matching the product and/or tag; a null or empty filter is not applied.
	public Task<IReadOnlyList<ScenarioRequest>> GetFilteredScenariosAsync(
		string? productCode,
		string? scenarioTag,
		CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(FilteredScenariosSql, new
		{
			ProductCode = string.IsNullOrWhiteSpace(productCode) ? null : productCode,
			ScenarioTag = string.IsNullOrWhiteSpace(scenarioTag) ? null : scenarioTag.Trim()
		}, cancellationToken);
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetScenariosWithoutBaselineAsync(CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(ScenariosWithoutBaselineSql, null, cancellationToken);
	}

	public async Task<ScenarioRequest?> GetScenarioByIdAsync(string scenarioId, CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(ScenarioByIdSql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
				return await connection.QuerySingleOrDefaultAsync<ScenarioRequest>(command);
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	private async Task<IReadOnlyList<ScenarioRequest>> QueryScenariosAsync(
		string sql,
		object? parameters,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				var scenarios = await connection.QueryAsync<ScenarioRequest>(command);
				return scenarios.AsList();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}
```

### `PricingValidationFramework.Core\Database\ResultUpdater.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class ResultUpdater
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	// PASS: the Radar response becomes the new baseline.
	private const string UpdatePassingResultSql = """
		UPDATE [Xml].[Response]
		SET Status = @Status,
		    Last_updated = SYSUTCDATETIME(),
		    XML_Response = @XmlResponse,
		    row_modifieddate = SYSUTCDATETIME(),
		    row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())
		WHERE Scenario_id = @ScenarioId;
		""";

	// FAIL: only the status (and audit columns) change, so the existing baseline XML is never
	// overwritten. Last_updated is left alone because ICE picks its baseline by it.
	private const string UpdateFailedStatusSql = """
		UPDATE [Xml].[Response]
		SET Status = @Status,
		    row_modifieddate = SYSUTCDATETIME(),
		    row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())
		WHERE Scenario_id = @ScenarioId;
		""";

	// New scenario: the first Radar response becomes its baseline. The NOT EXISTS guard (with
	// UPDLOCK/HOLDLOCK) means an existing baseline is never replaced, even by a concurrent run.
	private const string InsertBaselineSql = """
		INSERT INTO [Xml].[Response]
		    (Scenario_id, XML_Response, Status, Last_updated,
		     row_creationdate, row_createdby, row_modifieddate, row_modifiedby)
		SELECT @ScenarioId, @XmlResponse, @Status, SYSUTCDATETIME(),
		       SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME()), SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME())
		WHERE NOT EXISTS (
		    SELECT 1 FROM [Xml].[Response] WITH (UPDLOCK, HOLDLOCK)
		    WHERE Scenario_id = @ScenarioId);
		""";

	public ResultUpdater(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task UpdateResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		var affectedRows = string.Equals(response.Status, "PASS", StringComparison.Ordinal)
			? await ExecuteAsync(UpdatePassingResultSql, new
			{
				response.Status,
				response.XmlResponse,
				response.ScenarioId,
				connectionFactory.AuditUser
			}, cancellationToken)
			: await ExecuteAsync(UpdateFailedStatusSql, new
			{
				response.Status,
				response.ScenarioId,
				connectionFactory.AuditUser
			}, cancellationToken);
		EnsureSingleRowUpdated(response.ScenarioId, affectedRows);
	}

	public async Task InsertBaselineAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		if (!await TryInsertBaselineAsync(response, cancellationToken))
		{
			throw new InvalidOperationException(
				$"A baseline already exists in [Xml].[Response] for ScenarioId '{response.ScenarioId}'; it was not replaced.");
		}
	}

	/// False when a baseline row already exists (for example, written by a concurrent run); that
	/// row is left unchanged.
	public async Task<bool> TryInsertBaselineAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		var affectedRows = await ExecuteAsync(InsertBaselineSql, new
		{
			response.ScenarioId,
			response.XmlResponse,
			response.Status,
			connectionFactory.AuditUser
		}, cancellationToken);
		return affectedRows == 1;
	}

	private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				return await connection.ExecuteAsync(command);
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	private static void EnsureSingleRowUpdated(string scenarioId, int affectedRows)
	{
		if (affectedRows != 1)
		{
			throw new InvalidOperationException(
				$"Expected exactly one [Xml].[Response] row to be updated for ScenarioId '{scenarioId}', but {affectedRows} rows were updated.");
		}
	}
} 
```

### `PricingValidationFramework.Core\ExternalAPIAccess\UrlBuilders\RequestTimeFormatter.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;

using System.Globalization;

public static class RequestTimeFormatter
{
	private static string FormatUtcHour(DateTime value)
	{
		var startOfHour = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
		return startOfHour.ToString("yyyy-MM-dd'Z'HH:mm:ss", CultureInfo.InvariantCulture);
	}
	public static string FormatUtcNow()
	{
		return FormatUtcHour(DateTime.UtcNow);
	}

	public static string Resolve(string? suppliedValue)
	{
		// Empty or "now" (any case) means the current UTC hour.
		if (string.IsNullOrWhiteSpace(suppliedValue) ||
			string.Equals(suppliedValue.Trim(), "now", StringComparison.OrdinalIgnoreCase))
		{
			return FormatUtcNow();
		}

		var trimmedValue = suppliedValue.Trim();
		if (!DateTime.TryParseExact(
			trimmedValue,
			"yyyy-MM-dd'Z'HH:mm:ss",
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var parsed))
		{
			throw new ArgumentException("RequestTime must use yyyy-MM-ddZHH:mm:ss format.", nameof(suppliedValue));
		}

		return FormatUtcHour(parsed.ToUniversalTime());
	}
}
```

### `PricingValidationFramework.Core\Models\Common\PipelineSettings.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.Core.Models.Common;

public class PipelineSettings
{
    public string BuildId { get; set; } = string.Empty;
    /// Product filter (TEST_TAG): an exact [Xml].[Request].Product_code; empty selects every product.
    public string TestTag { get; set; } = string.Empty;

    /// Tag filter (SCENARIO_TAG): one tag from [Xml].[Request].Test_tags; empty selects every scenario.
    public string ScenarioTag { get; set; } = string.Empty;
    public string RequestTime { get; set; } = string.Empty;
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }
}
```

### `PricingValidationFramework.DataLoader\CsvScenarioReader.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

internal sealed record RequestCsvRow(string ScenarioId, string Xml, string TestTags);

internal static class CsvScenarioReader
{
    public static async IAsyncEnumerable<RequestCsvRow> ReadRequestsAsync(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in ReadAsync<RequestRow>(path, cancellationToken))
        {
            yield return new RequestCsvRow(row.ScenarioId, row.Xml, row.TestTags ?? string.Empty);
        }
    }

    private static async IAsyncEnumerable<TRecord> ReadAsync<TRecord>(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            ExceptionMessagesContainRawData = false
        });
        await foreach (var row in csv.GetRecordsAsync<TRecord>(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    // Same shape as the scenarios.csv the CSV loader UI produces: scenario_id + request XML, plus an
    // optional test_tags column (e.g. "smoke,regression"). Quote_ref, Product_code and Scheme_code are
    // read from the XML by ScenarioDataLoader.
    private sealed class RequestRow
    {
        [Name("scenario_id")] public string ScenarioId { get; set; } = string.Empty;
        [Name("xml")] public string Xml { get; set; } = string.Empty;
        [Name("test_tags"), Optional] public string? TestTags { get; set; }
    }
}
```

### `PricingValidationFramework.DataLoader\PricingValidationFramework.DataLoader.csproj`

Replace the entire contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CsvHelper" Version="33.1.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
    <ProjectReference Include="..\PricingValidationFramework.Core\PricingValidationFramework.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- Shared Radar routing: config/radar/mappings.json and its XPathProfiles, copied to Radar/ in the output. -->
    <None Include="..\config\radar\**\*.json" LinkBase="Radar" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

### `PricingValidationFramework.DataLoader\Program.cs`

Replace the entire contents with:

```csharp
using System.Globalization;
using System.Text.Json;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.DataLoader;

const string Usage = """
    Usage:
      DataLoader <validate|import> --requests <csv-or-folder> [--batch-size <1-1000>] [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>]
      DataLoader seed [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>] [--max-parallel <1-16>]
    """;
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 2 : 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
if (args[0] == "seed")
{
    // Creates the first [Xml].[Response] baseline for every scenario that has none, via Radar.
    return await SeedCommand.RunAsync(args[1..], cancellation.Token);
}

var reportPath = Path.Combine("TestResults", "DataLoader", "import-summary.json");
ImportSummary summary;
try
{
    if (args[0] is not ("validate" or "import") || (args.Length - 1) % 2 != 0)
    {
        throw new ArgumentException("A valid command and option/value pairs are required.");
    }
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < args.Length; index += 2)
    {
        if (args[index] is not ("--requests" or "--batch-size" or "--settings" or "--report" or "--request-time") ||
            !values.TryAdd(args[index], args[index + 1]))
        {
            throw new ArgumentException("An unknown or duplicate command option was supplied.");
        }
    }
    reportPath = values.GetValueOrDefault("--report", reportPath);
    var batchSize = int.Parse(values.GetValueOrDefault("--batch-size", "100"), CultureInfo.InvariantCulture);
    IScenarioImportRepository? repository = null;
    if (args[0] == "import")
    {
        var connectionString = Environment.GetEnvironmentVariable("DatabaseSettings__ConnectionString");
        var auditUser = Environment.GetEnvironmentVariable("DatabaseSettings__AuditUser");
        if ((connectionString is null || auditUser is null) && values.TryGetValue("--settings", out var settingsPath))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath, cancellation.Token));
            var databaseSettings = settings.RootElement.GetProperty("DatabaseSettings");
            connectionString ??= databaseSettings.GetProperty("ConnectionString").GetString();
            // Optional: who to record in row_createdby / row_modifiedby (the SQL login when absent).
            auditUser ??= databaseSettings.TryGetProperty("AuditUser", out var auditUserSetting) ? auditUserSetting.GetString() : null;
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Import requires a database connection string.");
        }
        repository = new ScenarioImportRepository(new SqlConnectionFactory(new DatabaseSettings { ConnectionString = connectionString, AuditUser = auditUser ?? string.Empty }));
    }
    summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(
        values.GetValueOrDefault("--requests") ?? throw new ArgumentException("--requests is required."),
        args[0] == "validate", batchSize, values.GetValueOrDefault("--request-time")), cancellation.Token);
}
catch (Exception exception)
{
    summary = new ImportSummary { Mode = args[0], Cancelled = exception is OperationCanceledException };
    summary.Issues.Add(new ImportIssue("configuration", 0, string.Empty,
        $"The command could not start ({exception.GetType().Name}). Check arguments, file paths, and database configuration."));
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{summary.Mode}: file={summary.RequestFile}, requestTime={summary.RequestTime}, success={summary.Succeeded}, requests={summary.InputRequests}, " +
    $"insertedRequests={summary.InsertedRequests}, updatedRequests={summary.UpdatedRequests}, skippedRequests={summary.SkippedRequests}, issues={summary.Issues.Count}.");
return summary.Cancelled ? 130 : summary.Succeeded ? 0 : 1;
```

### `PricingValidationFramework.DataLoader\README.md`

Replace the entire contents with:

````markdown
# Scenario Data Importer

The DataLoader has three commands:

- `validate` checks a requests CSV without a database.
- `import` loads `[Xml].[Request]` only, keyed by unique `Scenario_id`. It never writes `[Xml].[Response]`.
- `seed` creates the first baseline for every scenario that has no `[Xml].[Response]` row: it calls Radar and stores the response with `Status = PASS`, without the pricing comparison. See [Seed baselines](#seed-baselines).

Radar compares only. A scenario with no baseline is a Radar ERROR (`MissingBaseline`), so run `seed` after an import and before Radar.

## Ownership

- `CsvScenarioReader`: RFC4180 CSV parsing, including quoted XML and multiline values.
- `ScenarioDataLoader`: required-field/XML validation, duplicate detection, existing-record comparison, import planning, batching, cancellation, and summaries.
- `ImportSpool`: temporary normalized payload storage; memory retains IDs/offsets and a working batch, not all XML records.
- `SeedCommand`: the `seed` command's options and settings; Core `BaselineSeeder` finds scenarios without a baseline, calls Radar through `RadarPricingService.CreateBaselineAsync`, and inserts through `ResultUpdater.TryInsertBaselineAsync`.
- Core `IScenarioImportRepository` and `ScenarioImportRepository`: database lookups and transactional, parameterised inserts and updates. They reuse `SqlConnectionFactory.OpenAsync` and do not parse CSVs or decide whether a baseline is approved.

## Input Files

Local datasets belong under `PricingValidationFramework/data/import/`. Real CSVs there are ignored by Git; only synthetic files under `examples/` are tracked. Do not store customer XML in C# source folders or commit real datasets.

Request headers (the same shape as the `scenarios.csv` the CSV loader UI produces), plus an optional `test_tags`:

```csv
scenario_id,xml[,test_tags]
```

`Quote_ref`, `Product_code` and `Scheme_code` are read from each request's XML:

| `[Xml].[Request]` column | XML element |
|---|---|
| `Quote_ref` | `/Message/Policy/PolicyReference` |
| `Product_code` | `/Message/Policy/ProductCode` |
| `Scheme_code` | `/Message/Policy/SchemeCode` |

Each element must appear exactly once and be non-empty; otherwise the row is reported and nothing is imported.

An optional third column, `test_tags`, gives each scenario's tags, separated by commas or semicolons, e.g. `smoke,regression`. Tags may use letters, digits, `_`, `-` and `.` (at most 64 characters each); duplicates are removed and they are stored comma-separated in `Test_tags`. An invalid tag is reported and nothing is imported. Tags are set only when a scenario is first inserted: like every column except the XML, they are not compared or changed for an existing scenario, so change an existing scenario's tags in the database. Without the column, `Test_tags` is stored empty. Radar and ICE can then select by one tag with `SCENARIO_TAG`.

`--requests` takes a CSV file or a folder. A file is loaded as given. For a folder, the loader picks the newest `scenarios-{yyyyMMdd-HHmmss-fff}.csv` (the versioned files the CSV loader UI saves) by the timestamp in its **filename**, not the file's modified time, which changes when files are copied or checked out. Other CSVs in the folder, including an unversioned `scenarios.csv`, are ignored. If the folder has no versioned file, it falls back to `requests.csv`; if there's neither, the run fails with an issue. The chosen file is printed and recorded as `RequestFile` in the JSON summary.

Headers are case-insensitive and both are required. XML declarations are removed and DTDs are prohibited. This importer checks well-formed XML and the three identifier elements, not pricing correctness or XSD conformance. Offline validation cannot check for conflicts with existing rows; `import` checks them before any writes. The synthetic examples demonstrate import mechanics, not a production Radar request/response contract.

## Run Locally

From the repository root:

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- validate --requests PricingValidationFramework/data/import/examples/requests.csv --report TestResults/DataLoader/validation-summary.json
```

For database import, configure `DatabaseSettings__ConnectionString` directly in your environment or a secure configuration file containing the existing `DatabaseSettings.ConnectionString` section. Environment configuration takes precedence over `--settings`. Never pass a connection string as a command-line argument or commit it.

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- import --requests PricingValidationFramework/data/import/requests.csv --batch-size 100 --settings PricingValidationFramework/PricingValidationFramework.Tests/appsettings.json --report TestResults/DataLoader/import-summary.json
```

Exit codes: 0 success, 1 validation/import failure, 2 usage without a command, 130 cancellation. The JSON summary includes inserted, updated and skipped request counts, committed batches, rejected IDs/reasons, and cancellation state. It contains no XML or connection strings.

## Seed Baselines

`seed` finds every `[Xml].[Request]` scenario with no `[Xml].[Response]` row (of any status), calls Radar for each and inserts the response as its `PASS` baseline. It doesn't compare anything, and it never reads or changes a scenario that already has a baseline, so it is safe to rerun: a second run finds nothing to do.

```text
DataLoader seed [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>] [--max-parallel <1-16>]
```

Settings are read from the `--settings` JSON (optional), with environment variables on top. They use the same names as the Radar tests and pipeline:

| Setting | JSON | Environment variable |
|---|---|---|
| Database | `DatabaseSettings.ConnectionString`, `DatabaseSettings.AuditUser` | `DatabaseSettings__ConnectionString`, `DatabaseSettings__AuditUser` |
| Radar endpoints | `RadarSettings.Endpoints.Endpoint1.BaseUrl` / `.ApiKeyHeaderName` / `.ApiKeyValue` (and `Endpoint2`, `Endpoint3`) | `RadarSettings__Endpoints__Endpoint1__BaseUrl` ... |
| Request time | `PipelineSettings.RequestTime` | `RADAR_REQUEST_DATETIME` (`--request-time` wins over both) |
| Routing | `PricingProfiles.MappingFile` (default `Radar/mappings.json`) | `PricingProfiles__MappingFile` |
| Rate limit / retries | `RadarRateLimitSettings`, `RetrySettings` (defaults: 2 requests per second, 3 API retries) | `RadarRateLimitSettings__RequestsPerSecond` ... |

The request time is UTC `yyyy-MM-ddZHH:mm:ss`, rounded down to the hour, or `now` (the default) for the current hour. It's resolved once, so every scenario in a run uses the same time.

Only the endpoints your scenarios use need to be set. An endpoint whose URL, header and key are all empty is ignored, so a local settings file can fill in `Endpoint1` and leave the others blank. A partly filled endpoint is rejected. A scenario whose route names an endpoint that isn't configured fails with `EndpointResolution`; one whose product and scheme match no route in `mappings.json` fails with `RouteResolution`. Neither calls Radar.

The summary (default `TestResults/DataLoader/seed-summary.json`) lists `RequestTime`, `ScenariosWithoutBaseline`, `Seeded`, `AlreadyExisted` (a baseline written by another run in the meantime; left unchanged), `Failed`, `NotRun` (after cancellation), any start-up `Issues`, and one result per scenario with its route, outcome, failure stage and error. It contains no XML, connection strings or keys. Exit codes: 0 every scenario seeded (or none needed it), 1 any failure or a configuration problem, 130 cancelled.

The database identity needs SELECT on `[Xml].[Request]` and `[Xml].[Response]` and INSERT on `[Xml].[Response]`. Nothing is created in the database.

### Run it locally

1. Copy `data/import/examples/local-settings.example.json` to `data/import/local-settings.json` (ignored by Git) and fill in the connection string and at least the endpoint your scenarios use. Keep keys out of tracked files.
2. Check `config/radar/mappings.json` has a route for your scenarios' `Product_code` and `Scheme_code`.
3. From the `PricingValidationFramework` folder:

```bash
dotnet run --project PricingValidationFramework.DataLoader -- seed --settings data/import/local-settings.json --max-parallel 1
```

To debug in VS Code, open the `PricingValidationFramework` folder and choose **3. DataLoader: seed baselines into [Xml].[Response]** in Run and Debug. The `.vscode` folder also has **1. validate** and **2. import** configurations using the same settings file, so a full local run is 1, 2, 3 and then the Radar tests, which read the same `local-settings.json` (its `PipelineSettings` section holds Radar's thresholds and filters). To debug Radar or ICE, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process. Breakpoints that are useful: `BaselineSeeder.RunAsync` (the scenarios found), `RadarPricingService.CreateBaselineAsync` (route and URL), and `ResultUpdater.TryInsertBaselineAsync` (the insert).

## Database Behavior

The target schema must already exist. Enforce a unique/primary key on `[Xml].[Request].Scenario_id`. `XML_request` is `VARCHAR(MAX)` and its parameter is explicitly bound as ANSI MAX-length text. Ensure the database code page or UTF-8 collation supports the XML's characters. The importer does not create tables or change database constraints. Table names use the connection user's existing schema resolution, like the Core readers.

Every input record is validated and spooled before database access. Existing records are checked for the complete dataset before writing. New IDs are inserted. An existing ID is compared on its XML only, after normalising: identical XML is skipped, any other difference updates `XML_request` and nothing else. `Quote_ref`, `Product_code`, `Scheme_code`, `Test_tags` and the creation date are neither compared nor changed, so if the new XML has a different product or scheme, those columns keep their old values and Radar still routes on them; use a new scenario ID when a scenario really changes product or scheme. Stored XML that can't be parsed counts as different and is replaced. `import` never changes `[Xml].[Response]` or calls Radar.

Each batch uses one connection and serializable transaction, with `SET XACT_ABORT ON`. Inserts and updates use separate parameterised `VALUES` statements of up to 333 rows: six parameters per row keeps each statement under SQL Server's 2,100-parameter limit. Nothing is created in the database: no temporary tables, table types, stored procedures or bulk copy, so the loader needs `SELECT`, `INSERT` and `UPDATE` on `[Xml].[Request]`. An update target that no longer exists, or an insert ID that appeared since preflight, rolls the batch back. Counts are returned only after commit. Inserts set `row_creationdate`, `row_createdby`, `row_modifieddate` and `row_modifiedby`; updates set `row_modifieddate` and `row_modifiedby` and preserve the creation columns and tags. Dates are UTC (`SYSUTCDATETIME()`). The user is `DatabaseSettings__AuditUser` (or `DatabaseSettings.AuditUser` in the `--settings` file) when set, otherwise the SQL login (`SUSER_SNAME()`).

Default batch size is 100 for large XML payloads; maximum is 1000, with each batch split into SQL statements of at most 333 rows. Transactions are batch-scoped, not dataset-scoped: if a later batch fails, earlier commits remain and are counted in the summary. Check database state and rerun the same immutable dataset to skip committed inserts and updates. A lost connection during commit can make the last batch's outcome uncertain; never assume the entire run rolled back. No blind write retries are performed.

Allow temporary disk space for a normalized copy of the dataset, plus the input files. The spool is removed on disposal. Import timing depends on payload size, SQL indexes, and network latency; the 20,000-scenario test uses a fake repository and does not benchmark live SQL.

## Azure DevOps

Register `PricingValidationFramework/azure-pipelines-data-loader.yml` as a separate manual pipeline.

1. Configure an agent pool with .NET installation support and access to the target SQL Server. The YAML defaults to the self-hosted `Default` pool; set `agentPool` for your environment.
2. The Import stage targets the `pricing-data-import` environment (or another chosen with `importEnvironment`). Azure DevOps creates it on the first run if it doesn't exist. **Import is auto-approved: it runs as soon as Validate succeeds.** Do not add an Approvals check to the environment, as that would make every run wait for sign-off. Add an Exclusive lock check so two imports can't write at the same time; checks are configured in Azure DevOps, not in this YAML. Restrict environment and pipeline permissions to authorized operators.
3. Add secret variable `DataLoaderDatabaseConnectionString` for that environment. Use a dedicated database identity with SELECT, INSERT and UPDATE permission on `[Xml].[Request]`. It needs no permission to create anything, including temporary tables.
4. Choose `repository` for approved synthetic/test files, or `artifact` for a controlled dataset. Artifact mode requires a numeric pipeline definition ID and run ID and an artifact named `scenario-data` with `scenarios-{timestamp}.csv` file(s) or a `requests.csv` at its root; the newest versioned file is used. Do not select an unversioned latest artifact.
5. The first stage restores/builds/tests the importer, validates CSV/XML without SQL, and pins those exact files as `scenario-import-data`. It also resolves the `requestDateTime` parameter (default `now`) with Core's `RequestTimeFormatter`, the same rule Radar uses: UTC, `yyyy-MM-ddZHH:mm:ss`, rounded down to the hour, with `now` meaning the current hour at validation. An invalid value fails this stage. The resolved time is shown in the log and in `validation-summary.json` (`RequestTime`). Import starts automatically once this stage succeeds; a failed validation stops the run before any database write. Review `DataLoaderValidation` for the details of any rejected rows.
6. The second stage downloads the current run's pinned dataset, inserts new scenarios and updates changed XML through the Core repository. Review `DataLoaderImport` for counts and errors.
7. When `seedBaselines` is true (the default), a third stage, **Seed baselines**, runs `DataLoader seed --request-time <time resolved in step 5>`. Every scenario without an `[Xml].[Response]` row gets its first Radar response stored as its PASS baseline; scenarios that already have one are not read or changed. `seedMaxParallel` (default 4) sets how many scenarios are seeded at once; the Radar rate limit still applies. The summary is published as `SeedSummary`. The stage fails if any scenario could not be seeded. It needs `RadarDatabaseConnectionString` (with SELECT on both tables and INSERT on `[Xml].[Response]`), the three endpoint URL/header/key variable sets, all defined (an unused endpoint's three may be empty, and is then ignored), and an agent that can reach the Radar endpoints. It doesn't need thresholds. Set `seedBaselines` to false to load only, and run `seed` later.
8. To seed without importing, tick `seedOnly`. Validate and Import are skipped, and only **Seed baselines** runs, for the scenarios already in `[Xml].[Request]` that have no baseline. It uses `requestDateTime` directly (`now` means the current UTC hour when the stage runs) and needs no dataset parameters. `seedOnly` overrides `seedBaselines`.

Dataset artifacts contain XML and may contain sensitive data. Restrict artifact access and retention appropriately. Do not publish the dataset publicly. Environment exclusive locking prevents overlapping import runs only when its check is actually configured; other tools writing these tables still need database constraints.
````

### `PricingValidationFramework.DataLoader\ScenarioDataLoader.cs`

Replace the entire contents with:

```csharp
namespace PricingValidationFramework.DataLoader;

using System.Xml;
using System.Xml.Linq;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Validation;

public sealed record ImportOptions(
    string RequestCsv,
    bool ValidateOnly,
    int BatchSize = 100,
    string? RequestTime = null);

public sealed record ImportIssue(string Source, int RecordNumber, string ScenarioId, string Reason);

public sealed class ImportSummary
{
    public string Mode { get; init; } = string.Empty;
    public string RequestFile { get; set; } = string.Empty;

    /// The Radar request time for the baselines created after this load, resolved the same way
    /// Radar does (UTC, start of the hour). Empty when no --request-time was given.
    public string RequestTime { get; set; } = string.Empty;
    public int InputRequests { get; set; }
    public int InsertedRequests { get; set; }
    public int UpdatedRequests { get; set; }
    public int SkippedRequests { get; set; }
    public int CommittedBatches { get; set; }
    public bool Cancelled { get; set; }
    public List<ImportIssue> Issues { get; } = [];
    public bool Succeeded => !Cancelled && Issues.Count == 0;
}

/// Loads [Xml].[Request] only. Baselines in [Xml].[Response] are created by the Radar run: a scenario
/// with no [Xml].[Response] row gets Radar's first response stored as its PASS baseline.
public sealed class ScenarioDataLoader(IScenarioImportRepository? repository = null)
{
    public async Task<ImportSummary> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BatchSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Batch size must be between 1 and 1000.");
        }
        if (string.IsNullOrWhiteSpace(options.RequestCsv))
        {
            throw new ArgumentException("A request CSV is required.", nameof(options));
        }

        var summary = new ImportSummary { Mode = options.ValidateOnly ? "validate" : "import" };
        if (options.RequestTime is not null)
        {
            try
            {
                // "now" (any case) means the current UTC hour, exactly as an empty value does for Radar.
                summary.RequestTime = RequestTimeFormatter.Resolve(
                    string.Equals(options.RequestTime.Trim(), "now", StringComparison.OrdinalIgnoreCase) ? null : options.RequestTime);
            }
            catch (ArgumentException)
            {
                summary.Issues.Add(new ImportIssue("input", 0, string.Empty,
                    $"Request time '{options.RequestTime}' must be 'now' or use the yyyy-MM-ddZHH:mm:ss format, e.g. 2026-10-12Z09:00:00."));
                return summary;
            }
        }

        try
        {
            summary.RequestFile = RequestFileResolver.Resolve(options.RequestCsv);
        }
        catch (InvalidDataException exception)
        {
            summary.Issues.Add(new ImportIssue("input", 0, string.Empty, exception.Message));
            return summary;
        }

        using var spool = new ImportSpool();
        var operation = "Reading input files";
        try
        {
            await LoadRequestsAsync(summary.RequestFile, spool, summary, cancellationToken);
            if (summary.InputRequests == 0)
            {
                summary.Issues.Add(new ImportIssue("input", 0, string.Empty, "The input contains no scenario records."));
            }
            if (!summary.Succeeded || options.ValidateOnly)
            {
                return summary;
            }
            if (repository is null)
            {
                throw new InvalidOperationException("Import requires a database repository.");
            }

            var requestInserts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requestUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            operation = "Checking existing database records";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var existing = await repository.ReadExistingAsync(batch.Select(row => row.ScenarioId).ToArray(), cancellationToken);
                PlanBatch(batch, existing, summary, requestInserts, requestUpdates);
            }
            if (!summary.Succeeded)
            {
                return summary;
            }

            operation = "Writing a database batch";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var inserts = batch.Where(row => requestInserts.Contains(row.ScenarioId)).ToArray();
                var updates = batch.Where(row => requestUpdates.Contains(row.ScenarioId)).ToArray();
                if (inserts.Length + updates.Length == 0)
                {
                    continue;
                }
                var result = await repository.ApplyBatchAsync(inserts, updates, cancellationToken);
                summary.InsertedRequests += result.InsertedRequests;
                summary.UpdatedRequests += result.UpdatedRequests;
                summary.CommittedBatches++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            summary.Cancelled = true;
        }
        catch (Exception exception)
        {
            summary.Issues.Add(new ImportIssue("operation", 0, string.Empty,
                $"{operation} failed ({exception.GetType().Name}). Check database state before retrying; earlier batches may already be committed."));
        }
        return summary;
    }

    private static async Task LoadRequestsAsync(string requestFile, ImportSpool spool, ImportSummary summary, CancellationToken cancellationToken)
    {
        await foreach (var row in CsvScenarioReader.ReadRequestsAsync(requestFile, cancellationToken))
        {
            summary.InputRequests++;
            try
            {
                var normalized = RequestFromCsv(row);
                if (!spool.AddRequest(normalized))
                {
                    throw new InvalidDataException("Duplicate request Scenario_id.");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or XmlException)
            {
                summary.Issues.Add(new ImportIssue("requests", summary.InputRequests, row.ScenarioId,
                    exception is XmlException ? "Request XML is malformed or contains a prohibited DTD." : exception.Message));
            }
        }
    }

    private static void PlanBatch(
        IReadOnlyList<ScenarioRequestImport> batch,
        ScenarioImportSnapshot existing,
        ImportSummary summary,
        HashSet<string> requestInserts,
        HashSet<string> requestUpdates)
    {
        var requests = existing.Requests.ToDictionary(row => row.ScenarioId, StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch)
        {
            if (!requests.TryGetValue(row.ScenarioId, out var storedRequest))
            {
                requestInserts.Add(row.ScenarioId);
            }
            // An existing scenario is compared on its XML only: identical (after normalising) is
            // skipped, anything else updates XML_request. No other column is compared or changed, so
            // test_tags from the CSV are stored only when a scenario is first inserted.
            else if (string.Equals(TryNormalizeXml(storedRequest.XmlRequest), row.XmlRequest, StringComparison.Ordinal))
            {
                summary.SkippedRequests++;
            }
            else
            {
                requestUpdates.Add(row.ScenarioId);
            }
        }
    }

    private static ScenarioRequestImport RequestFromCsv(RequestCsvRow row)
    {
        var scenarioId = Required(row.ScenarioId, "scenario_id");
        var xml = NormalizeXml(row.Xml);
        var policy = XElement.Parse(xml) is { Name.LocalName: "Message" } message
            ? SingleChild(message, "Policy", "/Message/Policy")
            : throw new InvalidDataException("Request XML root element must be <Message>.");
        return new ScenarioRequestImport(scenarioId,
            Required(SingleChild(policy, "PolicyReference", "/Message/Policy/PolicyReference").Value, "PolicyReference (Quote_ref)"),
            Required(SingleChild(policy, "ProductCode", "/Message/Policy/ProductCode").Value, "ProductCode (Product_code)"),
            Required(SingleChild(policy, "SchemeCode", "/Message/Policy/SchemeCode").Value, "SchemeCode (Scheme_code)"),
            xml, ScenarioTags.Normalize(row.TestTags));
    }

    private static XElement SingleChild(XElement parent, string name, string path)
    {
        var matches = parent.Elements().Where(element => element.Name.LocalName == name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(matches.Length == 0
                ? $"Request XML has no {path} element."
                : $"Request XML has more than one {path} element.");
    }

    // Stored XML that can't be parsed simply counts as different, so a corrected CSV replaces it.
    private static string? TryNormalizeXml(string? value)
    {
        try
        {
            return NormalizeXml(value ?? string.Empty);
        }
        catch (Exception exception) when (exception is XmlException or InvalidDataException)
        {
            return null;
        }
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{field} is required.")
            : value.Trim();
    }

    private static string NormalizeXml(string value)
    {
        var xml = Required(value, "XML").TrimStart('﻿').Trim();
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader).Root?.ToString(SaveOptions.DisableFormatting)
            ?? throw new InvalidDataException("XML has no root element.");
    }
}
```

### `PricingValidationFramework.Tests\Helpers\Setup\IceTestSetup.cs`

Replace the entire contents with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Extraction;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Reporting;

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class IceTestSetup : IDisposable
{
    private readonly ILoggerFactory loggerFactory;

    private IceTestSetup(
        string buildId,
        IceSettings iceSettings,
        IceApiClient apiClient,
        IceUrlBuilder urlBuilder,
        JsonValueExtractor jsonExtractor,
        PricingComparisonService pricingProfiles,
        IceTestRunLogger logger,
        ILoggerFactory loggerFactory)
    {
        BuildId = buildId;
        IceSettings = iceSettings;
        ApiClient = apiClient;
        UrlBuilder = urlBuilder;
        JsonExtractor = jsonExtractor;
        PricingProfiles = pricingProfiles;
        Logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public string BuildId { get; }
    public IceSettings IceSettings { get; }
    public IceApiClient ApiClient { get; }
    public IceUrlBuilder UrlBuilder { get; }
    public JsonValueExtractor JsonExtractor { get; }
    public PricingComparisonService PricingProfiles { get; }
    public IceTestRunLogger Logger { get; }

    public static Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(CancellationToken cancellationToken = default) =>
        DiscoverScenariosAsync(includeLocalSettings: false, cancellationToken);

    public static async Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        bool includeLocalSettings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateDatabaseRetrySettings();
        var reader = new BaselineDataReader(new SqlConnectionFactory(databaseSettings), retrySettings);
        return await DiscoverScenariosAsync(reader, IceSelection.From(configuration), cancellationToken);
    }

    /// Which baselines ICE checks. With a Radar report (ICE_RADAR_REPORT): exactly the scenarios that
    /// passed in that Radar run. Otherwise the newest PASS baseline per product/scheme, within the same
    /// TEST_TAG product and SCENARIO_TAG tag filters as Radar.
    internal static async Task<IReadOnlyList<IceBaselineScenario>> DiscoverScenariosAsync(
        IBaselineDataReader reader,
        IceSelection selection,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(selection.RadarReport))
        {
            var passed = await RadarReportScenarios.ReadPassedScenarioIdsAsync(selection.RadarReport, cancellationToken);
            return passed.Count == 0 ? [] : await reader.GetPassingBaselinesByScenarioIdsAsync(passed, cancellationToken);
        }

        return string.IsNullOrWhiteSpace(selection.ProductCode) && string.IsNullOrWhiteSpace(selection.ScenarioTag)
            ? await reader.GetPassingBaselineScenariosAsync(cancellationToken)
            : await reader.GetFilteredPassingBaselineScenariosAsync(selection.ProductCode, selection.ScenarioTag, cancellationToken);
    }

    internal sealed record IceSelection(string ProductCode, string ScenarioTag, string? RadarReport)
    {
        public const string RadarReportVariable = "ICE_RADAR_REPORT";

        public static IceSelection From(IConfiguration configuration)
        {
            var report = Environment.GetEnvironmentVariable(RadarReportVariable) ?? configuration["PipelineSettings:IceRadarReport"];
            return new IceSelection(
                TestConfigurationLoader.ReadTestTag(configuration),
                TestConfigurationLoader.ReadScenarioTag(configuration),
                string.IsNullOrWhiteSpace(report) ? null : report.Trim());
        }
    }

    /// <param name="includeLocalSettings">True from the live fixture: also reads data/import/local-settings.json.</param>
    public static IceTestSetup Create(bool includeLocalSettings = false)
    {
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);

        var iceSettings = configuration.GetSection("IceSettings").Get<IceSettings>()
            ?? throw new InvalidOperationException("IceSettings is missing.");
        iceSettings.Validate();
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateApiRetrySettings();
        var profiles = TestConfigurationLoader.LoadProfiles(configuration);

        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local";

        var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        GlobalDiagnosticsContext.Set("BuildId", buildId);

        var apiClient = new IceApiClient(
            iceSettings,
            loggerFactory.CreateLogger<IceApiClient>(),
            retrySettings);

        return new IceTestSetup(
            buildId,
            iceSettings,
            apiClient,
            new IceUrlBuilder(),
            new JsonValueExtractor(),
            new PricingComparisonService(profiles.Profiles),
            new IceTestRunLogger(loggerFactory.CreateLogger<IceTestRunLogger>()),
            loggerFactory);
    }

    public void Dispose()
    {
        ApiClient.Dispose();
        loggerFactory.Dispose();
    }
}
```

### `PricingValidationFramework.Tests\Helpers\Setup\RadarTestSetup.cs`

Replace the entire contents with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog;
using System.Net;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Models.Common;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.Helpers.Setup;

public sealed class RadarTestSetup : IDisposable
{
    private readonly PipelineSettings settings;
    private readonly RadarRequestRateLimiter rateLimiter;
    private readonly RadarApiClient apiClient;
    private readonly ILoggerFactory loggerFactory;
    private bool disposed;

    private RadarTestSetup(
        PipelineSettings settings,
        RadarRequestRateLimiter rateLimiter,
        BaselineDataReader baselineReader,
        ResultUpdater resultUpdater,
        RadarApiClient apiClient,
        RadarPricingService pricingService,
        RadarTestRunLogger logger,
        ILoggerFactory loggerFactory)
    {
        this.settings = settings;
        this.rateLimiter = rateLimiter;
        this.apiClient = apiClient;
        BaselineReader = baselineReader;
        ResultUpdater = resultUpdater;
        PricingService = pricingService;
        Logger = logger;
        this.loggerFactory = loggerFactory;
    }

    public BaselineDataReader BaselineReader { get; }
    public ResultUpdater ResultUpdater { get; }
    public RadarPricingService PricingService { get; }
    public RadarTestRunLogger Logger { get; }

    public string BuildId => settings.BuildId;
    public string TestTag => settings.TestTag;
    public string RequestTime => settings.RequestTime;
    public decimal MinThreshold => settings.MinThreshold;
    public decimal MaxThreshold => settings.MaxThreshold;

    /// <param name="includeLocalSettings">True from the live fixture: also reads data/import/local-settings.json.</param>
    public static RadarTestSetup Create(bool includeLocalSettings = false)
    {
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);
        var radarConfiguration = LoadAndValidateRadarConfiguration(configuration);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var (pipelineSettings, retrySettings) = LoadValidatedPipelineInputs(configuration);
        var comparisonService = new PricingComparisonService(radarConfiguration.Profiles.Profiles);
        var rateLimiter = new RadarRequestRateLimiter(
            radarConfiguration.RateLimitSettings,
            radarConfiguration.RadarSettings.Endpoints.Keys);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        var connectionFactory = new SqlConnectionFactory(databaseSettings);
        var baselineReader = new BaselineDataReader(connectionFactory, retrySettings);
        var resultUpdater = new ResultUpdater(connectionFactory, retrySettings);
        var logger = new RadarTestRunLogger(loggerFactory.CreateLogger<RadarTestRunLogger>());
        GlobalDiagnosticsContext.Set("RadarBuildId", pipelineSettings.BuildId);
        var radarHttpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });
        var apiClient = new RadarApiClient(
            rateLimiter,
            radarHttpClient,
            loggerFactory.CreateLogger<RadarApiClient>(),
            retrySettings);
        var urlBuilder = new RadarUrlBuilder();
        var pricingService = new RadarPricingService(
            radarConfiguration.RadarSettings,
            apiClient,
            urlBuilder,
            comparisonService);
        return new RadarTestSetup(
            pipelineSettings,
            rateLimiter,
            baselineReader,
            resultUpdater,
            apiClient,
            pricingService,
            logger,
            loggerFactory);
    }

    public static Task<IReadOnlyList<ScenarioRequest>> DiscoverScenariosAsync(CancellationToken cancellationToken = default) =>
        DiscoverScenariosAsync(includeLocalSettings: false, cancellationToken);

    public static async Task<IReadOnlyList<ScenarioRequest>> DiscoverScenariosAsync(
        bool includeLocalSettings,
        CancellationToken cancellationToken = default)
    {
        var configuration = TestConfigurationLoader.Load(includeLocalSettings);
        var radarConfiguration = LoadAndValidateRadarConfiguration(configuration);
        var (pipelineSettings, retrySettings) = LoadValidatedPipelineInputs(configuration);
        var databaseSettings = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>()
            ?? throw new InvalidOperationException("DatabaseSettings is missing.");
        var reader = new RequestDataReader(new SqlConnectionFactory(databaseSettings), retrySettings);

        return string.IsNullOrWhiteSpace(pipelineSettings.TestTag) && string.IsNullOrWhiteSpace(pipelineSettings.ScenarioTag)
            ? await reader.GetAllScenariosAsync(cancellationToken)
            : await reader.GetFilteredScenariosAsync(pipelineSettings.TestTag, pipelineSettings.ScenarioTag, cancellationToken);
    }

    private static (PipelineSettings PipelineSettings, RetrySettings RetrySettings) LoadValidatedPipelineInputs(
        IConfiguration configuration)
    {
        var retrySettings = configuration.GetSection("RetrySettings").Get<RetrySettings>()
            ?? throw new InvalidOperationException("RetrySettings is missing.");
        retrySettings.ValidateRadarPipelineSettings();

        var pipelineSettings = new PipelineSettings
        {
            BuildId = ReadRequiredBuildId(configuration),
            MinThreshold = ReadRequiredDecimal(configuration, "RADAR_MIN_THRESHOLD", "MinThreshold"),
            MaxThreshold = ReadRequiredDecimal(configuration, "RADAR_MAX_THRESHOLD", "MaxThreshold"),
            RequestTime = RequestTimeFormatter.Resolve(
                Environment.GetEnvironmentVariable("RADAR_REQUEST_DATETIME") ?? configuration["PipelineSettings:RequestTime"]),
            TestTag = TestConfigurationLoader.ReadTestTag(configuration),
            ScenarioTag = TestConfigurationLoader.ReadScenarioTag(configuration)
        };
        new PipelineInputValidator().Validate(pipelineSettings);

        return (pipelineSettings, retrySettings);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        apiClient.Dispose();
        rateLimiter.Dispose();
        loggerFactory.Dispose();
        GlobalDiagnosticsContext.Remove("RadarBuildId");
    }

    private static string ReadRequiredBuildId(IConfiguration configuration)
    {
        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID")
            ?? configuration["PipelineSettings:BuildId"];

        if (!string.IsNullOrWhiteSpace(buildId))
        {
            return buildId.Trim();
        }

        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.Equals(environmentName, "local", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        throw new InvalidOperationException("BUILD_BUILDID is required outside local development.");
    }

    private static decimal ReadRequiredDecimal(IConfiguration configuration, string variableName, string settingName)
    {
        var value = Environment.GetEnvironmentVariable(variableName) ?? configuration[$"PipelineSettings:{settingName}"];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{variableName} is required and must be a valid decimal.");
        }

        if (!decimal.TryParse(value.Trim(), global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{variableName} must be a valid decimal.");
        }

        return parsed;
    }

    private static RadarRunConfiguration LoadAndValidateRadarConfiguration(IConfiguration configuration)
    {
        var radarSection = configuration.GetSection("RadarSettings");
        if (!radarSection.Exists())
        {
            throw new InvalidOperationException("RadarSettings is missing.");
        }

        var radarSettings = radarSection.Get<RadarSettings>()
            ?? throw new InvalidOperationException("RadarSettings is missing.");
        var profiles = TestConfigurationLoader.LoadProfiles(configuration);
        radarSettings.Routes = profiles.CreateRadarRoutes();
        var rateLimitSection = configuration.GetSection("RadarRateLimitSettings");
        if (!rateLimitSection.Exists())
        {
            throw new InvalidOperationException("RadarRateLimitSettings is missing.");
        }

        var rateLimitSettings = rateLimitSection.Get<RadarRateLimitSettings>()
            ?? throw new InvalidOperationException("RadarRateLimitSettings is missing.");
        // Only the endpoints in use need values (blank ones are dropped); a scenario whose route names
        // a missing endpoint is an EndpointResolution ERROR rather than failing the whole run.
        RadarSettingsValidator.RemoveBlankEndpoints(radarSettings);
        RadarSettingsValidator.Validate(radarSettings, rateLimitSettings, requireEndpointForEveryRoute: false);

        return new RadarRunConfiguration(radarSettings, rateLimitSettings, profiles);
    }

    private sealed record RadarRunConfiguration(
        RadarSettings RadarSettings,
        RadarRateLimitSettings RateLimitSettings,
        PricingProfileConfiguration Profiles);
}
```

### `PricingValidationFramework.Tests\Helpers\Setup\TestConfigurationLoader.cs`

Replace the entire contents with:

```csharp
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Validation;

namespace PricingValidationFramework.Tests.Helpers.Setup;

/// Shared between IceTestSetup and RadarTestSetup: appsettings.json, an optional
/// appsettings.{environment}.json, appsettings.Development.json when running locally, then (for
/// Development/local runs only) data/import/local-settings.json - the file DataLoader import and
/// seed use - and environment variables (double-underscore translated to the ":" section separator
/// IConfiguration expects) layered on top, highest priority last.
internal static class TestConfigurationLoader
{
    public static PricingProfileConfiguration LoadProfiles(IConfiguration configuration)
    {
        if (configuration.GetSection("RadarSettings:Routes").Exists() ||
            configuration.GetSection("RadarSettings:ResponseXsdMappings").Exists())
        {
            throw new InvalidOperationException("Move RadarSettings routes and schema mappings to PricingProfiles:MappingFile.");
        }
        return PricingProfileConfiguration.Load(PricingProfileConfiguration.ResolveMappingPath(
            configuration["PricingProfiles:MappingFile"], TestContext.CurrentContext.TestDirectory));
    }

    public const string LocalSettingsVariable = "PVF_LOCAL_SETTINGS";

    /// The product filter shared by Radar and ICE: TEST_TAG, then PipelineSettings:TestTag; empty means all.
    /// It matches [Xml].[Request].Product_code exactly, so both stages of a Radar run select the same products.
    public static string ReadTestTag(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("TEST_TAG") ?? configuration["PipelineSettings:TestTag"] ?? string.Empty;

    /// The tag filter shared by Radar and ICE: SCENARIO_TAG, then PipelineSettings:ScenarioTag; empty means all.
    /// One tag, matched against the comma-separated [Xml].[Request].Test_tags.
    public static string ReadScenarioTag(IConfiguration configuration) =>
        ScenarioTags.ValidateFilter(
            Environment.GetEnvironmentVariable("SCENARIO_TAG") ?? configuration["PipelineSettings:ScenarioTag"],
            "SCENARIO_TAG");

    /// PVF_LOCAL_SETTINGS when set, otherwise data/import/local-settings.json in the folder holding
    /// PricingValidationFramework.slnx, found by walking up from the test output folder. Null if absent.
    public static string? FindLocalSettings(string startDirectory)
    {
        var explicitPath = Environment.GetEnvironmentVariable(LocalSettingsVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        for (var folder = new DirectoryInfo(startDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PricingValidationFramework.slnx")))
            {
                var candidate = Path.Combine(folder.FullName, "data", "import", "local-settings.json");
                return File.Exists(candidate) ? candidate : null;
            }
        }

        return null;
    }

    /// <param name="includeLocalSettings">
    /// True only for the live Radar/ICE fixtures, so unit and system tests never depend on a
    /// developer's local-settings.json.
    /// </param>
    public static IConfiguration Load(bool includeLocalSettings = false)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(TestContext.CurrentContext.TestDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true);

        if (string.Equals(environmentName, "local", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddJsonFile("appsettings.Development.json", optional: true);
        }

        // Local runs share one settings file with DataLoader import/seed. Pipelines set another
        // environment (e.g. Uat), and the file is ignored by Git, so it never applies there.
        if (includeLocalSettings &&
            (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
             environmentName.Equals("local", StringComparison.OrdinalIgnoreCase)))
        {
            var localSettings = FindLocalSettings(TestContext.CurrentContext.TestDirectory);
            if (localSettings is not null)
            {
                builder.AddJsonFile(localSettings, optional: false, reloadOnChange: false);
            }
        }

        return builder
            .AddInMemoryCollection(GetEnvironmentVariables())
            .Build();
    }

    public static Dictionary<string, string?> GetEnvironmentVariables()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            values[key.Replace("__", ":", StringComparison.Ordinal)] = Environment.GetEnvironmentVariable(key);
        }

        return values;
    }
}
```

### `PricingValidationFramework.Tests\Integration\Ice\IceValidationTests.cs`

Replace the entire contents with:

```csharp
using NUnit.Framework;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.Integration.Ice;

[TestFixture]
[Explicit("Requires configured [Xml].[Request]/[Xml].[Response] database tables, ICE endpoint, credentials, and client certificate.")]
[NonParallelizable]
public class IceValidationTests
{
    private readonly List<IceValidationReportRow> reportRows = [];
    private IceTestSetup? setup;
    private Exception? setupFailure;
    private bool cancellationObserved;
    private readonly NLog.Logger diagnostics = NLog.LogManager.GetLogger("PricingValidationFramework.Tests.Integration.Ice");

    public static IEnumerable<TestCaseData> IceScenarios
    {
        get
        {
            try
            {
                var scenarios = IceTestSetup.DiscoverScenariosAsync(
                    includeLocalSettings: true, TestContext.CurrentContext.CancellationToken).GetAwaiter().GetResult();
                return CreateScenarioCases(scenarios);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A missing or unreadable Radar report names the file, which is safe to show.
                return [DiscoveryFailure(exception is FileNotFoundException or InvalidDataException
                    ? $"ICE scenario discovery failed: {exception.Message}"
                    : $"ICE scenario discovery failed ({exception.GetType().Name}).")];
            }
        }
    }

    internal static IReadOnlyList<TestCaseData> CreateScenarioCases(IEnumerable<IceBaselineScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var selectedScenarios = scenarios.ToArray();
        if (selectedScenarios.Length == 0)
        {
            return [DiscoveryFailure("No ICE scenarios were returned for the selected workload.")];
        }

        if (selectedScenarios.GroupBy(scenario => scenario.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            return [DiscoveryFailure("Duplicate ScenarioId values were returned for the ICE workload.")];
        }

        return selectedScenarios.Select(scenario =>
        {
            var safeId = new string(scenario.ScenarioId.Take(64)
                .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                    ? character
                    : '_').ToArray());
            return new TestCaseData(scenario, null)
                .SetName($"Ice_scenario_{(string.IsNullOrWhiteSpace(safeId) ? "unknown" : safeId)}")
                .SetCategory("Ice");
        }).ToArray();
    }

    private static TestCaseData DiscoveryFailure(string safeReason)
    {
        return new TestCaseData(null, safeReason)
            .SetName("Ice_scenario_discovery_should_succeed")
            .SetCategory("IceDiscovery");
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        NLog.GlobalDiagnosticsContext.Set("BuildId", Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local");
        diagnostics.Info("ICE setup starting.");
        try
        {
            setup = IceTestSetup.Create(includeLocalSettings: true);
            setup.Logger.ExecutionStarted(setup.BuildId);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
            diagnostics.Error("ICE setup failed ({0}). {1}", exception.GetType().Name,
                exception is InvalidDataException or FileNotFoundException ? exception.Message : "Check runtime inputs and configuration.");
        }
    }

    [TestCaseSource(nameof(IceScenarios))]
    public async Task Ice_scenario_should_match_baseline(IceBaselineScenario? scenario, string? discoveryFailure)
    {
        if (discoveryFailure is not null)
        {
            reportRows.Add(new IceValidationReportRow { ScenarioId = "IceDiscovery", Result = ScenarioResult.Error, Error = discoveryFailure });
            diagnostics.Error(discoveryFailure);
            throw new AssertionException(discoveryFailure);
        }

        if (scenario is null)
        {
            throw new AssertionException("ICE scenario discovery produced no scenario data.");
        }

        if (setup is null)
        {
            reportRows.Add(IceReportingHelper.BuildRow(Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local",
                scenario, null, null, ScenarioResult.Error));
            reportRows[^1].Error = $"ICE setup failed ({setupFailure?.GetType().Name ?? "Unknown"}). Check the run log.";
            throw new InvalidOperationException($"ICE test setup failed ({setupFailure?.GetType().Name ?? "Unknown"}).");
        }

        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        IceValidationReportRow row;
        Exception? scenarioError = null;
        string profileId = string.Empty, fieldName = string.Empty, xpath = string.Empty;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = setup.UrlBuilder.Build(setup.IceSettings.IceEndpoint, scenario.QuoteRef);
            var payload = await setup.ApiClient.GetAsync(url, cancellationToken);
            var iceValue = setup.JsonExtractor.ExtractPremium(payload);
            var profile = setup.PricingProfiles.GetProfile(scenario.ProductCode, scenario.SchemeCode);
            profileId = profile.ProfileId;
            fieldName = profile.IceField;
            xpath = profile.Fields[fieldName];
            setup.Logger.BaselineFieldSelected(scenario.ScenarioId, profile.ProfileId, profile.IceField, profile.Fields[profile.IceField]);
            var baselineValue = setup.PricingProfiles.ExtractIceBaseline(scenario.ProductCode, scenario.SchemeCode, scenario.XmlResponse);
            row = IceReportingHelper.BuildRow(
                setup.BuildId,
                scenario,
                iceValue,
                baselineValue,
                iceValue == baselineValue ? ScenarioResult.Pass : ScenarioResult.Fail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationObserved = true;
            setup.Logger.Cancellation(
                "ICE scenario cancelled. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.",
                scenario.ScenarioId,
                scenario.QuoteRef);
            throw;
        }
        catch (Exception exception)
        {
            scenarioError = exception;
            setup.Logger.ExecutionFailed(scenario.ScenarioId, scenario.QuoteRef, exception);
            row = IceReportingHelper.BuildRow(setup.BuildId, scenario, null, null, ScenarioResult.Error);
            row.Error = exception.Message;
        }

        row.ProfileId = profileId;
        row.FieldName = fieldName;
        row.XPath = xpath;
        RecordScenarioResult(row, scenarioError);
    }

    internal void RecordScenarioResult(IceValidationReportRow row, Exception? scenarioError = null)
    {
        if (scenarioError is not null && string.IsNullOrEmpty(row.Error))
        {
            row.Error = scenarioError.Message;
        }
        reportRows.Add(row);
        if (row.Result == ScenarioResult.Pass)
        {
            return;
        }

        if (row.Result == ScenarioResult.Fail)
        {
            Assert.Fail(
                $"ICE comparison failed. ScenarioId={row.ScenarioId}, QuoteRef={row.QuoteRef}, " +
                $"ProductCode={row.ProductCode}, SchemeCode={row.SchemeCode}, " +
                $"IceValue={row.IceValue}, BaselineValue={row.BaselineValue}.");
        }

        Assert.Fail(
            $"ICE scenario completed with ERROR. ScenarioId={row.ScenarioId}, " +
            $"ErrorType={scenarioError?.GetType().Name ?? "IceProcessingError"}, Error={scenarioError?.Message}.");
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        try
        {
            if (setup is not null)
            {
                setup.Logger.ExecutionCompleted(setup.BuildId);
            }

            if (reportRows.Count == 0)
            {
                reportRows.Add(new IceValidationReportRow { ScenarioId = "IceRun", Result = ScenarioResult.Error,
                    Error = cancellationObserved ? "ICE run was cancelled before any scenario completed." : "No ICE scenarios completed." });
            }
            var buildId = setup?.BuildId ?? reportRows.Select(row => row.BuildId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
                ?? Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local";
            await IceReportingHelper.WriteReportAsync(buildId, reportRows, CancellationToken.None);
            diagnostics.Info("ICE report written.");
        }
        catch (Exception exception)
        {
            diagnostics.Error("ICE report generation failed ({0}).", exception.GetType().Name);
            throw;
        }
        finally
        {
            setup?.Dispose();
            NLog.LogManager.Flush();
        }
    }
}
```

### `PricingValidationFramework.Tests\Integration\Radar\RadarValidationTests.cs`

Replace the entire contents with:

```csharp
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Tests.Helpers.Reporting;
using PricingValidationFramework.Tests.Helpers.Setup;
using PricingValidationFramework.Tests.Helpers.Validation;

namespace PricingValidationFramework.Tests.Integration.Radar;

[TestFixture]
[Explicit("Requires XML database access, Radar endpoint credentials, pipeline inputs, and pricing XPath mappings.")]
[Parallelizable(ParallelScope.Children)]
public class RadarValidationTests
{
    private const string MissingBaselineStage = "MissingBaseline";

    private readonly RadarReportCollection reportRows = new();
    private RadarTestSetup? setup;
    private Exception? setupFailure;
    private int cancellationObserved;
    private readonly NLog.Logger diagnostics = NLog.LogManager.GetLogger("PricingValidationFramework.Tests.Integration.Radar");

    public static IEnumerable<TestCaseData> RadarScenarios
    {
        get
        {
            try
            {
                var scenarios = RadarTestSetup.DiscoverScenariosAsync(
                    includeLocalSettings: true, TestContext.CurrentContext.CancellationToken).GetAwaiter().GetResult();
                return RadarScenarioTestCases.Create(scenarios);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return [RadarScenarioTestCases.DiscoveryFailure(
                    $"Radar scenario discovery failed ({exception.GetType().Name}).")];
            }
        }
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        NLog.GlobalDiagnosticsContext.Set("RadarBuildId", Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local");
        diagnostics.Info("Radar setup starting.");
        try
        {
            setup = RadarTestSetup.Create(includeLocalSettings: true);
            setup.Logger.ExecutionStarted(setup.BuildId);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
            diagnostics.Error("Radar setup failed ({0}). {1}", exception.GetType().Name,
                exception is InvalidDataException or FileNotFoundException ? exception.Message : "Check runtime inputs and configuration.");
        }
    }

    [TestCaseSource(nameof(RadarScenarios))]
    public async Task Radar_scenario_should_validate(ScenarioRequest? scenario, string? discoveryFailure)
    {
        if (discoveryFailure is not null)
        {
            reportRows.Add(new RadarValidationReportRow { ScenarioId = "RadarDiscovery", Result = ScenarioResult.Error,
                FailureStage = "Discovery", Error = discoveryFailure });
            diagnostics.Error(discoveryFailure);
            throw new AssertionException(discoveryFailure);
        }

        if (scenario is null)
        {
            throw new AssertionException("Radar scenario discovery produced no scenario data.");
        }

        if (setup is null)
        {
            reportRows.Add(new RadarValidationReportRow { ScenarioId = scenario.ScenarioId, QuoteRef = scenario.QuoteRef,
                ProductCode = scenario.ProductCode, SchemeCode = scenario.SchemeCode, RequestXml = scenario.XmlRequest,
                Result = ScenarioResult.Error, FailureStage = "Setup",
                Error = $"Radar setup failed ({setupFailure?.GetType().Name ?? "Unknown"}). Check the run log." });
            throw new InvalidOperationException($"Radar test setup failed ({setupFailure?.GetType().Name ?? "Unknown"}).");
        }

        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        ScenarioResponse? baseline = null;
        RadarValidationReportRow row;
        Exception? scenarioError = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            baseline = await setup.BaselineReader
                .GetBaselineByScenarioIdAsync(scenario.ScenarioId, cancellationToken);

            // Radar compares only. Baselines are created by `DataLoader seed` (the data-loader
            // pipeline's Seed baselines stage), so a scenario without one is an error, not a PASS.
            if (baseline is null)
            {
                var missing = $"No baseline in [Xml].[Response] for ScenarioId '{scenario.ScenarioId}'. Run DataLoader seed first.";
                setup.Logger.ExecutionFailed(
                    scenario.ScenarioId,
                    scenario.QuoteRef,
                    scenario.ProductCode,
                    scenario.SchemeCode,
                    null,
                    MissingBaselineStage,
                    new InvalidOperationException(missing),
                    safeDetail: missing);
                row = BuildErrorRow(scenario, setup, string.Empty, string.Empty, MissingBaselineStage, missing);
            }
            else
            {
                (row, scenarioError) = await CompareAndPersistAsync(setup, scenario, baseline, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref cancellationObserved, 1);
            setup.Logger.Cancellation(
                "Radar scenario cancelled. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, ExecutionStage={ExecutionStage}.",
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                "ScenarioExecution");
            throw;
        }
        catch (Exception exception)
        {
            scenarioError = exception;
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                exception is RadarRequestRateLimitException rateLimitException ? rateLimitException.EndpointName : null,
                exception switch
                {
                    RadarRequestRateLimitException => "RateLimitPermit",
                    HttpRequestException { StatusCode: global::System.Net.HttpStatusCode.TooManyRequests } => "Http429",
                    _ => "BaselineLoad"
                },
                exception,
                (exception as HttpRequestException)?.StatusCode is { } statusCode ? (int)statusCode : null);
            row = BuildErrorRow(scenario, setup, baseline?.XmlResponse ?? string.Empty, string.Empty, "ScenarioExecution", exception.Message);
        }

        reportRows.Add(row);
        AssertScenarioResult(row, scenarioError);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        try
        {
            setup?.Logger.ExecutionCompleted(setup.BuildId);
            if (reportRows.Count == 0)
            {
                reportRows.Add(new RadarValidationReportRow { ScenarioId = "RadarRun", Result = ScenarioResult.Error,
                    FailureStage = "Execution", Error = Volatile.Read(ref cancellationObserved) != 0
                        ? "Radar run was cancelled before any scenario completed." : "No Radar scenarios completed." });
            }
            await RadarReportingHelper.WriteReportAsync(
                setup?.BuildId ?? Environment.GetEnvironmentVariable("BUILD_BUILDID") ?? "local",
                reportRows.ReadSortedRows(), CancellationToken.None);
            diagnostics.Info("Radar report written.");
        }
        catch (Exception exception)
        {
            diagnostics.Error("Radar report generation failed ({0}).", exception.GetType().Name);
            throw;
        }
        finally
        {
            reportRows.Dispose();
            setup?.Dispose();
            NLog.LogManager.Flush();
        }
    }

    private static async Task<(RadarValidationReportRow Row, Exception? Error)> CompareAndPersistAsync(
        RadarTestSetup setup,
        ScenarioRequest scenario,
        ScenarioResponse baseline,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(baseline.Status, "PASS", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"No passing baseline was found for ScenarioId '{scenario.ScenarioId}' (Status is '{baseline.Status}').");
        }

        var run = await setup.PricingService.RunAsync(
            scenario,
            baseline.XmlResponse,
            setup.RequestTime,
            setup.MinThreshold,
            setup.MaxThreshold,
            cancellationToken);
        var row = RadarReportingHelper.BuildComparisonRow(
            setup.BuildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            baseline.XmlResponse,
            run.RadarResponseXml,
            setup.MinThreshold,
            setup.MaxThreshold,
            run.Comparison);
        if (row.Result != ScenarioResult.Pass)
        {
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                run.RouteId,
                row.FailureStage ?? "PricingComparison",
                new InvalidOperationException(row.Error ?? $"Pricing comparison returned {row.Result}."),
                safeDetail: row.Error);
        }

        if (row.Result == ScenarioResult.Error || string.IsNullOrWhiteSpace(row.RadarResponseXml))
        {
            return (row, null);
        }

        try
        {
            await PersistResultAsync(setup, baseline, row, cancellationToken);
            return (row, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            setup.Logger.ExecutionFailed(
                scenario.ScenarioId,
                scenario.QuoteRef,
                scenario.ProductCode,
                scenario.SchemeCode,
                null,
                "ResultPersistence",
                exception);
            return (BuildErrorRow(scenario, setup, baseline.XmlResponse, row.RadarResponseXml, "ResultPersistence", exception.Message), exception);
        }
    }

    private static void AssertScenarioResult(RadarValidationReportRow row, Exception? scenarioError)
    {
        if (row.Result == ScenarioResult.Pass)
        {
            return;
        }

        if (row.Result == ScenarioResult.Fail)
        {
            Assert.Fail(
                $"Radar comparison failed. ScenarioId={row.ScenarioId}, FailedFields={row.FieldComparisons.Count(field => field.Result == ScenarioResult.Fail)}, " +
                $"Details={string.Join(" | ", row.FieldComparisons.Where(field => field.Result == ScenarioResult.Fail).Select(field => $"{field.FieldKey}: expected={field.Expected}, actual={field.Actual}, delta={field.Delta}"))}.");
        }

        var errorType = scenarioError?.GetType().Name ?? row.FailureStage ?? "RadarProcessingError";
        Assert.Fail($"Radar scenario completed with ERROR. ScenarioId={row.ScenarioId}, ErrorType={errorType}, Error={row.Error}.");
    }

    private static async Task PersistResultAsync(
        RadarTestSetup setup,
        ScenarioResponse baseline,
        RadarValidationReportRow row,
        CancellationToken cancellationToken)
    {
        var response = new ScenarioResponse
        {
            ScenarioId = baseline.ScenarioId,
            QuoteRef = baseline.QuoteRef,
            XmlResponse = row.RadarResponseXml,
            CreatedDate = baseline.CreatedDate,
            LastUpdated = baseline.LastUpdated,
            Status = row.Result == ScenarioResult.Pass ? "PASS" : "FAIL"
        };

        await setup.ResultUpdater.UpdateResultAsync(response, cancellationToken);
    }

    private static RadarValidationReportRow BuildErrorRow(
        ScenarioRequest scenario,
        RadarTestSetup setup,
        string baselineXml,
        string radarResponseXml,
        string failureStage,
        string error)
    {
        return RadarReportingHelper.BuildComparisonRow(
            setup.BuildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            baselineXml,
            radarResponseXml,
            setup.MinThreshold,
            setup.MaxThreshold,
            new PricingComparisonResult(Array.Empty<DecimalFieldComparison>(), error, failureStage));
    }
}
```

### `PricingValidationFramework.Tests\System\DataLoaderTests.cs`

Replace the entire contents with:

```csharp
using System.Globalization;
using CsvHelper;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.DataLoader;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class DataLoaderTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"loader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public async Task Loader_should_insert_requests_with_quoted_multiline_xml()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC", "<Label>a,\"b\"\nnext</Label>")]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var request = repository.Batches.Single().Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(request.QuoteRef, Is.EqualTo("Q1"));
            Assert.That(request.ProductCode, Is.EqualTo("HOME"));
            Assert.That(request.SchemeCode, Is.EqualTo("ABC"));
            Assert.That(request.TestTags, Is.Empty);
            Assert.That(request.XmlRequest.Replace("\r\n", "\n", StringComparison.Ordinal), Does.Contain("a,\"b\"\nnext"));
        });
    }

    [Test]
    public async Task Optional_test_tags_column_should_be_normalised_and_stored_on_insert()
    {
        var requests = WriteCsv("requests.csv", ["scenario_id", "xml", "test_tags"],
            [["S1", PolicyXml("Q1", "HOME", "ABC"), " smoke ; regression,Smoke "], ["S2", PolicyXml("Q2", "HOME", "ABC"), ""]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var inserted = repository.Batches.Single().ToDictionary(request => request.ScenarioId);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(inserted["S1"].TestTags, Is.EqualTo("smoke,regression"));
            Assert.That(inserted["S2"].TestTags, Is.Empty);
        });
    }

    [TestCase("smoke test")]
    [TestCase("smoke|regression")]
    [TestCase("%")]
    public async Task Invalid_test_tags_should_be_reported_without_importing(string tags)
    {
        var requests = WriteCsv("requests.csv", ["scenario_id", "xml", "test_tags"], [["S1", PolicyXml("Q1", "HOME", "ABC"), tags]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("Test tag"));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Test_tags_on_an_existing_scenario_should_not_cause_an_update()
    {
        var xml = PolicyXml("Q1", "HOME", "ABC");
        var requests = WriteCsv("requests.csv", ["scenario_id", "xml", "test_tags"], [["S1", xml, "regression"]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", xml, "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(summary.UpdatedRequests, Is.Zero);
            Assert.That(repository.Requests.Single().TestTags, Is.EqualTo("smoke"), "tags are only set on insert");
        });
    }

    [Test]
    public async Task Identifiers_should_be_read_from_the_policy_element_of_the_request_xml()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Message>\n   <Policy>\n      <PolicyReference> QU07446731 </PolicyReference>\n" +
            "      <ProductCode>CommercialVehicle_v2</ProductCode>\n      <SchemeCode>AcornInsureCV</SchemeCode>\n   </Policy>\n</Message>";
        var requests = WriteCsv("requests.csv", RequestHeaders, [["CV-1", xml]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var request = repository.Batches.Single().Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(request.QuoteRef, Is.EqualTo("QU07446731"));
            Assert.That(request.ProductCode, Is.EqualTo("CommercialVehicle_v2"));
            Assert.That(request.SchemeCode, Is.EqualTo("AcornInsureCV"));
            Assert.That(request.XmlRequest, Does.StartWith("<Message>"));
        });
    }

    [TestCase("<Message><Policy><ProductCode>P</ProductCode><SchemeCode>S</SchemeCode></Policy></Message>", "/Message/Policy/PolicyReference")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><SchemeCode>S</SchemeCode></Policy></Message>", "/Message/Policy/ProductCode")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><ProductCode>P</ProductCode></Policy></Message>", "/Message/Policy/SchemeCode")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><ProductCode>P</ProductCode><SchemeCode> </SchemeCode></Policy></Message>", "SchemeCode (Scheme_code) is required")]
    [TestCase("<Message><Policy><PolicyReference>Q</PolicyReference><PolicyReference>Q2</PolicyReference><ProductCode>P</ProductCode><SchemeCode>S</SchemeCode></Policy></Message>", "more than one /Message/Policy/PolicyReference")]
    [TestCase("<Message />", "/Message/Policy")]
    [TestCase("<Request />", "root element must be <Message>")]
    public async Task Requests_missing_an_identifier_should_be_rejected_before_database_access(string xml, string expectedReason)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", xml]]);
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain(expectedReason));
            Assert.That(repository.ReadCount, Is.Zero);
        });
    }

    [Test]
    public async Task A_folder_should_load_the_newest_versioned_csv_by_its_filename_timestamp()
    {
        WriteCsv("scenarios-20260923-092037-291.csv", RequestHeaders, [["S-OLD", PolicyXml("Q1", "HOME", "ABC")]]);
        var newest = WriteCsv("scenarios-20260923-092114-865.csv", RequestHeaders, [["S-NEW", PolicyXml("Q2", "HOME", "ABC")]]);
        WriteCsv("scenarios.csv", RequestHeaders, [["S-UNVERSIONED", PolicyXml("Q3", "HOME", "ABC")]]);
        WriteCsv("requests.csv", RequestHeaders, [["S-FALLBACK", PolicyXml("Q4", "HOME", "ABC")]]);
        // Modified times must not matter: make the newest-named file look oldest on disk.
        File.SetLastWriteTimeUtc(newest, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(directory, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestFile, Is.EqualTo(newest));
            Assert.That(repository.Batches.Single().Single().ScenarioId, Is.EqualTo("S-NEW"));
        });
    }

    [Test]
    public async Task A_folder_without_versioned_csvs_should_fall_back_to_requests_csv()
    {
        var requests = RequestFile();
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(directory, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestFile, Is.EqualTo(requests));
        });
    }

    [Test]
    public async Task A_file_path_should_be_used_as_given()
    {
        var older = WriteCsv("scenarios-20260101-000000-000.csv", RequestHeaders, [["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        WriteCsv("scenarios-20260923-092114-865.csv", RequestHeaders, [["S2", PolicyXml("Q2", "HOME", "ABC")]]);

        var summary = await new ScenarioDataLoader(new FakeRepository()).RunAsync(new ImportOptions(older, true));

        Assert.That(summary.RequestFile, Is.EqualTo(older));
    }

    [TestCase(true, "No scenarios-{timestamp}.csv or requests.csv was found")]
    [TestCase(false, "was not found")]
    public async Task A_folder_with_no_csv_or_a_missing_path_should_be_reported(bool folderExists, string expectedReason)
    {
        var path = folderExists ? directory : Path.Combine(directory, "missing");
        var repository = new FakeRepository();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(path, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain(expectedReason));
            Assert.That(repository.ReadCount, Is.Zero);
        });
    }

    [Test]
    public async Task Request_time_now_should_resolve_to_the_current_utc_hour()
    {
        var before = DateTime.UtcNow;
        var summary = await new ScenarioDataLoader(new FakeRepository()).RunAsync(new ImportOptions(RequestFile(), true, RequestTime: "NOW"));
        var after = DateTime.UtcNow;

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.RequestTime, Is.AnyOf(
                before.ToString("yyyy-MM-dd'Z'HH:00:00", CultureInfo.InvariantCulture),
                after.ToString("yyyy-MM-dd'Z'HH:00:00", CultureInfo.InvariantCulture)));
        });
    }

    [Test]
    public async Task An_explicit_request_time_should_be_kept_at_the_start_of_its_hour()
    {
        var summary = await new ScenarioDataLoader(new FakeRepository()).RunAsync(
            new ImportOptions(RequestFile(), true, RequestTime: " 2026-10-12Z09:45:30 "));

        Assert.That(summary.RequestTime, Is.EqualTo("2026-10-12Z09:00:00"));
    }

    [TestCase("12/10/2026 09:00")]
    [TestCase("2026-10-12T09:00:00")]
    [TestCase("tomorrow")]
    public async Task An_invalid_request_time_should_stop_before_reading_files_or_the_database(string requestTime)
    {
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(
            new ImportOptions(Path.Combine(directory, "missing"), false, RequestTime: requestTime));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().Reason, Does.Contain("yyyy-MM-ddZHH:mm:ss"));
            Assert.That(summary.RequestFile, Is.Empty);
            Assert.That(repository.ReadCount, Is.Zero);
        });
    }

    [Test]
    public async Task No_request_time_option_should_leave_it_empty()
    {
        var summary = await new ScenarioDataLoader(new FakeRepository()).RunAsync(new ImportOptions(RequestFile(), true));

        Assert.That(summary.RequestTime, Is.Empty);
    }

    [Test]
    public async Task Validate_should_not_read_or_write_the_database()
    {
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), true));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Identical_requests_should_be_skipped_even_when_the_stored_row_has_tags()
    {
        var repository = new FakeRepository
        {
            Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "smoke")]
        };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Changed_xml_should_update_the_request_and_preserve_existing_tags()
    {
        var oldXml = PolicyXml("Q1", "HOME", "ABC", "<Label>bob</Label>");
        var newXml = PolicyXml("Q2", "HOME", "ABC", "<Label>tom</Label>");
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", newXml]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", oldXml, "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.Zero);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(repository.Batches, Is.Empty);
            Assert.That(repository.Requests.Single().XmlRequest, Does.Contain("<Label>tom</Label>"));
            Assert.That(repository.Requests.Single().QuoteRef, Is.EqualTo("Q1"), "only the XML is updated");
            Assert.That(repository.Requests.Single().TestTags, Is.EqualTo("smoke"));
        });

        var rerun = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        Assert.That(rerun.SkippedRequests, Is.EqualTo(1));
        Assert.That(rerun.UpdatedRequests, Is.Zero);
        Assert.That(repository.WriteCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Mixed_inserts_updates_and_skips_should_use_one_write_batch()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")],
             ["S1", PolicyXml("Q1", "HOME", "ABC", "<Label>tom</Label>")],
             ["s2", PolicyXml("Q2", "HOME", "ABC")]]);
        var repository = new FakeRepository
        {
            Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC", "<Label>bob</Label>"), "smoke"),
                new("S2", "Q2", "HOME", "ABC", PolicyXml("Q2", "HOME", "ABC"), "regression")]
        };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(repository.WriteCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_large_multiline_request_should_update_without_truncation()
    {
        var label = string.Join("\n", Enumerable.Repeat(new string('x', 100), 460));
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC", $"<Label>{label}</Label>")]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var document = global::System.Xml.Linq.XElement.Parse(repository.Requests.Single().XmlRequest);

        Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
        Assert.That(document.Element("Policy")!.Element("Label")!.Value, Is.EqualTo(label));
    }

    [Test]
    public async Task A_later_write_failure_should_report_only_committed_updates()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC", "<Label>tom</Label>")],
             ["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository
        {
            Requests = [new("S0", "Q0", "HOME", "ABC", PolicyXml("Q0", "HOME", "ABC", "<Label>bob</Label>"), "smoke")],
            FailOnBatch = 2
        };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(summary.InsertedRequests, Is.Zero);
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(repository.Requests.Single().XmlRequest, Does.Contain("<Label>tom</Label>"));
        });
    }

    [TestCase("MOTOR", "ABC")]
    [TestCase("HOME", "XYZ")]
    [TestCase("home", "ABC")]
    public async Task Product_or_scheme_changes_should_update_the_xml_only(string productCode, string schemeCode)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", PolicyXml("Q1", productCode, schemeCode)]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", PolicyXml("Q1", "HOME", "ABC"), "")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));
        var stored = repository.Requests.Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(stored.XmlRequest, Does.Contain($"<ProductCode>{productCode}</ProductCode>"));
            Assert.That(stored.ProductCode, Is.EqualTo("HOME"), "only XML_request is updated");
            Assert.That(stored.SchemeCode, Is.EqualTo("ABC"));
        });
    }

    [Test]
    public async Task Identical_xml_should_be_skipped_even_when_the_stored_columns_differ()
    {
        var xml = PolicyXml("Q1", "HOME", "ABC");
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", xml]]);
        var repository = new FakeRepository { Requests = [new("S1", "OTHER-QUOTE", "OTHER", "XYZ", xml, "smoke")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.SkippedRequests, Is.EqualTo(1));
            Assert.That(summary.UpdatedRequests, Is.Zero);
            Assert.That(repository.WriteCount, Is.Zero);
        });
    }

    [Test]
    public async Task Unreadable_stored_xml_should_be_replaced_by_the_csv_xml()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository { Requests = [new("S1", "Q1", "HOME", "ABC", "<Message><Policy><Vehicle></Policy></Message>", "")] };

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True);
            Assert.That(summary.UpdatedRequests, Is.EqualTo(1));
            Assert.That(repository.Requests.Single().XmlRequest, Is.EqualTo(PolicyXml("Q1", "HOME", "ABC")));
        });
    }

    [TestCase("<Request>")]
    [TestCase("<!DOCTYPE Request [<!ENTITY external SYSTEM 'file:///outside'>]><Request>&external;</Request>")]
    public async Task Malformed_xml_and_external_dtds_should_be_rejected_before_database_access(string xml)
    {
        var requests = WriteCsv("requests.csv", RequestHeaders, [["S1", xml]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task Duplicate_scenario_ids_should_be_rejected_case_insensitively()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S1", PolicyXml("Q1", "HOME", "ABC")], ["s1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Issues.Single().Reason, Does.Contain("Duplicate"));
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [Test]
    public async Task An_invalid_row_later_in_the_file_should_prevent_all_database_writes()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")], ["S1", "<Message><Policy><Vehicle></Policy></Message>"]]);
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.Issues.Single().ScenarioId, Is.EqualTo("S1"));
            Assert.That(repository.ReadCount, Is.Zero);
            Assert.That(repository.WriteCount, Is.Zero);
        });
    }

    [Test]
    public async Task A_failed_batch_should_report_previous_commits_without_exposing_exception_payloads()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            [["S0", PolicyXml("Q0", "HOME", "ABC")], ["S1", PolicyXml("Q1", "HOME", "ABC")]]);
        var repository = new FakeRepository { FailOnBatch = 2 };
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(summary.InsertedRequests, Is.EqualTo(1));
            Assert.That(summary.CommittedBatches, Is.EqualTo(1));
            Assert.That(summary.Issues.Single().Reason, Does.Not.Contain("sensitive-payload"));
        });
    }

    [Test]
    public async Task Cancellation_should_stop_the_loader_without_database_writes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new FakeRepository();
        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(RequestFile(), false), cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Cancelled, Is.True);
            Assert.That(summary.Succeeded, Is.False);
            Assert.That(repository.Batches, Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(1001)]
    public void Invalid_batch_sizes_should_be_rejected(int batchSize)
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new ScenarioDataLoader().RunAsync(
            new ImportOptions(RequestFile(), true, batchSize)));
    }

    [Test]
    public async Task Loader_should_process_20000_scenarios_in_bounded_batches()
    {
        var requests = WriteCsv("requests.csv", RequestHeaders,
            Enumerable.Range(0, 20000).Select(index => new[] { $"S{index:D5}", PolicyXml($"Q{index:D5}", "HOME", "ABC") }));
        var repository = new FakeRepository();
        var elapsed = global::System.Diagnostics.Stopwatch.StartNew();

        var summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(requests, false, 500));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Succeeded, Is.True, string.Join("; ", summary.Issues.Select(issue => issue.Reason)));
            Assert.That(summary.InsertedRequests, Is.EqualTo(20000));
            Assert.That(summary.CommittedBatches, Is.EqualTo(40));
            Assert.That(repository.ReadCount, Is.EqualTo(40));
            Assert.That(repository.Batches.All(batch => batch.Count <= 500), Is.True);
        });
        TestContext.Out.WriteLine($"Validated and planned 20000 scenarios in {elapsed.Elapsed.TotalSeconds:F2} seconds using a fake repository; no live SQL timing is included.");
    }

    private static readonly string[] RequestHeaders = ["scenario_id", "xml"];

    // Same /Message/Policy layout as the real request XMLs (e.g. CV-Request-updated.xml).
    private static string PolicyXml(string quoteRef, string productCode, string schemeCode, string extra = "") =>
        $"<Message><Policy><PolicyReference>{quoteRef}</PolicyReference><ProductCode>{productCode}</ProductCode>" +
        $"<SchemeCode>{schemeCode}</SchemeCode>{extra}</Policy></Message>";

    private string RequestFile() => WriteCsv("requests.csv", RequestHeaders, [["S1", PolicyXml("Q1", "HOME", "ABC")]]);

    private string WriteCsv(string name, string[] headers, IEnumerable<string[]> rows)
    {
        var path = Path.Combine(directory, name);
        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var field in headers)
        {
            csv.WriteField(field);
        }
        csv.NextRecord();
        foreach (var row in rows)
        {
            foreach (var field in row)
            {
                csv.WriteField(field);
            }
            csv.NextRecord();
        }
        return path;
    }

    private sealed class FakeRepository : IScenarioImportRepository
    {
        private readonly List<ScenarioRequestImport> storedRequests = [];
        public IReadOnlyList<ScenarioRequestImport> Requests
        {
            get => storedRequests;
            init => storedRequests.AddRange(value);
        }
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public List<IReadOnlyList<ScenarioRequestImport>> Batches { get; } = [];
        public int FailOnBatch { get; init; }

        public Task<ScenarioImportSnapshot> ReadExistingAsync(IReadOnlyCollection<string> scenarioIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(new ScenarioImportSnapshot(Requests.Where(row => scenarioIds.Contains(row.ScenarioId, StringComparer.OrdinalIgnoreCase)).ToArray()));
        }

        public Task<ScenarioImportBatchResult> ApplyBatchAsync(
            IReadOnlyCollection<ScenarioRequestImport> inserts,
            IReadOnlyCollection<ScenarioRequestImport> updates,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteCount + 1 == FailOnBatch)
            {
                throw new InvalidOperationException("sensitive-payload");
            }
            foreach (var update in updates)
            {
                var index = storedRequests.FindIndex(row => string.Equals(row.ScenarioId, update.ScenarioId, StringComparison.OrdinalIgnoreCase));
                storedRequests[index] = storedRequests[index] with { XmlRequest = update.XmlRequest };
            }
            if (inserts.Count > 0)
            {
                Batches.Add(inserts.ToArray());
                storedRequests.AddRange(inserts);
            }
            WriteCount++;
            return Task.FromResult(new ScenarioImportBatchResult(inserts.Count, updates.Count));
        }
    }
}
```

### `PricingValidationFramework.Tests\System\DatabaseQueryContractTests.cs`

Replace the entire contents with:

```csharp
using System.Reflection;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class DatabaseQueryContractTests
{
    [Test]
    public void Connection_factory_should_create_a_closed_connection()
    {
        using var connection = new SqlConnectionFactory(new DatabaseSettings()).Create();

        Assert.That(connection.State, Is.EqualTo(global::System.Data.ConnectionState.Closed));
    }

    [Test]
    public void Connection_factory_should_fail_to_open_without_a_connection_string()
    {
        var factory = new SqlConnectionFactory(new DatabaseSettings());

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var connection = await factory.OpenAsync();
        });
    }

    [TestCase("AllScenariosSql")]
    [TestCase("ProductScenariosSql")]
    [TestCase("ScenarioByIdSql")]
    [TestCase("ScenariosWithoutBaselineSql")]
    [TestCase("FilteredScenariosSql")]
    public void Request_queries_should_use_the_current_table_columns(string queryName)
    {
        var sql = ReadQuery<RequestDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Request]"));
            Assert.That(sql, Does.Contain("Scheme_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("row_creationdate AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Seed_query_should_select_only_requests_with_no_response_row_of_any_status()
    {
        var sql = ReadQuery<RequestDataReader>("ScenariosWithoutBaselineSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS (SELECT 1 FROM [Xml].[Response] AS response WHERE response.Scenario_id = request.Scenario_id)"));
            Assert.That(sql, Does.Not.Contain("Status"));
            Assert.That(sql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
            Assert.That(sql, Does.Not.Match(@"\b(INSERT|UPDATE|DELETE|CREATE)\s"));
        });
    }

    [Test]
    public void Request_filters_should_remain_parameterized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadQuery<RequestDataReader>("ProductScenariosSql"), Does.Contain("Product_code = @ProductCode"));
            Assert.That(ReadQuery<RequestDataReader>("ScenarioByIdSql"), Does.Contain("Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Radar_product_filter_should_preserve_all_schemes_and_tags_as_metadata()
    {
        var productSql = ReadQuery<RequestDataReader>("ProductScenariosSql");
        var allSql = ReadQuery<RequestDataReader>("AllScenariosSql");

        Assert.Multiple(() =>
        {
            Assert.That(productSql, Does.Contain("WHERE Product_code = @ProductCode ORDER BY row_creationdate, Scenario_id;"));
            Assert.That(productSql, Does.Contain("Test_tags AS TestTags"));
            Assert.That(productSql, Does.Not.Contain("Test_tags ="));
            Assert.That(productSql, Does.Not.Contain("@TestTag"));
            Assert.That(productSql, Does.Not.Contain("Scheme_code ="));
            Assert.That(productSql, Does.Not.Contain("ROW_NUMBER"));
            Assert.That(productSql, Does.Not.Contain("[Xml].[Response]"));
            Assert.That(allSql, Does.Not.Contain("WHERE"));
            Assert.That(allSql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
        });
    }

    [TestCase("PassingBaselinesSql")]
    [TestCase("FilteredPassingBaselinesSql")]
    [TestCase("PassingBaselinesByIdSql")]
    [TestCase("BaselineByIdSql")]
    public void Baseline_queries_should_join_request_identifiers(string queryName)
    {
        var sql = ReadQuery<BaselineDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Response] AS response"));
            Assert.That(sql, Does.Contain("INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id"));
            Assert.That(sql, Does.Contain("request.Quote_ref AS QuoteRef"));
            Assert.That(sql, Does.Contain("response.XML_Response AS XmlResponse"));
            Assert.That(sql, Does.Not.Contain("response.Quote_ref"));
            Assert.That(sql, Does.Not.Contain("response.Product_code"));
            Assert.That(sql, Does.Not.Contain("response.Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Ice_selection_should_keep_the_latest_passing_baseline_per_request_product_and_scheme()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselinesSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("response.Status = 'PASS'"));
            Assert.That(sql, Does.Contain("request.Product_code AS ProductCode"));
            Assert.That(sql, Does.Contain("request.Scheme_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("PARTITION BY request.Product_code, request.Scheme_code"));
            Assert.That(sql, Does.Contain("ORDER BY response.Last_updated DESC, response.Scenario_id DESC"));
            Assert.That(sql, Does.Contain("WHERE RowNumber = 1"));
        });
    }

    [Test]
    public void Ice_filtered_selection_should_be_the_same_query_with_optional_filters_inside_the_ranking()
    {
        var all = ReadQuery<BaselineDataReader>("PassingBaselinesSql");
        var filtered = ReadQuery<BaselineDataReader>("FilteredPassingBaselinesSql");
        var filters = "\n\t\t\tAND (@ProductCode IS NULL OR request.Product_code = @ProductCode)" +
            "\n\t\t\tAND (@ScenarioTag IS NULL OR CHARINDEX(',' + @ScenarioTag + ',', ',' + REPLACE(REPLACE(ISNULL(request.Test_tags, ''), ';', ','), ' ', '') + ',') > 0)";

        Assert.Multiple(() =>
        {
            Assert.That(filtered, Does.Contain("WHERE response.Status = 'PASS'" + filters));
            Assert.That(filtered.IndexOf("@ScenarioTag", StringComparison.Ordinal), Is.LessThan(filtered.IndexOf("WHERE RowNumber = 1", StringComparison.Ordinal)));
            Assert.That(filtered.Replace(filters, string.Empty, StringComparison.Ordinal), Is.EqualTo(all));
            Assert.That(all, Does.Not.Contain("@ProductCode"));
        });
    }

    [Test]
    public void Ice_selection_by_scenario_id_should_take_exactly_those_passing_baselines_without_ranking()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselinesByIdSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE response.Status = 'PASS' AND response.Scenario_id IN @ScenarioIds;"));
            Assert.That(sql, Does.Not.Contain("ROW_NUMBER"));
            Assert.That(BaselineDataReader.MaxScenarioIdsPerQuery, Is.LessThanOrEqualTo(2000), "IN expands to one parameter per ID");
        });
    }

    [Test]
    public void Radar_filters_should_be_optional_parameterized_and_match_one_whole_tag()
    {
        var sql = ReadQuery<RequestDataReader>("FilteredScenariosSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE (@ProductCode IS NULL OR Product_code = @ProductCode)"));
            Assert.That(sql, Does.Contain("AND (@ScenarioTag IS NULL OR " + RequestDataReader.ScenarioTagMatch + ")"));
            Assert.That(RequestDataReader.ScenarioTagMatch, Does.Contain("CHARINDEX(',' + @ScenarioTag + ','"), "comma-delimited, so 'smoke' never matches 'smoke2'");
            Assert.That(sql, Does.Not.Contain("LIKE"), "'_' in a tag must not be a wildcard");
            Assert.That(sql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
        });
    }

    [Test]
    public void Radar_baseline_selection_should_map_audit_fields_and_filter_by_scenario_id()
    {
        var sql = ReadQuery<BaselineDataReader>("BaselineByIdSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("response.Status AS Status"));
            Assert.That(sql, Does.Not.Contain("Status = 'PASS'"));
            Assert.That(sql, Does.Contain("response.row_creationdate AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Contain("response.Last_updated AS LastUpdated"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Contain("response.Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Passing_result_update_should_preserve_creation_date_and_update_status_response_and_timestamp()
    {
        var sql = ReadQuery<ResultUpdater>("UpdatePassingResultSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE [Xml].[Response]"));
            Assert.That(sql, Does.Contain("Status = @Status"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("XML_Response = @XmlResponse"));
            Assert.That(sql, Does.Contain("Last_updated = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("row_creationdate"), "an update never changes who or when the row was created");
            Assert.That(sql, Does.Not.Contain("row_createdby"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Quote_ref"));
            Assert.That(sql, Does.Not.Contain("Product_code"));
            Assert.That(sql, Does.Not.Contain("Scheme_code"));
        });
    }

    [Test]
    public void Failed_result_update_should_change_only_the_status()
    {
        var sql = ReadQuery<ResultUpdater>("UpdateFailedStatusSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE [Xml].[Response]"));
            Assert.That(sql, Does.Contain("SET Status = @Status"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("XML_Response"));
            Assert.That(sql, Does.Not.Contain("Last_updated"));
            Assert.That(sql, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("row_creationdate"));
        });
    }

    [Test]
    public void Baseline_insert_should_never_replace_an_existing_baseline()
    {
        var sql = ReadQuery<ResultUpdater>("InsertBaselineSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("(Scenario_id, XML_Response, Status, Last_updated,"));
            Assert.That(sql, Does.Contain("row_creationdate, row_createdby, row_modifieddate, row_modifiedby)"));
            Assert.That(sql, Does.Contain("COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("SELECT @ScenarioId, @XmlResponse, @Status, SYSUTCDATETIME(),"));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"));
            Assert.That(sql, Does.Contain("WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Not.Contain("UPDATE"));
        });
    }

    [Test]
    public void Import_insert_should_use_parameterised_values_rows_guarded_against_existing_ids()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(2);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("INSERT INTO [Xml].[Request] (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags, row_creationdate, row_createdby, row_modifieddate, row_modifiedby)"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME()), SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("(@s0, @q0, @p0, @c0, @x0, @t0)"));
            Assert.That(sql, Does.Contain("(@s1, @q1, @p1, @c1, @x1, @t1)"));
            Assert.That(sql, Does.Contain("AS v (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags)"));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"));
            Assert.That(sql, Does.Contain("[Xml].[Request] AS target WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Contain("WHERE target.Scenario_id = v.Scenario_id"));
        });
    }

    [Test]
    public void Import_insert_should_need_only_insert_and_select_on_existing_tables()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(ScenarioImportRepository.MaxRowsPerStatement);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("#"), "no temporary tables");
            Assert.That(sql, Does.Not.Match(@"\bCREATE\s").IgnoreCase, "creates nothing (Create_date is only a column)");
            Assert.That(sql, Does.Not.Contain("MERGE").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("OPENJSON").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("[Xml].[Response]"));
            Assert.That(sql, Does.Not.Contain("UPDATE").IgnoreCase);
        });
    }

    [Test]
    public void Import_insert_should_stay_within_sql_servers_parameter_limit()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(ScenarioImportRepository.MaxRowsPerStatement);
        var parameters = global::System.Text.RegularExpressions.Regex.Matches(sql, @"@[a-z]\d+")
            .Select(match => match.Value)
            .Distinct()
            .Count();

        Assert.Multiple(() =>
        {
            Assert.That(ScenarioImportRepository.MaxRowsPerStatement, Is.EqualTo(333));
            Assert.That(parameters, Is.EqualTo(ScenarioImportRepository.MaxRowsPerStatement * ScenarioImportRepository.ParametersPerRow));
            Assert.That(parameters, Is.LessThanOrEqualTo(2100));
        });
    }

    [TestCase(0)]
    [TestCase(334)]
    public void Import_insert_should_reject_statement_sizes_outside_the_limit(int rowCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioImportRepository.BuildInsertSql(rowCount));
    }

    [Test]
    public void Import_updates_should_use_values_and_preserve_identity_tags_creation_and_baselines()
    {
        var sql = ScenarioImportRepository.BuildUpdateSql(2);
        var assignments = sql.Split("SET", StringSplitOptions.None)[1].Split("FROM", StringSplitOptions.None)[0];

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Request] AS target WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Contain("(@s1, @q1, @p1, @c1, @x1, @t1)"));
            Assert.That(sql, Does.Contain("ON target.Scenario_id = v.Scenario_id"));
            Assert.That(sql, Does.Not.Contain("target.Product_code"), "no product/scheme condition: the XML alone decides");
            Assert.That(sql, Does.Not.Contain("target.Scheme_code"));
            Assert.That(sql, Does.Not.Contain("target.Quote_ref"));
            Assert.That(assignments, Does.Contain("XML_request = v.XML_request"));
            Assert.That(assignments, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(assignments, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(assignments, Does.Not.Contain("row_creationdate"));
            Assert.That(assignments, Does.Not.Contain("row_createdby"));
            Assert.That(assignments, Does.Not.Contain("Quote_ref"), "the update changes the XML only");
            Assert.That(assignments, Does.Not.Contain("Test_tags"));
            Assert.That(assignments, Does.Not.Contain("Product_code"));
            Assert.That(assignments, Does.Not.Contain("Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("[Xml].[Response]"));
            Assert.That(sql, Does.Not.Contain("#"));
            Assert.That(sql, Does.Not.Match(@"\bCREATE\s").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("MERGE").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("OPENJSON").IgnoreCase);
        });
    }

    [TestCase("testuser", "testuser")]
    [TestCase("  testuser  ", "testuser")]
    [TestCase("", null)]
    [TestCase("   ", null)]
    public void Audit_user_should_be_trimmed_or_left_null_for_the_sql_login(string configured, string? expected)
    {
        var factory = new SqlConnectionFactory(new DatabaseSettings { AuditUser = configured });
        ScenarioRequestImport[] rows = [new("S1", "Q1", "HOME", "ABC", "<Message />", "")];
        var parameters = ScenarioImportRepository.BuildParameters(rows, factory.AuditUser);

        Assert.Multiple(() =>
        {
            Assert.That(factory.AuditUser, Is.EqualTo(expected));
            Assert.That(parameters.ParameterNames, Does.Contain("AuditUser"));
            Assert.That(parameters.Get<string?>("AuditUser"), Is.EqualTo(expected));
        });
    }

    [Test]
    public void Import_parameters_should_bind_large_xml_as_varchar_max()
    {
        var xml = "<Message>" + new string('x', 46000) + "</Message>";
        var parameters = ScenarioImportRepository.BuildParameters([new("S1", "Q1", "HOME", "ABC", xml, "")]);
        using var connection = new SqlConnectionFactory(new DatabaseSettings()).Create();
        using var command = connection.CreateCommand();
        var identity = (Dapper.SqlMapper.Identity)Activator.CreateInstance(
            typeof(Dapper.SqlMapper.Identity), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ScenarioImportRepository.BuildUpdateSql(1), global::System.Data.CommandType.Text, connection, null, parameters.GetType()], null)!;
        ((Dapper.SqlMapper.IDynamicParameters)parameters).AddParameters(command, identity);
        var parameter = command.Parameters["x0"];

        Assert.Multiple(() =>
        {
            Assert.That(parameter.SqlDbType, Is.EqualTo(global::System.Data.SqlDbType.VarChar));
            Assert.That(parameter.Size, Is.EqualTo(-1));
            Assert.That(parameter.Value, Is.EqualTo(xml));
        });
    }

    [Test]
    public void Import_update_should_stay_within_sql_servers_parameter_limit()
    {
        var sql = ScenarioImportRepository.BuildUpdateSql(ScenarioImportRepository.MaxRowsPerStatement);
        var parameters = global::System.Text.RegularExpressions.Regex.Matches(sql, @"@[a-z]\d+")
            .Select(match => match.Value).Distinct().Count();

        Assert.That(parameters, Is.EqualTo(1998));
    }

    [TestCase(0)]
    [TestCase(334)]
    public void Import_update_should_reject_statement_sizes_outside_the_limit(int rowCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioImportRepository.BuildUpdateSql(rowCount));
    }

    private static string ReadQuery<TReader>(string fieldName)
    {
        var field = typeof(TReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetRawConstantValue() as string
            ?? throw new AssertionException($"SQL constant '{typeof(TReader).Name}.{fieldName}' was not found.");
    }
}
```

### `PricingValidationFramework.Tests\System\Radar\RadarExecutionInfrastructureTests.cs`

Replace the entire contents with:

```csharp
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
		using var collection = new RadarReportCollection();
		Parallel.ForEach(new[] { "SCENARIO-3", "SCENARIO-1", "SCENARIO-2" }, scenarioId =>
			collection.Add(CreateRow(scenarioId)));

		Assert.Multiple(() =>
		{
			Assert.That(collection.Count, Is.EqualTo(3));
			Assert.That(collection.ReadSortedRows().Select(row => row.ScenarioId),
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
			Assert.That(await File.ReadAllTextAsync(reportPath), Does.Contain("SCENARIO-1,"));
		}
		finally
		{
			File.Delete(reportPath);
		}
	}

	[Test]
	public void Report_collection_should_snapshot_results_and_reject_access_after_disposal()
	{
		using var collection = new RadarReportCollection();
		var row = CreateRow("SCENARIO-1");
		row.FieldComparisons = [new("/Pricing/Amount", "/Pricing/Amount", "/Pricing/Amount", 10m, 10.01m, 0.01m, ScenarioResult.Pass)];
		collection.Add(row);
		row.FieldComparisons = [];

		Assert.That(collection.ReadSortedRows().Single().FieldComparisons.Single().Delta, Is.EqualTo(0.01m));
		collection.Dispose();
		Assert.Throws<ObjectDisposedException>(() => collection.Add(row));
		Assert.Throws<ObjectDisposedException>(() => collection.ReadSortedRows().ToArray());
	}

	[Test]
	public async Task Report_collection_should_stream_20000_scenarios_as_flat_field_rows()
	{
		var buildId = $"volume-{Guid.NewGuid():N}";
		var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "Reports", $"Radar_{buildId}.csv");
		using var collection = new RadarReportCollection();
		var xml = $"<PricingResponse><Label>{new string('x', 256)}</Label></PricingResponse>";
		var elapsed = global::System.Diagnostics.Stopwatch.StartNew();
		try
		{
			await Parallel.ForEachAsync(Enumerable.Range(0, 20000), new ParallelOptions { MaxDegreeOfParallelism = 8 },
				(scenarioIndex, _) =>
				{
					collection.Add(new RadarValidationReportRow
					{
						ScenarioId = $"SCENARIO-{scenarioIndex:D5}",
						ProductCode = "HOME",
						SchemeCode = "ABC",
						ProfileId = "Route001",
						RequestXml = xml,
						BaselineXml = xml,
						RadarResponseXml = xml,
						MinThreshold = -0.01m,
						MaxThreshold = 0.01m,
						Result = ScenarioResult.Pass,
						FieldComparisons =
						[
							new("/Pricing/Amount", "/Pricing/Amount", "/Pricing/Amount", scenarioIndex, scenarioIndex, 0m, ScenarioResult.Pass)
						]
						});
					return ValueTask.CompletedTask;
				});

			await RadarReportingHelper.WriteReportAsync(buildId, collection.ReadSortedRows(), CancellationToken.None);
			var fieldCount = 0;
			foreach (var line in File.ReadLines(reportPath).Skip(1))
			{
				if (!line.StartsWith($"SCENARIO-{fieldCount:D5},", StringComparison.Ordinal))
				{
					throw new InvalidOperationException("The streamed report is not sorted by ScenarioId.");
				}
				if (!line.Contains($",{fieldCount},{fieldCount},0,-0.01,0.01,PASS,PASS,", StringComparison.Ordinal))
				{
					throw new InvalidOperationException("The streamed report contains an incorrect decimal field result.");
				}
				Assert.That(line, Does.EndWith($",{xml},{xml},{xml}"));
				fieldCount++;
			}

			Assert.Multiple(() =>
			{
				Assert.That(collection.Count, Is.EqualTo(20000));
				Assert.That(fieldCount, Is.EqualTo(20000));
			});
			TestContext.Out.WriteLine($"Spooled and streamed {fieldCount} scenarios in {elapsed.Elapsed.TotalSeconds:F2} seconds.");
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
		using var collection = new RadarReportCollection();
		collection.Add(CreateRow("SCENARIO-1"));

		try
		{
			Assert.That(async () =>
				await RadarReportingHelper.WriteReportAsync(buildId, collection.ReadSortedRows(), CancellationToken.None),
				Throws.InstanceOf<Exception>());
			Assert.That(collection.Count, Is.EqualTo(1));
			Assert.That(Directory.GetFiles(reportDirectory, $".Radar_{buildId}.csv.*.tmp"), Is.Empty);
		}
		finally
		{
			Directory.Delete(finalPath);
		}
	}

	[TestCase(0)]
	[TestCase(-1)]
	public void Rate_limiter_settings_should_reject_non_positive_values(int requestsPerSecond)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new RadarRateLimitSettings
		{
			RequestsPerSecond = requestsPerSecond
		}.Validate());
	}

	[Test]
	public void Rate_limiter_settings_should_default_to_confirmed_values()
	{
		var settings = new RadarRateLimitSettings();

		Assert.Multiple(() =>
		{
			Assert.That(settings.RequestsPerSecond, Is.EqualTo(2));
			Assert.That(typeof(RadarRateLimitSettings).GetProperty("Enabled"), Is.Null);
		});
	}

	[Test]
	public void Retry_settings_should_accept_zero_retries_delays_and_retry_after_cap()
	{
		var settings = new RetrySettings
		{
			DatabaseRetryCount = 0,
			DatabaseRetryDelaySeconds = 0,
			ApiRetryCount = 0,
			ApiRetryDelaySeconds = 0,
			ApiRetryAfterMaxDelaySeconds = 0
		};

		Assert.DoesNotThrow(settings.ValidateRadarPipelineSettings);
	}

	[Test]
	public void Retry_settings_should_accept_defaults()
	{
		Assert.DoesNotThrow(() => new RetrySettings().ValidateRadarPipelineSettings());
	}

	[TestCase(-1, 0, 0, 0)]
	[TestCase(0, -1, 0, 0)]
	[TestCase(0, 0, -1, 0)]
	[TestCase(0, 0, 0, -1)]
	public void Retry_settings_should_reject_negative_counts_and_delays(
		int databaseRetryCount,
		int databaseDelay,
		int apiRetryCount,
		int apiDelay)
	{
		var settings = new RetrySettings
		{
			DatabaseRetryCount = databaseRetryCount,
			DatabaseRetryDelaySeconds = databaseDelay,
			ApiRetryCount = apiRetryCount,
			ApiRetryDelaySeconds = apiDelay
		};

		Assert.Throws<ArgumentOutOfRangeException>(settings.ValidateRadarPipelineSettings);
	}

	[Test]
	public void Retry_settings_should_reject_delay_calculations_beyond_task_delay_range()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new RetrySettings
		{
			ApiRetryCount = 32,
			ApiRetryDelaySeconds = 1
		}.ValidateRadarPipelineSettings());

		Assert.Throws<ArgumentOutOfRangeException>(() => new RetrySettings
		{
			ApiRetryAfterMaxDelaySeconds = int.MaxValue
		}.ValidateRadarPipelineSettings());
	}

	[Test]
	public void Generic_api_retry_validation_should_not_require_a_radar_retry_after_cap()
	{
		Assert.DoesNotThrow(() => new RetrySettings
		{
			ApiRetryAfterMaxDelaySeconds = -1
		}.ValidateApiRetrySettings());
	}

	[Test]
	public async Task Rate_limiter_should_cancel_waiting_requests_without_dispatching_or_rejecting_them()
	{
		using var limiter = new RadarRequestRateLimiter(
			new RadarRateLimitSettings(),
			["PricingA", "PricingB", "PricingC"]);
		var dispatchCount = 0;
		Task<HttpResponseMessage> SendResponse()
		{
			dispatchCount++;
			return Task.FromResult(new HttpResponseMessage());
		}
		using var firstResponse = await limiter.SendAsync("PricingA", SendResponse);

		using var cancellationTokenSource = new CancellationTokenSource();
		var queuedRequests = Enumerable.Range(0, 8)
			.Select(_ => limiter.SendAsync("pricingb", SendResponse, cancellationTokenSource.Token))
			.ToArray();

		cancellationTokenSource.Cancel();
		Assert.That(async () => await Task.WhenAll(queuedRequests), Throws.InstanceOf<OperationCanceledException>());
		Assert.That(dispatchCount, Is.EqualTo(1));
		using var nextResponse = await limiter.SendAsync("PricingC", SendResponse);
		Assert.That(dispatchCount, Is.EqualTo(2));
	}

	[TestCase(2)]
	[TestCase(4)]
	public async Task Rate_limiter_should_stagger_requests_across_endpoints(int requestsPerSecond)
	{
		using var limiter = new RadarRequestRateLimiter(
			new RadarRateLimitSettings { RequestsPerSecond = requestsPerSecond },
			["PricingA", "PricingB"]);
		var starts = new List<long>();
		var requests = new[] { "PricingA", "PricingB", "pricinga", "PricingB" }
			.Select(endpoint => limiter.SendAsync(endpoint, () =>
			{
				starts.Add(global::System.Diagnostics.Stopwatch.GetTimestamp());
				return Task.FromResult(new HttpResponseMessage());
			}));
		var responses = await Task.WhenAll(requests);
		foreach (var response in responses)
		{
			response.Dispose();
		}

		Assert.That(starts, Has.Count.EqualTo(4));
		for (var index = 1; index < starts.Count; index++)
		{
			Assert.That(global::System.Diagnostics.Stopwatch.GetElapsedTime(starts[index - 1], starts[index]),
				Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1d / requestsPerSecond)));
		}
	}

	[Test]
	public async Task Rate_limiter_should_release_the_gate_before_the_response_completes()
	{
		using var limiter = new RadarRequestRateLimiter(new RadarRateLimitSettings(), ["PricingA", "PricingB"]);
		var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstRequest = limiter.SendAsync("PricingA", () => pendingResponse.Task);
		try
		{
			using var secondResponse = await limiter.SendAsync("PricingB", () => Task.FromResult(new HttpResponseMessage()))
				.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.That(firstRequest.IsCompleted, Is.False);
		}
		finally
		{
			pendingResponse.SetResult(new HttpResponseMessage());
			using var firstResponse = await firstRequest;
		}
	}

	[Test]
	public async Task Rate_limiter_should_not_catch_up_with_a_burst_after_an_idle_gap()
	{
		using var limiter = new RadarRequestRateLimiter(
			new RadarRateLimitSettings { RequestsPerSecond = 10 }, ["PricingA", "PricingB"]);
		var starts = new List<long>();
		Task<HttpResponseMessage> SendResponse()
		{
			starts.Add(global::System.Diagnostics.Stopwatch.GetTimestamp());
			return Task.FromResult(new HttpResponseMessage());
		}

		using var firstResponse = await limiter.SendAsync("PricingA", SendResponse);
		await Task.Delay(250);
		var responses = await Task.WhenAll(
			limiter.SendAsync("PricingA", SendResponse),
			limiter.SendAsync("PricingB", SendResponse));
		foreach (var response in responses)
		{
			response.Dispose();
		}

		Assert.That(starts, Has.Count.EqualTo(3));
		Assert.That(global::System.Diagnostics.Stopwatch.GetElapsedTime(starts[1], starts[2]),
			Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100)));
	}

	[Test]
	public async Task Rate_limiter_should_release_and_pace_the_gate_after_a_synchronous_send_failure()
	{
		using var limiter = new RadarRequestRateLimiter(
			new RadarRateLimitSettings { RequestsPerSecond = 10 }, ["PricingA"]);
		var failedDispatch = 0L;
		Assert.ThrowsAsync<HttpRequestException>(async () => await limiter.SendAsync("PricingA", () =>
		{
			failedDispatch = global::System.Diagnostics.Stopwatch.GetTimestamp();
			throw new HttpRequestException("Simulated dispatch failure.");
		}));

		var nextDispatch = 0L;
		using var response = await limiter.SendAsync("PricingA", () =>
		{
			nextDispatch = global::System.Diagnostics.Stopwatch.GetTimestamp();
			return Task.FromResult(new HttpResponseMessage());
		});
		Assert.That(global::System.Diagnostics.Stopwatch.GetElapsedTime(failedDispatch, nextDispatch),
			Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100)));
	}

	[Test]
	public void Limiter_registry_should_reject_unknown_and_duplicate_logical_endpoints()
	{
		using var limiter = new RadarRequestRateLimiter(new RadarRateLimitSettings(), ["PricingA"]);

		Assert.Multiple(() =>
		{
			Assert.ThrowsAsync<RadarRequestRateLimitException>(async () =>
				await limiter.SendAsync("PricingB", () => Task.FromResult(new HttpResponseMessage())));
			Assert.Throws<ArgumentException>(() => new RadarRequestRateLimiter(
				new RadarRateLimitSettings(), ["PricingA", "pricinga"]));
		});
	}

	[TestCase("RadarRateLimitSettings__RequestsPerSecond", "0")]
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
	public void Radar_setup_should_allow_a_route_to_an_unconfigured_endpoint_and_continue_to_discovery()
	{
		const string variableName = "PricingProfiles__MappingFile";
		var originalValue = Environment.GetEnvironmentVariable(variableName);
		var sourcePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssets", "Radar", "Fixtures", "mappings.json");
		var mappings = PricingProfileConfiguration.Load(sourcePath);
		mappings.Profiles[0].Radar.EndpointName = "PricingUnknown";
		mappings.Profiles[0].PathsFile = Path.Combine(Path.GetDirectoryName(sourcePath)!, "paths.json");
		var mappingPath = Path.Combine(Path.GetTempPath(), $"unknown-endpoint-{Guid.NewGuid():N}.json");
		File.WriteAllText(mappingPath, global::System.Text.Json.JsonSerializer.Serialize(mappings));
		Environment.SetEnvironmentVariable(variableName, mappingPath);

		try
		{
			// Such scenarios become EndpointResolution ERRORs at run time; setup only fails later, at the
			// (unconfigured) database.
			var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
				await RadarTestSetup.DiscoverScenariosAsync());
			Assert.That(exception!.Message, Does.Not.Contain("Every Radar route"));
		}
		finally
		{
			Environment.SetEnvironmentVariable(variableName, originalValue);
			File.Delete(mappingPath);
		}
	}

	[Test]
	public void Radar_setup_should_reject_case_insensitive_duplicate_endpoints_before_database_discovery()
	{
		var radarSettings = new RadarSettings
		{
			Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
			{
				["PricingA"] = new()
				{
					BaseUrl = "https://radar.example.test",
					ApiKeyHeaderName = "X-API-KEY",
					ApiKeyValue = "test-value"
				},
				["pricinga"] = new()
				{
					BaseUrl = "https://radar.example.test",
					ApiKeyHeaderName = "X-API-KEY",
					ApiKeyValue = "test-value"
				}
			}
		};

		Assert.That(
			() => RadarSettingsValidator.Validate(radarSettings, new RadarRateLimitSettings()),
			Throws.InvalidOperationException.With.Message.Contains("unique ignoring case"));
	}

	[Test]
	public void Radar_configuration_should_accept_complete_endpoint_and_route_settings()
	{
		Assert.DoesNotThrow(() => RadarSettingsValidator.Validate(
			CreateValidRadarSettings(),
			new RadarRateLimitSettings()));
	}

	[TestCase("url")]
	[TestCase("header")]
	[TestCase("credential")]
	[TestCase("product")]
	[TestCase("scheme")]
	[TestCase("endpoint")]
	[TestCase("routeKey")]
	public void Radar_configuration_should_reject_missing_endpoint_or_route_values_without_secrets(string field)
	{
		const string secret = "test-secret-that-must-not-appear";
		var settings = CreateValidRadarSettings();
		settings.Endpoints["PricingA"].ApiKeyValue = secret;

		switch (field)
		{
			case "url":
				settings.Endpoints["PricingA"].BaseUrl = string.Empty;
				break;
			case "header":
				settings.Endpoints["PricingA"].ApiKeyHeaderName = string.Empty;
				break;
			case "credential":
				settings.Endpoints["PricingA"].ApiKeyValue = string.Empty;
				break;
			case "product":
				settings.Routes["Route001"].ProductCode = string.Empty;
				break;
			case "scheme":
				settings.Routes["Route001"].SchemeCodes.Clear();
				break;
			case "endpoint":
				settings.Routes["Route001"].EndpointName = string.Empty;
				break;
			case "routeKey":
				settings.Routes["Route001"].RouteKey = string.Empty;
				break;
		}

		var exception = Assert.Throws<InvalidOperationException>(() =>
			RadarSettingsValidator.Validate(settings, new RadarRateLimitSettings()));
		Assert.That(exception!.Message, Does.Not.Contain(secret));
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

	private static RadarSettings CreateValidRadarSettings()
	{
		return new RadarSettings
		{
			Endpoints = new Dictionary<string, RadarEndpointSettings>(StringComparer.Ordinal)
			{
				["PricingA"] = new()
				{
					BaseUrl = "https://radar.example.test/quote",
					ApiKeyHeaderName = "X-API-KEY",
					ApiKeyValue = "test-value"
				}
			},
			Routes = new Dictionary<string, RadarRouteSettings>(StringComparer.Ordinal)
			{
				["Route001"] = new()
				{
					ProductCode = "HOME",
					SchemeCodes = new() { "ABC" },
					EndpointName = "PricingA",
					RouteKey = "home-abc"
				}
			},
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
```

### `azure-pipelines-data-loader.yml`

Replace the entire contents with:

```yaml
trigger: none
pr: none

parameters:
  - name: agentPool
    type: string
    default: Default
  - name: importEnvironment
    type: string
    default: pricing-data-import
  - name: datasetSource
    type: string
    default: repository
    values:
      - repository
      - artifact
  - name: datasetPath
    type: string
    default: PricingValidationFramework/data/import/examples
  - name: datasetPipelineId
    type: string
    default: ''
  - name: datasetRunId
    type: string
    default: ''
  - name: batchSize
    type: number
    default: 100
  - name: requestDateTime
    displayName: Radar request date/time for seeded baselines (UTC, yyyy-MM-ddZHH:mm:ss, or now)
    type: string
    default: now
  - name: seedBaselines
    displayName: Seed Radar baselines for scenarios without one after the import
    type: boolean
    default: true
  - name: seedOnly
    displayName: Seed baselines only - skip Validate and Import (seeds scenarios already in the database)
    type: boolean
    default: false
  - name: seedMaxParallel
    displayName: Scenarios seeded at once (1-16; Radar's rate limit still applies)
    type: number
    default: 4

pool:
  name: ${{ parameters.agentPool }}

stages:
  - ${{ if eq(parameters.seedOnly, false) }}:
      - stage: Validate
        displayName: Validate importer and CSV dataset
        jobs:
          - job: ValidateDataset
            timeoutInMinutes: 30
            workspace:
              clean: all
            steps:
              - checkout: self
              - task: UseDotNet@2
                displayName: Use .NET 10
                inputs:
                  packageType: sdk
                  version: 10.0.x
              - pwsh: |
                  dotnet restore PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --disable-parallel
                  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                  dotnet test PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DataLoaderTests|FullyQualifiedName~DatabaseQueryContractTests|FullyQualifiedName~SeedCommandTests|FullyQualifiedName~BaselineSeederTests" --logger "trx;LogFileName=DataLoader.trx" --results-directory "$(Agent.TempDirectory)/DataLoaderTests"
                  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                displayName: Test loader and repository contracts
              - ${{ if eq(parameters.datasetSource, 'repository') }}:
                  - task: CopyFiles@2
                    displayName: Stage repository dataset
                    inputs:
                      SourceFolder: '$(Build.SourcesDirectory)/${{ parameters.datasetPath }}'
                      Contents: |
                        requests.csv
                        scenarios-*.csv
                      TargetFolder: '$(Pipeline.Workspace)/scenario-data'
                      CleanTargetFolder: true
              - ${{ if eq(parameters.datasetSource, 'artifact') }}:
                  - task: DownloadPipelineArtifact@2
                    displayName: Download selected dataset version
                    inputs:
                      buildType: specific
                      project: '$(System.TeamProject)'
                      definition: '${{ parameters.datasetPipelineId }}'
                      buildVersionToDownload: specific
                      pipelineId: '${{ parameters.datasetRunId }}'
                      artifactName: scenario-data
                      targetPath: '$(Pipeline.Workspace)/scenario-data'
              - pwsh: |
                  $reportDirectory = '$(Build.ArtifactStagingDirectory)/DataLoaderValidation'
                  New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
                  $importArguments = @('--requests', "$(Pipeline.Workspace)/scenario-data", '--batch-size', '${{ parameters.batchSize }}', '--request-time', '${{ parameters.requestDateTime }}', '--report', "$reportDirectory/validation-summary.json")
                  dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader --configuration Release --no-build -- validate @importArguments
                  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                  # Resolved once here ("now" = this moment's UTC hour) so the baseline stage uses the same time.
                  $requestTime = (Get-Content "$reportDirectory/validation-summary.json" -Raw | ConvertFrom-Json).RequestTime
                  Write-Host "Radar request time for new baselines: $requestTime"
                  Write-Host "##vso[task.setvariable variable=requestTime;isOutput=true]$requestTime"
                name: validateDataset
                displayName: Validate CSV and XML without database writes
              - task: PublishTestResults@2
                condition: succeededOrFailed()
                inputs:
                  testResultsFormat: VSTest
                  testResultsFiles: '$(Agent.TempDirectory)/DataLoaderTests/DataLoader.trx'
                  testRunTitle: Data loader
                  failTaskOnMissingResultsFile: true
              - task: PublishPipelineArtifact@1
                condition: succeeded()
                displayName: Pin validated dataset for the import stage
                inputs:
                  targetPath: '$(Pipeline.Workspace)/scenario-data'
                  artifact: scenario-import-data
              - task: PublishPipelineArtifact@1
                condition: succeededOrFailed()
                displayName: Publish validation summary
                inputs:
                  targetPath: '$(Build.ArtifactStagingDirectory)/DataLoaderValidation'
                  artifact: DataLoaderValidation

      - stage: Import
        displayName: Import validated dataset
        dependsOn: Validate
        lockBehavior: sequential
        jobs:
          - deployment: ImportDataset
            timeoutInMinutes: 120
            environment: ${{ parameters.importEnvironment }}
            workspace:
              clean: all
            strategy:
              runOnce:
                deploy:
                  steps:
                    - download: none
                    - checkout: self
                    - task: UseDotNet@2
                      inputs:
                        packageType: sdk
                        version: 10.0.x
                    - download: current
                      artifact: scenario-import-data
                    - pwsh: |
                        $reportDirectory = '$(Build.ArtifactStagingDirectory)/DataLoaderImport'
                        New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
                        $importArguments = @('--requests', "$(Pipeline.Workspace)/scenario-import-data", '--batch-size', '${{ parameters.batchSize }}', '--report', "$reportDirectory/import-summary.json")
                        dotnet restore PricingValidationFramework/PricingValidationFramework.DataLoader --disable-parallel
                        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                        dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader --configuration Release --no-restore -- import @importArguments
                        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                      displayName: Import through Core repository
                      env:
                        DatabaseSettings__ConnectionString: $(DataLoaderDatabaseConnectionString)
                    - task: PublishPipelineArtifact@1
                      condition: succeededOrFailed()
                      displayName: Publish import summary
                      inputs:
                        targetPath: '$(Build.ArtifactStagingDirectory)/DataLoaderImport'
                        artifact: DataLoaderImport

  - ${{ if or(eq(parameters.seedBaselines, true), eq(parameters.seedOnly, true)) }}:
      - stage: SeedBaselines
        displayName: Seed Radar baselines for scenarios without one
        ${{ if eq(parameters.seedOnly, true) }}:
          # Seed only: no import, so seed resolves requestDateTime itself ("now" = the current UTC hour).
          dependsOn: []
          variables:
            requestTime: ${{ parameters.requestDateTime }}
        ${{ else }}:
          # After an import: use the time Validate resolved, so the whole run shares one hour.
          dependsOn:
            - Validate
            - Import
          variables:
            requestTime: $[ stageDependencies.Validate.ValidateDataset.outputs['validateDataset.requestTime'] ]
        jobs:
          - job: SeedBaselines
            timeoutInMinutes: 360
            workspace:
              clean: all
            steps:
              - checkout: self
              - task: UseDotNet@2
                displayName: Use .NET 10
                inputs:
                  packageType: sdk
                  version: 10.0.x
              - pwsh: |
                  # Creates the first [Xml].[Response] baseline for every scenario that has none, at the
                  # request time Validate resolved. Scenarios that already have one are not touched.
                  Write-Host "Seeding baselines with Radar request time $(requestTime)"
                  dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader --configuration Release -- seed --request-time '$(requestTime)' --max-parallel ${{ parameters.seedMaxParallel }} --report '$(Build.ArtifactStagingDirectory)/SeedSummary/seed-summary.json'
                  exit $LASTEXITCODE
                displayName: Seed baselines
                env:
                  DatabaseSettings__ConnectionString: $(RadarDatabaseConnectionString)
                  RadarSettings__Endpoints__Endpoint1__BaseUrl: $(RadarEndpoint1Url)
                  RadarSettings__Endpoints__Endpoint1__ApiKeyHeaderName: $(RadarEndpoint1KeyHeader)
                  RadarSettings__Endpoints__Endpoint1__ApiKeyValue: $(RadarEndpoint1ApiKey)
                  RadarSettings__Endpoints__Endpoint2__BaseUrl: $(RadarEndpoint2Url)
                  RadarSettings__Endpoints__Endpoint2__ApiKeyHeaderName: $(RadarEndpoint2KeyHeader)
                  RadarSettings__Endpoints__Endpoint2__ApiKeyValue: $(RadarEndpoint2ApiKey)
                  RadarSettings__Endpoints__Endpoint3__BaseUrl: $(RadarEndpoint3Url)
                  RadarSettings__Endpoints__Endpoint3__ApiKeyHeaderName: $(RadarEndpoint3KeyHeader)
                  RadarSettings__Endpoints__Endpoint3__ApiKeyValue: $(RadarEndpoint3ApiKey)

              - task: PublishPipelineArtifact@1
                condition: always()
                displayName: Publish seed summary
                inputs:
                  targetPath: '$(Build.ArtifactStagingDirectory)/SeedSummary'
                  artifact: SeedSummary
```

### `azure-pipelines-ice.yml`

Replace the entire contents with:

```yaml
trigger: none
pr: none

parameters:
  - name: testTag
    displayName: Product_code to check (empty for every product), as TestTag in the Radar pipeline
    type: string
    default: ''
  - name: scenarioTag
    displayName: Scenario tag filter - one tag from Test_tags, e.g. smoke (empty for all)
    type: string
    default: ''

pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    displayName: Use .NET 10
    inputs:
      packageType: sdk
      version: 10.0.x

  - pwsh: |
      dotnet test PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Ice.IceValidationTests" --logger "trx;LogFileName=Ice.trx" --results-directory "$(Agent.TempDirectory)/IceTestResults" -- NUnit.ExplicitMode=Relaxed
      if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
      [xml]$results = Get-Content '$(Agent.TempDirectory)/IceTestResults/Ice.trx'
      if ([int]$results.TestRun.ResultSummary.Counters.executed -eq 0) { throw 'No ICE integration tests executed.' }
    displayName: Run ICE integration tests only
    env:
      DOTNET_ENVIRONMENT: Uat
      DatabaseSettings__ConnectionString: $(IceDatabaseConnectionString)
      IceSettings__IceEndpoint: $(IceEndpoint)
      IceSettings__ApiKeyHeaderName: $(IceApiKeyHeaderName)
      IceSettings__ApiKeyHeaderValue: $(IceApiKeyHeaderValue)
      IceSettings__PfxCertificateBase64: $(IcePfxCertificateBase64)
      IceSettings__CertificatePassword: $(IceCertificatePassword)
      TEST_TAG: ${{ parameters.testTag }}
      SCENARIO_TAG: ${{ parameters.scenarioTag }}

  - task: PublishTestResults@2
    condition: always()
    displayName: Publish ICE test results
    inputs:
      testResultsFormat: VSTest
      testResultsFiles: '$(Agent.TempDirectory)/IceTestResults/Ice.trx'
      testRunTitle: ICE integration
      failTaskOnMissingResultsFile: true

  - pwsh: |
      $resultDirectory = '$(Build.SourcesDirectory)/PricingValidationFramework/PricingValidationFramework.Tests/bin/Release/net10.0/TestResults'
      $artifactDirectory = '$(Build.ArtifactStagingDirectory)/IceReports'
      New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
      $missing = @()
      foreach ($output in @(@{ Folder = 'Reports'; Pattern = 'Ice_*.csv' }, @{ Folder = 'Logs'; Pattern = 'Ice_*.log' })) {
        $source = Join-Path $resultDirectory $output.Folder
        $target = Join-Path $artifactDirectory $output.Folder
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $files = @(if (Test-Path $source) { Get-ChildItem $source -Filter $output.Pattern -File })
        if ($files.Count -eq 0) { $missing += $output.Folder } else { $files | Copy-Item -Destination $target }
      }
      if ($missing.Count -gt 0) { throw "Missing ICE outputs: $($missing -join ', '). Available outputs have been staged." }
    condition: always()
    displayName: Stage ICE reports and logs

  - task: PublishPipelineArtifact@1
    condition: always()
    displayName: Publish ICE reports and logs
    inputs:
      targetPath: '$(Build.ArtifactStagingDirectory)/IceReports'
      artifact: IceReports
```

### `azure-pipelines-radar.yml`

Replace the entire contents with:

```yaml
trigger: none
pr: none

parameters:
  - name: scenarioTag
    displayName: Scenario tag filter - one tag from Test_tags, e.g. smoke (empty for all)
    type: string
    default: ''
  - name: runIce
    displayName: Run ICE after Radar
    type: boolean
    default: true
  - name: iceSelection
    displayName: Which baselines ICE checks
    type: string
    default: radarRun
    values:
      - radarRun
      - newestPerProductScheme

pool:
  vmImage: ubuntu-latest

# Radar selects scenarios with the TestTag variable (TEST_TAG: an exact Product_code, empty for all)
# and the scenarioTag parameter (SCENARIO_TAG: one tag from Test_tags, empty for all).
# ICE then checks, by iceSelection:
#   radarRun               - exactly the scenarios that passed in this run's Radar report (default)
#   newestPerProductScheme - the newest PASS baseline per product/scheme within the same filters
# Both reports share the run's BUILD_BUILDID.
stages:
  - stage: Radar
    displayName: Radar validation
    jobs:
      - job: RadarValidation
        timeoutInMinutes: 360
        steps:
          - task: UseDotNet@2
            displayName: Use .NET 10
            inputs:
              packageType: sdk
              version: 10.0.x

          - pwsh: |
              dotnet test PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Radar.RadarValidationTests" --logger "trx;LogFileName=Radar.trx" --results-directory "$(Agent.TempDirectory)/RadarTestResults" -- NUnit.ExplicitMode=Relaxed
              if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
              [xml]$results = Get-Content '$(Agent.TempDirectory)/RadarTestResults/Radar.trx'
              if ([int]$results.TestRun.ResultSummary.Counters.executed -eq 0) { throw 'No Radar integration tests executed.' }
            displayName: Run Radar integration tests only
            env:
              DOTNET_ENVIRONMENT: Uat
              DatabaseSettings__ConnectionString: $(RadarDatabaseConnectionString)
              RadarSettings__Endpoints__Endpoint1__BaseUrl: $(RadarEndpoint1Url)
              RadarSettings__Endpoints__Endpoint1__ApiKeyHeaderName: $(RadarEndpoint1KeyHeader)
              RadarSettings__Endpoints__Endpoint1__ApiKeyValue: $(RadarEndpoint1ApiKey)
              RadarSettings__Endpoints__Endpoint2__BaseUrl: $(RadarEndpoint2Url)
              RadarSettings__Endpoints__Endpoint2__ApiKeyHeaderName: $(RadarEndpoint2KeyHeader)
              RadarSettings__Endpoints__Endpoint2__ApiKeyValue: $(RadarEndpoint2ApiKey)
              RadarSettings__Endpoints__Endpoint3__BaseUrl: $(RadarEndpoint3Url)
              RadarSettings__Endpoints__Endpoint3__ApiKeyHeaderName: $(RadarEndpoint3KeyHeader)
              RadarSettings__Endpoints__Endpoint3__ApiKeyValue: $(RadarEndpoint3ApiKey)
              RADAR_MIN_THRESHOLD: $(RadarMinThreshold)
              RADAR_MAX_THRESHOLD: $(RadarMaxThreshold)
              TEST_TAG: $(TestTag)
              SCENARIO_TAG: ${{ parameters.scenarioTag }}
              RADAR_REQUEST_DATETIME: $(RadarRequestDateTime)

          - task: PublishTestResults@2
            condition: always()
            displayName: Publish Radar test results
            inputs:
              testResultsFormat: VSTest
              testResultsFiles: '$(Agent.TempDirectory)/RadarTestResults/Radar.trx'
              testRunTitle: Radar integration
              failTaskOnMissingResultsFile: true

          - pwsh: |
              $resultDirectory = '$(Build.SourcesDirectory)/PricingValidationFramework/PricingValidationFramework.Tests/bin/Release/net10.0/TestResults'
              $artifactDirectory = '$(Build.ArtifactStagingDirectory)/RadarReports'
              New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
              $missing = @()
              foreach ($output in @(@{ Folder = 'Reports'; Pattern = 'Radar_*.csv' }, @{ Folder = 'Logs'; Pattern = 'Radar_*.log' })) {
                $source = Join-Path $resultDirectory $output.Folder
                $target = Join-Path $artifactDirectory $output.Folder
                New-Item -ItemType Directory -Force -Path $target | Out-Null
                $files = @(if (Test-Path $source) { Get-ChildItem $source -Filter $output.Pattern -File })
                if ($files.Count -eq 0) { $missing += $output.Folder } else { $files | Copy-Item -Destination $target }
              }
              if ($missing.Count -gt 0) { throw "Missing Radar outputs: $($missing -join ', '). Available outputs have been staged." }
            condition: always()
            displayName: Stage Radar reports and logs

          - task: PublishPipelineArtifact@1
            condition: always()
            displayName: Publish Radar reports and logs
            inputs:
              targetPath: '$(Build.ArtifactStagingDirectory)/RadarReports'
              artifact: RadarReports

  - ${{ if eq(parameters.runIce, true) }}:
      - stage: Ice
        displayName: ICE validation
        dependsOn: Radar
        # Runs when Radar passed or reported FAIL/ERROR scenarios (ICE reads only PASS baselines);
        # skipped if the run is cancelled. With radarRun it needs this run's RadarReports artifact.
        condition: succeededOrFailed()
        jobs:
          - job: IceValidation
            timeoutInMinutes: 120
            steps:
              - task: UseDotNet@2
                displayName: Use .NET 10
                inputs:
                  packageType: sdk
                  version: 10.0.x

              - ${{ if eq(parameters.iceSelection, 'radarRun') }}:
                  - download: current
                    artifact: RadarReports
                    displayName: Download this run's Radar report

              - pwsh: |
                  dotnet test PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~PricingValidationFramework.Tests.Integration.Ice.IceValidationTests" --logger "trx;LogFileName=Ice.trx" --results-directory "$(Agent.TempDirectory)/IceTestResults" -- NUnit.ExplicitMode=Relaxed
                  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                  [xml]$results = Get-Content '$(Agent.TempDirectory)/IceTestResults/Ice.trx'
                  if ([int]$results.TestRun.ResultSummary.Counters.executed -eq 0) { throw 'No ICE integration tests executed.' }
                displayName: Run ICE integration tests only
                env:
                  DOTNET_ENVIRONMENT: Uat
                  DatabaseSettings__ConnectionString: $(IceDatabaseConnectionString)
                  IceSettings__IceEndpoint: $(IceEndpoint)
                  IceSettings__ApiKeyHeaderName: $(IceApiKeyHeaderName)
                  IceSettings__ApiKeyHeaderValue: $(IceApiKeyHeaderValue)
                  IceSettings__PfxCertificateBase64: $(IcePfxCertificateBase64)
                  IceSettings__CertificatePassword: $(IceCertificatePassword)
                  TEST_TAG: $(TestTag)
                  SCENARIO_TAG: ${{ parameters.scenarioTag }}
                  ${{ if eq(parameters.iceSelection, 'radarRun') }}:
                    ICE_RADAR_REPORT: $(Pipeline.Workspace)/RadarReports/Reports/Radar_$(Build.BuildId).csv

              - task: PublishTestResults@2
                condition: always()
                displayName: Publish ICE test results
                inputs:
                  testResultsFormat: VSTest
                  testResultsFiles: '$(Agent.TempDirectory)/IceTestResults/Ice.trx'
                  testRunTitle: ICE integration
                  failTaskOnMissingResultsFile: true

              - pwsh: |
                  $resultDirectory = '$(Build.SourcesDirectory)/PricingValidationFramework/PricingValidationFramework.Tests/bin/Release/net10.0/TestResults'
                  $artifactDirectory = '$(Build.ArtifactStagingDirectory)/IceReports'
                  New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
                  $missing = @()
                  foreach ($output in @(@{ Folder = 'Reports'; Pattern = 'Ice_*.csv' }, @{ Folder = 'Logs'; Pattern = 'Ice_*.log' })) {
                    $source = Join-Path $resultDirectory $output.Folder
                    $target = Join-Path $artifactDirectory $output.Folder
                    New-Item -ItemType Directory -Force -Path $target | Out-Null
                    $files = @(if (Test-Path $source) { Get-ChildItem $source -Filter $output.Pattern -File })
                    if ($files.Count -eq 0) { $missing += $output.Folder } else { $files | Copy-Item -Destination $target }
                  }
                  if ($missing.Count -gt 0) { throw "Missing ICE outputs: $($missing -join ', '). Available outputs have been staged." }
                condition: always()
                displayName: Stage ICE reports and logs

              - task: PublishPipelineArtifact@1
                condition: always()
                displayName: Publish ICE reports and logs
                inputs:
                  targetPath: '$(Build.ArtifactStagingDirectory)/IceReports'
                  artifact: IceReports
```

## 4. Find/replace edits

### `ARCHITECTURE.md`

#### From commit 2dd63e1

**Edit 1: find** (line 17):

```text
│   ├── RadarSettings.cs
```

**Replace with:**

```text
│   ├── RadarSettings.cs
│   ├── RadarSettingsValidator.cs
```

**Edit 2: find** (line 61):

```text
│   └── CsvReportWriter.cs
```

**Replace with:**

```text
│   └── CsvReportWriter.cs
├── Seeding/
│   └── BaselineSeeder.cs
```

**Edit 3: find** (line 64):

```text
    ├── PricingComparisonService.cs
```

**Replace with:**

```text
    ├── PricingComparisonService.cs
    ├── ScenarioTags.cs
```

**Edit 4: find** (line 165):

```text
`ResultUpdater.InsertBaselineAsync()` inserts a new scenario's first Radar response as its `PASS` baseline, guarded so it never replaces an existing row. `ResultUpdater.UpdateResultAsync()` updates `[Xml].[Response]` by unique `Scenario_id`, preserving `row_creationdate`/`row_createdby` and requiring exactly one affected row. On PASS it updates `Status`, `Last_updated` and `XML_Response`, so the Radar response becomes the new baseline. On FAIL it updates `Status` only, so the existing baseline XML is never overwritten. Both also set `row_modifieddate` and `row_modifiedby`.
```

**Replace with:**

```text
`ResultUpdater.TryInsertBaselineAsync()` (used by `DataLoader seed`) inserts a new scenario's first Radar response as its `PASS` baseline, guarded with `NOT EXISTS ... WITH (UPDLOCK, HOLDLOCK)` so it never replaces an existing row; it returns false when a row already exists. `InsertBaselineAsync()` is the throwing form. `RequestDataReader.GetScenariosWithoutBaselineAsync()` selects the `[Xml].[Request]` rows with no `[Xml].[Response]` row of any status. `ResultUpdater.UpdateResultAsync()` updates `[Xml].[Response]` by unique `Scenario_id`, preserving `row_creationdate`/`row_createdby` and requiring exactly one affected row. On PASS it updates `Status`, `Last_updated` and `XML_Response`, so the Radar response becomes the new baseline. On FAIL it updates `Status` only, so the existing baseline XML is never overwritten. Both also set `row_modifieddate` and `row_modifiedby`.
```

**Edit 5: find** (line 314):

```text
- `TestTag`: `PipelineSettings:TestTag` or the `TEST_TAG` pipeline override; the value identifies `[Xml].[Request].Product_code`.
```

**Replace with:**

```text
- `TestTag`: `PipelineSettings:TestTag` or the `TEST_TAG` pipeline override; the value identifies `[Xml].[Request].Product_code`.
- `ScenarioTag`: `PipelineSettings:ScenarioTag` or the `SCENARIO_TAG` pipeline override; one tag (letters, digits, `_`, `-`, `.`) matched as a whole entry of the comma-separated `[Xml].[Request].Test_tags`.
```

**Edit 6: find** (line 318):

```text
- `BaselineOnly`: `PipelineSettings:BaselineOnly` or the `RADAR_BASELINE_ONLY` pipeline override (`true`/`false`, default false). When true, scenarios with an existing `[Xml].[Response]` row are skipped and only missing baselines are created.
```

**Delete those line(s)** (including the line breaks, so no blank line is left).

**Edit 7: find** (line 330):

```text
- `TestTag` selects all request scenarios with the supplied product code when supplied, regardless of their scheme or stored `Test_tags`.
- `RequestTime` must match `yyyy-MM-ddZHH:mm:ss` when supplied. The `Z` is a literal separator in this business format, not a UTC offset suffix.
```

**Replace with:**

```text
- `TestTag` selects all request scenarios with the supplied product code when supplied, regardless of their scheme. `ScenarioTag`, when supplied, further limits them to scenarios carrying that tag.
- `RequestTime` must match `yyyy-MM-ddZHH:mm:ss` or be `now` when supplied. The `Z` is a literal separator in this business format, not a UTC offset suffix.
```

**Edit 8: find** (line 405):

```text
Pricing comparison and XPath extraction live in Core. NUnit loads the baseline, invokes the service, persists eligible PASS/FAIL results and asserts the outcome. There is no whole-document schema validation. Initial-baseline creation still stores the API response without extraction or comparison.
```

**Replace with:**

```text
Pricing comparison and XPath extraction live in Core. NUnit loads the baseline, invokes the service, persists eligible PASS/FAIL results and asserts the outcome. There is no whole-document schema validation. Radar never creates baselines: a missing `[Xml].[Response]` row is an ERROR (`MissingBaseline`). Initial baselines come from `Core/Seeding/BaselineSeeder` (run by `DataLoader seed`), which stores the API response without extraction or comparison.
```

**Edit 9: find** (line 785):

```text
Baseline extraction fails before the Radar call. Response extraction errors occur after the call. Both are technical ERROR results: log the field/path diagnostics, report the scenario, do not update the baseline, and continue with other cases. Initial-baseline creation deliberately bypasses extraction.
```

**Replace with:**

```text
Baseline extraction fails before the Radar call. Response extraction errors occur after the call. Both are technical ERROR results: log the field/path diagnostics, report the scenario, do not update the baseline, and continue with other cases. Baseline seeding (`DataLoader seed`) deliberately bypasses extraction.
```

**Edit 10: find** (line 956):

```text
ICE uses one named `TestCaseSource` case per selected baseline and remains explicitly `[NonParallelizable]`. Discovery loads only database/retry configuration and passing baseline data, without creating the API client or loading its certificate. Empty discovery, duplicate IDs, and discovery exceptions produce a failing discovery case. One-time setup creates the shared execution resources; each scenario records PASS/FAIL/ERROR before its independent assertion, and subsequent cases continue after a failed case. Teardown uses a non-cancelled reporting token to write one combined report, including completed rows after cancellation, and disposes resources in `finally`. Baseline selection, exact comparison, authentication, and retries are unchanged; ICE tag filtering remains pending.
```

**Replace with:**

```text
ICE uses one named `TestCaseSource` case per selected baseline and remains explicitly `[NonParallelizable]`. Discovery loads only database/retry configuration and passing baseline data, without creating the API client or loading its certificate. Empty discovery, duplicate IDs, and discovery exceptions produce a failing discovery case. One-time setup creates the shared execution resources; each scenario records PASS/FAIL/ERROR before its independent assertion, and subsequent cases continue after a failed case. Teardown uses a non-cancelled reporting token to write one combined report, including completed rows after cancellation, and disposes resources in `finally`. Baseline selection, exact comparison, authentication, and retries are unchanged. ICE shares Radar's filters, the `TEST_TAG` product and the `SCENARIO_TAG` tag (`TestConfigurationLoader.ReadTestTag`/`ReadScenarioTag`, `BaselineDataReader.GetFilteredPassingBaselineScenariosAsync`; the tag rule is Core `ScenarioTags`, shared with the loader). With `ICE_RADAR_REPORT`, `IceTestSetup` instead reads the PASS scenario IDs from that `Radar_<BuildId>.csv` (`RadarReportScenarios`) and loads exactly those baselines (`GetPassingBaselinesByScenarioIdsAsync`, in chunks of 1,000 IDs), which the Radar pipeline's ICE stage does by default. The live Radar and ICE fixtures (only) also read `data/import/local-settings.json` in the Development/local environment (`TestConfigurationLoader.Load(includeLocalSettings: true)`).
```

**Edit 11: find** (line 961):

```text
- `TestTag` and `RequestTime` are optional pipeline inputs for Radar only; `TestTag` retains its user-facing name but filters `[Xml].[Request].Product_code`.
```

**Replace with:**

```text
- `TestTag`, `ScenarioTag` and `RequestTime` are optional pipeline inputs; `TestTag` retains its user-facing name but filters `[Xml].[Request].Product_code`, and `ScenarioTag` filters `Test_tags`. Radar and ICE share both filters.
```

#### If you already applied the seed changes

**Edit 1: find** (line 67):

```text
    ├── PricingComparisonService.cs
```

**Replace with:**

```text
    ├── PricingComparisonService.cs
    ├── ScenarioTags.cs
```

**Edit 2: find** (line 317):

```text
- `TestTag`: `PipelineSettings:TestTag` or the `TEST_TAG` pipeline override; the value identifies `[Xml].[Request].Product_code`.
```

**Replace with:**

```text
- `TestTag`: `PipelineSettings:TestTag` or the `TEST_TAG` pipeline override; the value identifies `[Xml].[Request].Product_code`.
- `ScenarioTag`: `PipelineSettings:ScenarioTag` or the `SCENARIO_TAG` pipeline override; one tag (letters, digits, `_`, `-`, `.`) matched as a whole entry of the comma-separated `[Xml].[Request].Test_tags`.
```

**Edit 3: find** (line 332):

```text
- `TestTag` selects all request scenarios with the supplied product code when supplied, regardless of their scheme or stored `Test_tags`.
- `RequestTime` must match `yyyy-MM-ddZHH:mm:ss` when supplied. The `Z` is a literal separator in this business format, not a UTC offset suffix.
```

**Replace with:**

```text
- `TestTag` selects all request scenarios with the supplied product code when supplied, regardless of their scheme. `ScenarioTag`, when supplied, further limits them to scenarios carrying that tag.
- `RequestTime` must match `yyyy-MM-ddZHH:mm:ss` or be `now` when supplied. The `Z` is a literal separator in this business format, not a UTC offset suffix.
```

**Edit 4: find** (line 958):

```text
ICE uses one named `TestCaseSource` case per selected baseline and remains explicitly `[NonParallelizable]`. Discovery loads only database/retry configuration and passing baseline data, without creating the API client or loading its certificate. Empty discovery, duplicate IDs, and discovery exceptions produce a failing discovery case. One-time setup creates the shared execution resources; each scenario records PASS/FAIL/ERROR before its independent assertion, and subsequent cases continue after a failed case. Teardown uses a non-cancelled reporting token to write one combined report, including completed rows after cancellation, and disposes resources in `finally`. Baseline selection, exact comparison, authentication, and retries are unchanged; ICE tag filtering remains pending.
```

**Replace with:**

```text
ICE uses one named `TestCaseSource` case per selected baseline and remains explicitly `[NonParallelizable]`. Discovery loads only database/retry configuration and passing baseline data, without creating the API client or loading its certificate. Empty discovery, duplicate IDs, and discovery exceptions produce a failing discovery case. One-time setup creates the shared execution resources; each scenario records PASS/FAIL/ERROR before its independent assertion, and subsequent cases continue after a failed case. Teardown uses a non-cancelled reporting token to write one combined report, including completed rows after cancellation, and disposes resources in `finally`. Baseline selection, exact comparison, authentication, and retries are unchanged. ICE shares Radar's filters, the `TEST_TAG` product and the `SCENARIO_TAG` tag (`TestConfigurationLoader.ReadTestTag`/`ReadScenarioTag`, `BaselineDataReader.GetFilteredPassingBaselineScenariosAsync`; the tag rule is Core `ScenarioTags`, shared with the loader). With `ICE_RADAR_REPORT`, `IceTestSetup` instead reads the PASS scenario IDs from that `Radar_<BuildId>.csv` (`RadarReportScenarios`) and loads exactly those baselines (`GetPassingBaselinesByScenarioIdsAsync`, in chunks of 1,000 IDs), which the Radar pipeline's ICE stage does by default. The live Radar and ICE fixtures (only) also read `data/import/local-settings.json` in the Development/local environment (`TestConfigurationLoader.Load(includeLocalSettings: true)`).
```

**Edit 5: find** (line 963):

```text
- `TestTag` and `RequestTime` are optional pipeline inputs for Radar only; `TestTag` retains its user-facing name but filters `[Xml].[Request].Product_code`.
```

**Replace with:**

```text
- `TestTag`, `ScenarioTag` and `RequestTime` are optional pipeline inputs; `TestTag` retains its user-facing name but filters `[Xml].[Request].Product_code`, and `ScenarioTag` filters `Test_tags`. Radar and ICE share both filters.
```

### `README.md`

#### From commit 2dd63e1

**Edit 1: find** (line 13):

```text
The explicit Radar workload creates one NUnit test case per selected `[Xml].[Request]` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its `[Xml].[Response]` row. If there is none, it calls Radar and stores the initial baseline with `Status = PASS` without XPath extraction or comparison. A later consumer reports an extraction error if required values are absent. An existing non-PASS baseline is an ERROR. Otherwise `RadarPricingService` compares the baseline with the API response. PASS updates the status, timestamp and baseline XML; FAIL updates only status. Extraction/technical errors do not update the baseline. All comparisons are reported in one consolidated CSV.
```

**Replace with:**

```text
The explicit Radar workload creates one NUnit test case per selected `[Xml].[Request]` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its `[Xml].[Response]` row. Radar compares only: a scenario with no row is an ERROR (stage `MissingBaseline`), because first baselines are created by `DataLoader seed` (see below), not by Radar. An existing non-PASS baseline is also an ERROR. Otherwise `RadarPricingService` compares the baseline with the API response. PASS updates the status, timestamp and baseline XML; FAIL updates only status. Extraction/technical errors do not update the baseline. All comparisons are reported in one consolidated CSV.
```

**Edit 2: find** (line 79):

```text
Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.
```

**Replace with:**

```text
Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. For local runs, the Radar and ICE fixtures also read `data/import/local-settings.json`, the same file DataLoader import and seed use (copy `data/import/examples/local-settings.example.json`, which sets exact-match thresholds of 0). Only the live fixtures read it, never the unit and system tests, and only in the Development/local environment; set `PVF_LOCAL_SETTINGS` to use a file elsewhere. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.
```

**Edit 3: find** (line 81):

```text
The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged. Stored `[Xml].[Request].Test_tags` values remain scenario metadata and NUnit categories, not workload-selection criteria. Previously supplied tag values must be replaced with actual product codes.
```

**Replace with:**

```text
The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged and still filter on product. A second, separate filter selects by tag: `SCENARIO_TAG` (or `PipelineSettings:ScenarioTag`) is one tag, such as `smoke`, matched against the comma-separated `[Xml].[Request].Test_tags` (a whole tag, so `smoke` never matches `smoke2`). Both filters are optional and combine: product and tag. Tags reach `Test_tags` through the optional `test_tags` column of the requests CSV, set when a scenario is first inserted (see the DataLoader README). They are also NUnit categories.
```

**Edit 4: find** (line 83):

```text
Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:
```

**Replace with:**

```text
Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, `SCENARIO_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `RADAR_REQUEST_DATETIME` accepts `now` as well as `yyyy-MM-ddZHH:mm:ss`. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:
```

**Edit 5: find** (line 95):

```text
`RADAR_BASELINE_ONLY` (or `PipelineSettings:BaselineOnly`) set to `true` makes a run create baselines only: scenarios that already have an `[Xml].[Response]` row are skipped without calling Radar. It defaults to false. The data-loader pipeline's Create baselines stage uses it.
```

**Replace with:**

```text
Radar no longer creates baselines and has no baseline-only mode. Run `DataLoader seed` (or the data-loader pipeline's Seed baselines stage) before Radar for any newly loaded scenarios.

Only the Radar endpoints in use need values. An endpoint whose URL, header and key are all empty is ignored, and a scenario whose route names an endpoint that isn't configured is an `EndpointResolution` ERROR while the rest of the run continues. A partly filled endpoint still fails setup.

To debug the Radar or ICE tests in VS Code, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process it prints.
```

**Edit 6: find** (line 99):

```text
Register two separate YAML pipelines using `PricingValidationFramework/azure-pipelines-radar.yml` and `PricingValidationFramework/azure-pipelines-ice.yml` from the repository root. Both are manual-run only. Each selects its own explicit integration fixture, publishes NUnit TRX results to the Tests tab, and publishes its CSV report as a separate `RadarReports` or `IceReports` pipeline artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.
```

**Replace with:**

```text
Register `PricingValidationFramework/azure-pipelines-radar.yml` from the repository root. It is manual-run only and has two stages. **Radar** runs `RadarValidationTests`; **ICE** (when the `runIce` parameter is true, the default) then runs `IceValidationTests`. Radar selects scenarios with the `TestTag` variable (product) and the `scenarioTag` parameter (tag). The `iceSelection` parameter chooses what ICE checks: `radarRun` (the default) takes exactly the scenarios that passed in this run's Radar report, downloaded from the `RadarReports` artifact and passed as `ICE_RADAR_REPORT`; `newestPerProductScheme` takes the newest PASS baseline per product/scheme within the same product and tag filters. ICE runs after Radar whether or not Radar reported FAIL or ERROR scenarios, because a Radar FAIL never changes a PASS baseline; it is skipped only if the run is cancelled. Both reports share the run's `BUILD_BUILDID` (`Radar_<id>.csv`, `Ice_<id>.csv`). `PricingValidationFramework/azure-pipelines-ice.yml` remains for ICE-only runs, with `testTag` and `scenarioTag` parameters for the same filters (newest per product/scheme). Each stage publishes NUnit TRX results to the Tests tab and its CSV report and logs as a `RadarReports` or `IceReports` artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.
```

**Edit 7: find** (line 109):

```text
`PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `[Xml].[Request]` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `[Xml].[Request]`. New IDs are inserted. An existing ID is compared on its XML only: identical (normalized) XML is skipped and any other change updates `XML_request` alone; quote reference, product, scheme, tags and creation date are not compared or changed. The default batch size is 100 and SQL statements are limited to 333 rows. The loader doesn't call Radar or write baselines itself. The data-loader pipeline's optional **Create baselines** stage runs Radar in baseline-only mode (`RADAR_BASELINE_ONLY=true`) after the import, at the pipeline's `requestDateTime` (default `now`), so new scenarios get baselines and existing ones are left untouched.
```

**Replace with:**

```text
`PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `[Xml].[Request]` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `[Xml].[Request]`. New IDs are inserted. An existing ID is compared on its XML only: identical (normalized) XML is skipped and any other change updates `XML_request` alone; quote reference, product, scheme, tags and creation date are not compared or changed. The default batch size is 100 and SQL statements are limited to 333 rows. `validate` and `import` don't call Radar or write `[Xml].[Response]`. A separate `seed` command creates the first baseline for every scenario that has no `[Xml].[Response]` row: it calls Radar at the request time (`--request-time`, then `RADAR_REQUEST_DATETIME`, default `now`) and stores each response as a `PASS` baseline without comparing it. Scenarios that already have a baseline, of any status, are never touched. It reads the Radar endpoints and database from a `--settings` JSON and/or the same environment variables as the Radar pipeline, and routes with `config/radar/mappings.json` (routing only, no XPath files). The data-loader pipeline runs it as its **Seed baselines** stage after the import, at the pipeline's `requestDateTime`. See [the DataLoader README](PricingValidationFramework.DataLoader/README.md) for running it locally.
```

**Edit 8: find** (line 125):

```text
The workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair. It calls ICE using this URL shape:
```

**Replace with:**

```text
By default the workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair, limited to the `TEST_TAG` product and the `SCENARIO_TAG` tag when they are set. When `ICE_RADAR_REPORT` (or `PipelineSettings:IceRadarReport`) names a `Radar_<BuildId>.csv`, it instead selects exactly the scenarios whose `ScenarioResult` is `PASS` in that report, ignoring the filters; the Radar pipeline's ICE stage does this by default. It calls ICE using this URL shape:
```

**Edit 9: find** (line 245):

```text
The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection remains the newest passing baseline per product/scheme; test-tag filtering is not enabled for ICE.
```

**Replace with:**

```text
The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection is the newest passing baseline per product/scheme within the `TEST_TAG` product and `SCENARIO_TAG` tag filters, or, with `ICE_RADAR_REPORT`, exactly the scenarios that passed in that Radar report. A missing or unreadable report fails discovery with the file named.
```

#### If you already applied the seed changes

**Edit 1: find** (line 79):

```text
Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.
```

**Replace with:**

```text
Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. For local runs, the Radar and ICE fixtures also read `data/import/local-settings.json`, the same file DataLoader import and seed use (copy `data/import/examples/local-settings.example.json`, which sets exact-match thresholds of 0). Only the live fixtures read it, never the unit and system tests, and only in the Development/local environment; set `PVF_LOCAL_SETTINGS` to use a file elsewhere. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.
```

**Edit 2: find** (line 81):

```text
The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged. Stored `[Xml].[Request].Test_tags` values remain scenario metadata and NUnit categories, not workload-selection criteria. Previously supplied tag values must be replaced with actual product codes.
```

**Replace with:**

```text
The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged and still filter on product. A second, separate filter selects by tag: `SCENARIO_TAG` (or `PipelineSettings:ScenarioTag`) is one tag, such as `smoke`, matched against the comma-separated `[Xml].[Request].Test_tags` (a whole tag, so `smoke` never matches `smoke2`). Both filters are optional and combine: product and tag. Tags reach `Test_tags` through the optional `test_tags` column of the requests CSV, set when a scenario is first inserted (see the DataLoader README). They are also NUnit categories.
```

**Edit 3: find** (line 83):

```text
Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:
```

**Replace with:**

```text
Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, `SCENARIO_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `RADAR_REQUEST_DATETIME` accepts `now` as well as `yyyy-MM-ddZHH:mm:ss`. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:
```

**Edit 4: find** (line 95):

```text
Radar no longer creates baselines and has no baseline-only mode. Run `DataLoader seed` (or the data-loader pipeline's Seed baselines stage) before Radar for any newly loaded scenarios.
```

**Replace with:**

```text
Radar no longer creates baselines and has no baseline-only mode. Run `DataLoader seed` (or the data-loader pipeline's Seed baselines stage) before Radar for any newly loaded scenarios.

Only the Radar endpoints in use need values. An endpoint whose URL, header and key are all empty is ignored, and a scenario whose route names an endpoint that isn't configured is an `EndpointResolution` ERROR while the rest of the run continues. A partly filled endpoint still fails setup.

To debug the Radar or ICE tests in VS Code, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process it prints.
```

**Edit 5: find** (line 99):

```text
Register two separate YAML pipelines using `PricingValidationFramework/azure-pipelines-radar.yml` and `PricingValidationFramework/azure-pipelines-ice.yml` from the repository root. Both are manual-run only. Each selects its own explicit integration fixture, publishes NUnit TRX results to the Tests tab, and publishes its CSV report as a separate `RadarReports` or `IceReports` pipeline artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.
```

**Replace with:**

```text
Register `PricingValidationFramework/azure-pipelines-radar.yml` from the repository root. It is manual-run only and has two stages. **Radar** runs `RadarValidationTests`; **ICE** (when the `runIce` parameter is true, the default) then runs `IceValidationTests`. Radar selects scenarios with the `TestTag` variable (product) and the `scenarioTag` parameter (tag). The `iceSelection` parameter chooses what ICE checks: `radarRun` (the default) takes exactly the scenarios that passed in this run's Radar report, downloaded from the `RadarReports` artifact and passed as `ICE_RADAR_REPORT`; `newestPerProductScheme` takes the newest PASS baseline per product/scheme within the same product and tag filters. ICE runs after Radar whether or not Radar reported FAIL or ERROR scenarios, because a Radar FAIL never changes a PASS baseline; it is skipped only if the run is cancelled. Both reports share the run's `BUILD_BUILDID` (`Radar_<id>.csv`, `Ice_<id>.csv`). `PricingValidationFramework/azure-pipelines-ice.yml` remains for ICE-only runs, with `testTag` and `scenarioTag` parameters for the same filters (newest per product/scheme). Each stage publishes NUnit TRX results to the Tests tab and its CSV report and logs as a `RadarReports` or `IceReports` artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.
```

**Edit 6: find** (line 125):

```text
The workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair. It calls ICE using this URL shape:
```

**Replace with:**

```text
By default the workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair, limited to the `TEST_TAG` product and the `SCENARIO_TAG` tag when they are set. When `ICE_RADAR_REPORT` (or `PipelineSettings:IceRadarReport`) names a `Radar_<BuildId>.csv`, it instead selects exactly the scenarios whose `ScenarioResult` is `PASS` in that report, ignoring the filters; the Radar pipeline's ICE stage does this by default. It calls ICE using this URL shape:
```

**Edit 7: find** (line 245):

```text
The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection remains the newest passing baseline per product/scheme; test-tag filtering is not enabled for ICE.
```

**Replace with:**

```text
The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection is the newest passing baseline per product/scheme within the `TEST_TAG` product and `SCENARIO_TAG` tag filters, or, with `ICE_RADAR_REPORT`, exactly the scenarios that passed in that Radar report. A missing or unreadable report fails discovery with the file named.
```

### `..\.gitignore`

At the repository root, one folder above `PricingValidationFramework`. Skip it if your copy isn't a git clone.

**Edit 1: find** (line 22):

```text
.vs/
```

**Replace with:**

```text
.vs/

# Local settings with connection strings and API keys (never commit these)
**/appsettings.Development.json
**/appsettings.local.json
**/local-settings.json
```

