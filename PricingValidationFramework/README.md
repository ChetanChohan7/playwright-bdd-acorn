# Pricing Validation Framework

## Control panel

`PricingValidationFramework.ControlPanel` is a local web page that runs each step with a button, on this machine, so nobody has to type commands:

| Step | What it runs |
|---|---|
| 1. Choose scenarios CSV | Copies the chosen file into `data/import` as the newest `scenarios-{timestamp}.csv` and checks it offline (`DataLoader validate`) |
| 2. Import | `DataLoader import` of that CSV into `[Xml].[Request]` |
| 3. Seed baselines | `DataLoader seed` into `[Xml].[Response]` |
| 4. Radar compare | The Radar tests, with the threshold type, min/max, product, tag and request time from the form |
| 5. ICE check | The ICE tests: the newest PASS baseline per product/scheme, or exactly the scenarios that passed in the latest Radar run |
| 6. Set status | `DataLoader set-status` to PASS or FAIL, for typed IDs and/or a file of IDs |

To start it, open the `PricingValidationFramework` folder in VS Code and run **Control panel (opens http://localhost:5080)** in Run and Debug, or run `dotnet run --project PricingValidationFramework.ControlPanel` and open http://localhost:5080. Every step uses `data/import/local-settings.json` (copy `data/import/examples/local-settings.example.json`), as the VS Code launch configurations do; a box left empty on the page falls back to that file. The results are the same as running the commands yourself: Radar PASS updates the baseline, and FAIL sets the status.

- One step runs at a time; the page shows its live output, a short result, and links to its summary and report files (`TestResults/ControlPanel` for DataLoader summaries; the Radar and ICE reports are named after the run, e.g. `Radar_local-20261021-093000-000.csv`).
- It listens on `localhost` only and accepts changes only from its own page, so other websites open in the same browser can't trigger a step.
- Settings are in `PricingValidationFramework.ControlPanel/appsettings.json`: the port, the settings file, and the build configuration.
- The steps go through `IStepRunner`. `LocalStepRunner` runs them on this machine; a pipeline runner can implement the same interface later, queuing the Azure DevOps pipelines with the same inputs, without changing the page.

## Run the test suite

```bash
dotnet test PricingValidationFramework.Tests
```

No database, endpoints, or credentials needed - this runs every unit and system test (extraction, matching, URL building, rate limiting, retry/config validation, CSV reporting, etc.). The two live validation flows below (`IceValidationTests`, `RadarValidationTests`) are `[Explicit]` and sit out of a normal run since they need real infrastructure; see their sections for how to opt into each.

## Run the Radar validation flow

The explicit Radar workload creates one NUnit test case per selected `[Xml].[Request]` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its `[Xml].[Response]` row. Radar compares only: a scenario with no row is an ERROR (stage `MissingBaseline`), because first baselines are created by `DataLoader seed` (see below), not by Radar. An existing non-PASS baseline is also an ERROR. Otherwise `RadarPricingService` compares the baseline with the API response. PASS updates the status, timestamp and baseline XML; FAIL updates only status. Extraction/technical errors do not update the baseline. All comparisons are reported in one consolidated CSV.

Run build identifiers remain in filenames and logs (and the Ice CSV) and are not read from or written to `[Xml].[Response]`.

One mandatory run-level `RadarRequestRateLimiter` staggers request starts across all Radar endpoints, including every retry. Configure `RadarRateLimitSettings:RequestsPerSecond` in appsettings or override it with `RadarRateLimitSettings__RequestsPerSecond`. The checked-in value of 2 gives a minimum 500 ms gap between starts; 4 gives 250 ms. The limiter uses monotonic elapsed time and starts the HTTP attempt inside a shared asynchronous gate, releasing the gate before awaiting the response. Slow responses can overlap, but an idle gap never causes catch-up bursts. There is no bounded rejection queue or `QueueLimit` setting: NUnit's four workers bound the active callers, which wait asynchronously with cancellation support. The shared budget is local to the run, not coordinated across separate processes or pipeline runs. ICE is unchanged and does not consume this budget.

HTTP 429 is a transient technical failure. When `Retry-After` contains valid delay-seconds or an HTTP date, the client caps that server delay at `RetrySettings:ApiRetryAfterMaxDelaySeconds` (60 seconds by default), then waits for the longer of the capped delay and local exponential backoff before entering the shared pacing gate again. A cap warning includes the supplied value, configured maximum, and effective delay. Missing or invalid headers use local backoff; invalid values are logged without the header content. Exhausted retries produce technical ERROR results without normal PASS/FAIL persistence.

### Central mappings and 16 XPath files

Product/scheme selection, Radar routing and Ice field selection live only in [mappings.json](config/radar/mappings.json). The 16 separate [XPathProfiles](config/radar/XPathProfiles) files contain only named XPath expressions. Appsettings holds `PricingProfiles:MappingFile`, endpoint definitions and runtime settings, not routes. Override the mapping location with `PricingProfiles__MappingFile`. Both live in the shared `config/radar` folder and are copied to `Radar/` in the Tests and DataLoader outputs, so `MappingFile` is `Radar/mappings.json`, resolved relative to each program's output folder (`PricingProfileConfiguration.ResolveMappingPath`); referenced files are resolved relative to the mapping file. Comparison and ICE use `PricingProfileConfiguration.Load`, which also opens and checks every XPath file. Callers that only route to Radar, such as baseline seeding, use `PricingRouteConfiguration.LoadRoutes`, which reads the same file but never opens the XPath files. Both are copied to output by the test project.

**All 16 XPath files are templates, not verified production contracts.** All main mappings use numbered `REPLACE_PRODUCT_*` / `REPLACE_SCHEME_*` placeholders and placeholder route keys. Replace the paths, complete the product/scheme/route mappings, select each endpoint and `iceField`, and configure real endpoint URLs, credentials, database access and thresholds before live runs. Main-profile tests check configuration consistency, not whether paths match unknown production XML. Extraction and report tests use fixed synthetic fixtures so editing production mappings does not change their expectations. Verify each real mapping against representative DB and API responses before a live run.

An entry in the central mapping:

```json
{
  "profileId": "Route001",
  "productCode": "HOME",
  "schemeCodes": ["ABC", "XYS", "POL"],
  "radar": { "endpointName": "Endpoint1", "routeKey": "home-abc" },
  "pathsFile": "XPathProfiles/Profile01.json",
  "iceField": "grossPremium"
}
```

The referenced file contains only fields:

```json
{
  "fields": {
    "grossPremium": "/Response/Totals/GrossPremium",
    "tax": "/Response/Totals/Tax"
  }
}
```

`PricingProfileConfiguration` loads the mappings and files once during setup. Missing files, invalid XPath syntax, duplicate product/scheme mappings and undefined Ice fields fail explicitly. Radar additionally checks endpoint references; Ice does not need Radar credentials. One model supports any field count, and multiple mappings can share a paths file.

`XmlValueExtractor` extracts configured elements/attributes into scenario-local `PricingDocument` values. Radar extracts the baseline before sending the request, then extracts the same fields from the response. `FuzzyPricingMatcher` pairs values by configured field name; each passes when its difference is between `MinThreshold` and `MaxThreshold`, inclusively. The Radar pipeline's **Threshold type** dropdown (`RADAR_THRESHOLD_TYPE`, or `PipelineSettings:ThresholdType`) sets what the difference is: **Pound** (the default) uses `API - Baseline` in pounds; **Percentage** uses the % change, `(API - Baseline) / |Baseline| x 100`, so `-2` and `2` mean within ±2%. A negative difference always means the API value is lower. In Percentage mode a zero baseline passes only if the API value is also zero, since no % change can be calculated from zero. The report's `Difference` column holds pounds or percent accordingly. Unconfigured XML is ignored: whole-document schema validation is removed. Missing, multiple-match, non-numeric or out-of-range values are extraction ERRORs, not zero defaults or skipped fields.

Use XPath 1.0 expressions with **one-based** positions (`/Response/Item[2]/Amount`) or identifying predicates; attributes use `/@amount`. Each field must select one element or attribute, not a scalar expression or text node. Namespace-prefix configuration is not supported; supplied expressions must work with the actual XML, using explicit `local-name()` expressions if appropriate.

Ice resolves the same mapping and evaluates only `iceField` against the DB XML. The ICE API JSON extraction and exact-equality comparison remain unchanged.

Comparison and reporting have 20,000-scenario regression workloads. Each document is parsed once, then its XPath expressions are evaluated; cost depends on expressions and document size. Completed report payloads are spooled to disk, retaining only IDs and offsets in memory. Teardown streams sorted rows into an atomic CSV and disposal removes the spool. NUnit still retains request test cases.

`RadarApiClient` accepts raw XML or a JSON envelope containing a top-level `response` XML string. It deserializes the envelope into `RadarJsonResponse`, then normalizes the XML before extraction and comparison. Representative API JSON payloads belong under `TestAssets/Json`; pricing mappings and XPath files belong under `config/radar`.

Database operations open their own connections and HTTP attempts use independent requests. Scenario data is not stored in global logging context. `Radar_<BuildId>.csv` is flat: one row per field, scenario identifiers and overall result repeated, XML only on the first row per scenario. Scenarios without comparisons still produce one outcome row. There are no summary rows or record-type column. Rows sort by scenario ID then field name; filtering/sorting may move or hide the XML-bearing row, so use ScenarioId to find it.

```csv
ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml
```

The Ice CSV retains its single-value result columns and adds `ProfileId,FieldName,XPath,Error`. Setup/discovery failures produce ERROR rows and logs. Reports are atomic and logs are flushed in teardown.

Local runs automatically write beneath `PricingValidationFramework.Tests/bin/<Configuration>/net10.0/TestResults/{Reports,Logs}`. The Radar pipeline's Radar and ICE stages publish these two folders as the `RadarReports` and `IceReports` artifacts after success or failure. `always()` tasks attempt publication on cancellation while the agent remains available; process termination or agent loss cannot guarantee outputs. Missing outputs fail staging after available files are staged. TRX results are published separately. Local execution does not upload to Azure. Reports contain XML payloads: restrict artifact access and retention; routine logs must not contain credentials or full XML.

The live workload is explicit and requires configured database access, Radar endpoints and credentials, real XPath mappings, and pipeline inputs:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --filter "FullyQualifiedName~RadarValidationTests" -- NUnit.ExplicitMode=Relaxed
```

Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. For local runs, the Radar and ICE fixtures also read `data/import/local-settings.json`, the same file DataLoader import and seed use (copy `data/import/examples/local-settings.example.json`, which sets exact-match thresholds of 0). Only the live fixtures read it, never the unit and system tests, and only in the Development/local environment; set `PVF_LOCAL_SETTINGS` to use a file elsewhere. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.

The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged and still filter on product. A second, separate filter selects by tag: `SCENARIO_TAG` (or `PipelineSettings:ScenarioTag`) is one tag, such as `smoke`, matched against the comma-separated `[Xml].[Request].Test_tags` (a whole tag, so `smoke` never matches `smoke2`). Both filters are optional and combine: product and tag. Tags reach `Test_tags` through the optional `test_tags` column of the requests CSV, set when a scenario is first inserted (see the DataLoader README). They are also NUnit categories.

Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, `SCENARIO_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `RADAR_REQUEST_DATETIME` accepts `now` as well as `yyyy-MM-ddZHH:mm:ss`. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:

```yaml
env:
  RADAR_MIN_THRESHOLD: $(RadarMinThreshold)
  RADAR_MAX_THRESHOLD: $(RadarMaxThreshold)
  TEST_TAG: $(TestTag)
  RADAR_REQUEST_DATETIME: $(RadarRequestDateTime)
```

`PipelineSettings__MinThreshold`, `PipelineSettings__MaxThreshold`, `PipelineSettings__TestTag`, and `PipelineSettings__RequestTime` are also supported as standard configuration environment overrides. The `RADAR_*` and `TEST_TAG` variables take precedence when both forms are set.

Radar no longer creates baselines and has no baseline-only mode. Run `DataLoader seed` (or the data-loader pipeline's Seed baselines stage) before Radar for any newly loaded scenarios.

Only the Radar endpoints in use need values. An endpoint whose URL, header and key are all empty is ignored, and a scenario whose route names an endpoint that isn't configured is an `EndpointResolution` ERROR while the rest of the run continues. A partly filled endpoint still fails setup.

To debug the Radar or ICE tests in VS Code, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process it prints.

### Azure DevOps integration pipelines

Register `PricingValidationFramework/azure-pipelines-radar.yml` from the repository root. It is manual-run only and has three stages. **Build** compiles the test project once and publishes it as the `test-build` artifact; Radar and ICE download it and run `dotnet test` on `PricingValidationFramework.Tests.dll`, so they run exactly that build, without checking out or compiling the repository, and their reports and logs are written under the downloaded folder's `TestResults`. The run form asks for the **threshold type** (Pound or Percentage), the **minimum and maximum thresholds** (in £ or % accordingly; left empty, the `RadarMinThreshold`/`RadarMaxThreshold` variables are used), the **scenario tag** (one tag from `Test_tags`, empty for all) and which baselines ICE checks. The thresholds are checked before any Radar call. The run's **Summary** tab shows the threshold type, min/max and filters used, the PASS/FAIL/ERROR counts, and each scenario's result with its failing fields' differences in £ or %. **Radar** runs `RadarValidationTests`; **ICE** then runs `IceValidationTests`, but only after someone approves it: its job targets the `pricing-ice-validation` environment, so the stage waits for that environment's approval. Rejecting the approval skips ICE and runs Radar alone. Radar selects scenarios with the `TestTag` variable (product) and the `scenarioTag` parameter (tag). The `iceSelection` parameter chooses what ICE checks: `radarRun` (the default) takes exactly the scenarios that passed in this run's Radar report, downloaded from the `RadarReports` artifact and passed as `ICE_RADAR_REPORT`; `newestPerProductScheme` takes the newest PASS baseline per product/scheme within the same product and tag filters. ICE runs after Radar whether or not Radar reported FAIL or ERROR scenarios, because a Radar FAIL never changes a PASS baseline; it is skipped only if the build failed or the run is cancelled. Both reports share the run's `BUILD_BUILDID` (`Radar_<id>.csv`, `Ice_<id>.csv`). There is no separate ICE pipeline: ICE always runs as the last stage, after Radar and its approval. Before the first run, create the `pricing-ice-validation` environment (Pipelines > Environments) and add an **Approvals** check listing who may approve ICE; without that check the ICE stage runs straight away. Each stage publishes NUnit TRX results to the Tests tab and its CSV report and logs as a `RadarReports` or `IceReports` artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.

Create the referenced pipeline variables in Azure DevOps before running it. The Radar stage uses the `RadarDatabaseConnectionString` database connection string and the ICE stage `IceDatabaseConnectionString` (both secrets; they can be the same database, and ICE only needs SELECT). Radar requires `RadarEndpoint1Url`, `RadarEndpoint1KeyHeader`, `RadarEndpoint1ApiKey` through `RadarEndpoint3Url`, `RadarEndpoint3KeyHeader`, `RadarEndpoint3ApiKey`, plus `TestTag` and `RadarRequestDateTime`. `RadarMinThreshold` and `RadarMaxThreshold` are needed only as defaults for runs that leave the thresholds empty on the run form. Define `TestTag` and `RadarRequestDateTime` as empty to select all scenarios and use the current UTC time. Keep endpoint API keys secret. ICE requires `IceEndpoint`, `IceApiKeyHeaderName`, `IceApiKeyHeaderValue`, `IcePfxCertificateBase64`, and `IceCertificatePassword`; keep the API key, PFX and password secret. Azure DevOps supplies `BUILD_BUILDID` automatically.

The chosen agent must reach the database and external endpoints. Replace the template pricing mappings and paths before live runs; the pipeline does not infer them. Use an agent pool with the required network access if the hosted pool cannot reach those services.

The Radar job timeout is 360 minutes. At the configured overall limit of two request starts per second, 20,000 API calls across any combination of endpoints require approximately 2 hours 47 minutes, before latency and retries. NUnit still has four scenario workers; the remaining scenarios wait to be scheduled rather than entering the limiter together. Ensure the agent entitlement permits a job of this duration; a free hosted job capped at 60 minutes cannot complete that workload even with the YAML timeout increased. Longer runs may require a self-hosted agent and a larger timeout. Local regression timings exclude SQL and live API calls and are not an end-to-end throughput guarantee.

## Set a baseline's status

Once someone has reviewed a Radar FAIL, they can set the scenario's baseline status back to PASS (or mark one FAIL) without touching the database themselves. Register `PricingValidationFramework/azure-pipelines-set-status.yml` as a manual pipeline. Its **Build** stage runs the set-status tests and publishes the data loader once as the `dataloader` artifact; the **SetStatus** stage runs that build with only the .NET 10 runtime. To use it:

1. Open the pipeline and click **Run pipeline**.
2. Commit a CSV of the scenario IDs to the repository and enter its path (required), e.g. `PricingValidationFramework/data/status/ids.csv`. The CSV can have a `scenario_id` header (other columns are ignored, so a loader CSV works too) or just one ID per row.
3. Choose the new status, **PASS** or **FAIL**, and click **Run**. Every ID in the CSV is set to that one status.

The run's **Summary** tab shows each ID with its status before and after, and whether it was updated, already at that status, or not found. The same summary is published as the `SetStatusSummary` artifact. The run fails if any ID wasn't found, so typos are noticed; the IDs that were found are still updated. To undo a change, run it again with the other status.

Only `Status`, `row_modifieddate` and `row_modifiedby` change. The baseline XML and `Last_updated` are never changed, and no rows are added or removed. The person who started the run (their Azure DevOps e-mail address) is recorded in `row_modifiedby`. Setting a FAIL back to PASS keeps the old baseline XML, so the next Radar run compares against it again.

Set-up:
- Add a secret pipeline variable `StatusUpdateDatabaseConnectionString`, using a login that doesn't need MFA and has SELECT and UPDATE on `[Xml].[Response]`. Nothing is created in the database.
- The agent pool (`agentPool` in the YAML's `variables` block, default `Default`) must be able to reach the database.
- Give "Queue builds" permission on this pipeline only to the people who should change statuses. To require a second person to approve each run, add an approval check (an Azure DevOps environment) in front of it.

The same change can be made locally: `dotnet run --project PricingValidationFramework.DataLoader -- set-status --status PASS --scenarios "id1,id2" --settings data/import/local-settings.json`. See the DataLoader README.

## Load scenario data

`PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `[Xml].[Request]` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `[Xml].[Request]`. New IDs are inserted. An existing ID is compared on its XML only: identical (normalized) XML is skipped and any other change updates `XML_request` alone; quote reference, product, scheme, tags and creation date are not compared or changed. The default batch size is 100 and SQL statements are limited to 333 rows. `validate` and `import` don't call Radar or write `[Xml].[Response]`. A separate `seed` command creates the first baseline for every scenario that has no `[Xml].[Response]` row: it calls Radar at the request time (`--request-time`, then `RADAR_REQUEST_DATETIME`, default `now`) and stores each response as a `PASS` baseline without comparing it. Scenarios that already have a baseline, of any status, are never touched. It reads the Radar endpoints and database from a `--settings` JSON and/or the same environment variables as the Radar pipeline, and routes with `config/radar/mappings.json` (routing only, no XPath files). The data-loader pipeline runs it as its **Seed baselines** stage after the import, at the pipeline's `requestDateTime`. See [the DataLoader README](PricingValidationFramework.DataLoader/README.md) for running it locally.

See [the importer guide](PricingValidationFramework.DataLoader/README.md) for CSV headers, local commands, database prerequisites, and the manual [data-loader pipeline](azure-pipelines-data-loader.yml). Local real CSVs belong under `data/import/` and are ignored by Git; synthetic examples are under `data/import/examples/`. The importer checks the full dataset before writing and uses disk-backed, bounded batches for approximately 20,000 scenarios.

## Run the ICE validation flow

The ICE flow is an explicit, non-parallel NUnit integration fixture. Database-only discovery loads passing baselines and creates one `Ice_scenario_<ScenarioId>` test case per scenario through `TestCaseSource`. Each case calls ICE for its quote reference, compares the ICE premium with the XML baseline using exact equality, records its report row, and asserts independently. Fixture teardown writes one consolidated CSV.

### Prerequisites

- .NET SDK 10.0, matching the projects' `net10.0` target framework.
- Network access to the SQL Server database and ICE endpoint.
- SQL Server `[Xml].[Response]` rows with `Status = 'PASS'` and valid `XML_Response`, linked to `[Xml].[Request]` by unique `Scenario_id`; quote and product/scheme identifiers come from `[Xml].[Request]`.
- A valid ICE API key and client certificate.
- The client certificate's password, if the PFX is password protected.

By default the workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair, limited to the `TEST_TAG` product and the `SCENARIO_TAG` tag when they are set. When `ICE_RADAR_REPORT` (or `PipelineSettings:IceRadarReport`) names a `Radar_<BuildId>.csv`, it instead selects exactly the scenarios whose `ScenarioResult` is `PASS` in that report, ignoring the filters; the Radar pipeline's ICE stage does this by default. It calls ICE using this URL shape:

```text
{IceEndpoint}/{QuoteRef}
```

`QuoteRef` is URL-escaped. A row passes only when:

```text
iceValue == baselineValue
```

### Configure settings

The test loads configuration in this order:

1. `PricingValidationFramework.Tests/appsettings.json`
2. `PricingValidationFramework.Tests/appsettings.Development.json`, when present

The development file overrides matching values in the base file. Keep real connection strings, API keys, certificate passwords, and certificate material out of source control. The checked-in values are placeholders or development examples.

Configure these values under `IceSettings` and `DatabaseSettings` in `appsettings.Development.json`:

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=YOUR_SERVER;Database=PricingValidation;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "IceSettings": {
    "IceEndpoint": "https://ice.example.com/quote",
    "ApiKeyHeaderName": "X-ICE-API-KEY",
    "ApiKeyHeaderValue": "YOUR_ICE_API_KEY",
    "CertificateHost": "ice.example.com",
    "PfxCertificateFile": "C:\\SecureCertificates\\ice-client.pfx",
    "CertificatePassword": "YOUR_PFX_PASSWORD",
    "PfxCertificateBase64": ""
  }
}
```

`CertificateHost` is retained as ICE configuration metadata. The current HTTP client does not use it to override TLS host validation; the endpoint host must match the certificate and server TLS configuration.

### Configure the client certificate

The ICE client uses mutual TLS and needs one PFX certificate. Configure **one** of the following options.

#### Option A: PFX file path

This is the recommended local-development approach.

1. Store the PFX outside the repository, for example `C:\SecureCertificates\ice-client.pfx`.
2. Set `IceSettings:PfxCertificateFile` to its absolute path.
3. Set `IceSettings:CertificatePassword` to the PFX password.
4. Leave `IceSettings:PfxCertificateBase64` empty.

An absolute path is recommended because tests run from the compiled test-output directory, not from the repository root.

#### Option B: Base64-encoded PFX

For CI or a secret store, set `IceSettings:PfxCertificateBase64` to the Base64 content of the PFX and set `IceSettings:CertificatePassword`. When this value is non-empty, it takes precedence over `PfxCertificateFile`.

Do not commit the encoded certificate or its password. Inject both from your CI secret store when creating the test configuration.

#### Repository-local certificate location

The conventional project location is:

```text
PricingValidationFramework.Tests/TestAssets/Ice/client-certificate.pfx
```

If you use that location, `PfxCertificateFile` must resolve from the test output directory at runtime. Add this item to `PricingValidationFramework.Tests.csproj` so the asset is copied during builds:

```xml
<ItemGroup>
  <None Update="TestAssets\Ice\client-certificate.pfx" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Then configure:

```json
"PfxCertificateFile": "TestAssets/Ice/client-certificate.pfx"
```

Do not commit the actual PFX. Keep the `.gitkeep` file so the folder exists, and provide the certificate securely on each development machine or CI agent.

### Configure the database baseline data

ICE reads `[Xml].[Response]` rows where `Status = 'PASS'`, joining `[Xml].[Request]` on `Scenario_id` for quote and product/scheme identifiers. For each product and scheme, it selects the most recently updated row, using `Scenario_id` as a tie-breaker.

Each selected row must supply:

- `Scenario_id`
- `Quote_ref`
- `Product_code`
- `Scheme_code`
- `XML_Response`
- `Status`
- `Last_updated`

`XML_Response` must contain the value selected by the product/scheme mapping's `iceField` XPath. ICE returns the same JSON payload expected by `JsonValueExtractor`; its API extraction is unchanged.

### Restore and build

From the repository root:

```powershell
dotnet restore .\PricingValidationFramework.slnx
dotnet build .\PricingValidationFramework.slnx --no-restore
```

### Run the ICE workload

The test is marked `Explicit`, so select it directly:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --no-restore --filter "FullyQualifiedName~IceValidationTests" -- NUnit.ExplicitMode=Relaxed
```

The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection is the newest passing baseline per product/scheme within the `TEST_TAG` product and `SCENARIO_TAG` tag filters, or, with `ICE_RADAR_REPORT`, exactly the scenarios that passed in that Radar report. A missing or unreadable report fails discovery with the file named.

To set the report build identifier, set `BUILD_BUILDID` before running. If it is not set, the report uses `local`.

```powershell
$env:BUILD_BUILDID = "20260923.1"
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --no-restore --filter "FullyQualifiedName~IceValidationTests" -- NUnit.ExplicitMode=Relaxed
```

### Find the report

The report is written under the test output directory:

```text
PricingValidationFramework.Tests/bin/Debug/net10.0/TestResults/Reports/Ice_{BuildId}.csv
```

It contains `BuildId`, scenario and quote identifiers, product/scheme data, ICE value, baseline value, and a `PASS`, `FAIL`, or technical `ERROR` result. Both `FAIL` and `ERROR` fail the corresponding NUnit case.

### Troubleshooting

| Symptom | Cause and action |
| --- | --- |
| `Could not find ... client-certificate.pfx` | Use an absolute `PfxCertificateFile` path, or add the PFX copy rule above and place the file under `TestAssets/Ice`. |
| Certificate cryptographic error | Confirm the PFX exists, the password is correct, and the certificate includes its private key. |
| HTTP 401 or 403 | Verify `ApiKeyHeaderName` and `ApiKeyHeaderValue` with the ICE provider. |
| TLS or handshake error | Verify the client certificate is authorized by ICE and that `IceEndpoint` uses the expected host and certificate chain. |
| SQL connection error | Verify `DatabaseSettings:ConnectionString`, network access, SQL authentication, and access to `[Xml].[Request]` and `[Xml].[Response]`. |
| No report appears | Resolve discovery or setup failures first. Teardown writes a report when scenario rows have been collected or execution cancellation was observed. |
| Discovery case fails because no scenarios matched | Check that `[Xml].[Response]` contains `PASS` rows linked to `[Xml].[Request]` by `Scenario_id`. Empty selection is a test failure, not a passing empty workload. |

### Security checklist

- Never commit a PFX, certificate password, API key, or production connection string.
- Prefer an absolute certificate path on developer machines.
- Prefer Base64 certificate injection from a CI secret store in automated runs.
- Restrict read access to the certificate and configuration containing its password.