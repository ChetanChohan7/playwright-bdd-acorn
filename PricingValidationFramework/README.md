# Pricing Validation Framework

## Run the test suite

```bash
dotnet test PricingValidationFramework.Tests
```

No database, endpoints, or credentials needed - this runs every unit and system test (extraction, matching, URL building, rate limiting, retry/config validation, CSV reporting, etc.). The two live validation flows below (`IceValidationTests`, `RadarValidationTests`) are `[Explicit]` and sit out of a normal run since they need real infrastructure; see their sections for how to opt into each.

## Run the Radar validation flow

The explicit Radar workload creates one NUnit test case per selected `[Xml].[Request]` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its `[Xml].[Response]` row. If there is none, it calls Radar and stores the initial baseline with `Status = PASS` without XPath extraction or comparison. A later consumer reports an extraction error if required values are absent. An existing non-PASS baseline is an ERROR. Otherwise `RadarPricingService` compares the baseline with the API response. PASS updates the status, timestamp and baseline XML; FAIL updates only status. Extraction/technical errors do not update the baseline. All comparisons are reported in one consolidated CSV.

Run build identifiers remain in filenames and logs (and the Ice CSV) and are not read from or written to `[Xml].[Response]`.

One mandatory run-level `RadarRequestRateLimiter` staggers request starts across all Radar endpoints, including every retry. Configure `RadarRateLimitSettings:RequestsPerSecond` in appsettings or override it with `RadarRateLimitSettings__RequestsPerSecond`. The checked-in value of 2 gives a minimum 500 ms gap between starts; 4 gives 250 ms. The limiter uses monotonic elapsed time and starts the HTTP attempt inside a shared asynchronous gate, releasing the gate before awaiting the response. Slow responses can overlap, but an idle gap never causes catch-up bursts. There is no bounded rejection queue or `QueueLimit` setting: NUnit's four workers bound the active callers, which wait asynchronously with cancellation support. The shared budget is local to the run, not coordinated across separate processes or pipeline runs. ICE is unchanged and does not consume this budget.

HTTP 429 is a transient technical failure. When `Retry-After` contains valid delay-seconds or an HTTP date, the client caps that server delay at `RetrySettings:ApiRetryAfterMaxDelaySeconds` (60 seconds by default), then waits for the longer of the capped delay and local exponential backoff before entering the shared pacing gate again. A cap warning includes the supplied value, configured maximum, and effective delay. Missing or invalid headers use local backoff; invalid values are logged without the header content. Exhausted retries produce technical ERROR results without normal PASS/FAIL persistence.

### Central mappings and 16 XPath files

Product/scheme selection, Radar routing and Ice field selection live only in [mappings.json](config/radar/mappings.json). The 16 separate [XPathProfiles](config/radar/XPathProfiles) files contain only named XPath expressions. Appsettings holds `PricingProfiles:MappingFile`, endpoint definitions and runtime settings, not routes or XSD mappings. Override the mapping location with `PricingProfiles__MappingFile`. Both live in the shared `config/radar` folder and are copied to `Radar/` in the Tests and DataLoader outputs, so `MappingFile` is `Radar/mappings.json`, resolved relative to each program's output folder (`PricingProfileConfiguration.ResolveMappingPath`); referenced files are resolved relative to the mapping file. Comparison and ICE use `PricingProfileConfiguration.Load`, which also opens and checks every XPath file. Callers that only route to Radar, such as baseline seeding, use `PricingRouteConfiguration.LoadRoutes`, which reads the same file but never opens the XPath files. Both are copied to output by the test project.

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

`XmlValueExtractor` extracts configured elements/attributes into scenario-local `PricingDocument` values. Radar extracts the baseline before sending the request, then extracts the same fields from the response. `FuzzyPricingMatcher` pairs values by configured field name; each passes when `MinThreshold <= API - Baseline <= MaxThreshold`, inclusively. Unconfigured XML is ignored: whole-document schema validation is removed. Missing, multiple-match, non-numeric or out-of-range values are extraction ERRORs, not zero defaults or skipped fields.

Use XPath 1.0 expressions with **one-based** positions (`/Response/Item[2]/Amount`) or identifying predicates; attributes use `/@amount`. Each field must select one element or attribute, not a scalar expression or text node. Namespace-prefix configuration is not supported; supplied expressions must work with the actual XML, using explicit `local-name()` expressions if appropriate.

Ice resolves the same mapping and evaluates only `iceField` against the DB XML. The ICE API JSON extraction and exact-equality comparison remain unchanged.

Comparison and reporting have 20,000-scenario regression workloads. Each document is parsed once, then its XPath expressions are evaluated; cost depends on expressions and document size. Completed report payloads are spooled to disk, retaining only IDs and offsets in memory. Teardown streams sorted rows into an atomic CSV and disposal removes the spool. NUnit still retains request test cases.

`RadarApiClient` accepts raw XML or a JSON envelope containing a top-level `response` XML string. It deserializes the envelope into `RadarJsonResponse`, then normalizes the XML before extraction and comparison. Representative API JSON payloads belong under `TestAssets/Json`; pricing mappings and XPath files belong under `config/radar`.

Database operations open their own connections and HTTP attempts use independent requests. Scenario data is not stored in global logging context. `Radar_<BuildId>.csv` is flat: one row per field, scenario identifiers and overall result repeated, XML only on the first row per scenario. Scenarios without comparisons still produce one outcome row. There are no summary rows or record-type column. Rows sort by scenario ID then field name; filtering/sorting may move or hide the XML-bearing row, so use ScenarioId to find it.

```csv
ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml
```

The Ice CSV retains its single-value result columns and adds `ProfileId,FieldName,XPath,Error`. Setup/discovery failures produce ERROR rows and logs. Reports are atomic and logs are flushed in teardown.

Local runs automatically write beneath `PricingValidationFramework.Tests/bin/<Configuration>/net10.0/TestResults/{Reports,Logs}`. Both Azure pipelines publish these two folders under their existing `RadarReports` / `IceReports` artifacts after success or failure. `always()` tasks attempt publication on cancellation while the agent remains available; process termination or agent loss cannot guarantee outputs. Missing outputs fail staging after available files are staged. TRX results are published separately. Local execution does not upload to Azure. Reports contain XML payloads: restrict artifact access and retention; routine logs must not contain credentials or full XML.

The live workload is explicit and requires configured database access, Radar endpoints and credentials, real XPath mappings, and pipeline inputs:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --filter "FullyQualifiedName~RadarValidationTests" -- NUnit.ExplicitMode=Relaxed
```

Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. The user-facing `TestTag` input filters `[Xml].[Request].Product_code` using SQL equality: enter the product code, such as `HOME`, to select all matching request scenarios across their schemes. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.

The `TestTag`, `TEST_TAG`, and `PipelineSettings__TestTag` input names are unchanged. Stored `[Xml].[Request].Test_tags` values remain scenario metadata and NUnit categories, not workload-selection criteria. Previously supplied tag values must be replaced with actual product codes.

Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:

```yaml
env:
  RADAR_MIN_THRESHOLD: $(RadarMinThreshold)
  RADAR_MAX_THRESHOLD: $(RadarMaxThreshold)
  TEST_TAG: $(TestTag)
  RADAR_REQUEST_DATETIME: $(RadarRequestDateTime)
```

`PipelineSettings__MinThreshold`, `PipelineSettings__MaxThreshold`, `PipelineSettings__TestTag`, and `PipelineSettings__RequestTime` are also supported as standard configuration environment overrides. The `RADAR_*` and `TEST_TAG` variables take precedence when both forms are set.

`RADAR_BASELINE_ONLY` (or `PipelineSettings:BaselineOnly`) set to `true` makes a run create baselines only: scenarios that already have an `[Xml].[Response]` row are skipped without calling Radar. It defaults to false. The data-loader pipeline's Create baselines stage uses it.

### Azure DevOps integration pipelines

Register two separate YAML pipelines using `PricingValidationFramework/azure-pipelines-radar.yml` and `PricingValidationFramework/azure-pipelines-ice.yml` from the repository root. Both are manual-run only. Each selects its own explicit integration fixture, publishes NUnit TRX results to the Tests tab, and publishes its CSV report as a separate `RadarReports` or `IceReports` pipeline artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.

Create the referenced pipeline variables in Azure DevOps before running them. Both pipelines require a database connection string (`RadarDatabaseConnectionString` or `IceDatabaseConnectionString`, stored as a secret). Radar requires `RadarEndpoint1Url`, `RadarEndpoint1KeyHeader`, `RadarEndpoint1ApiKey` through `RadarEndpoint3Url`, `RadarEndpoint3KeyHeader`, `RadarEndpoint3ApiKey`, plus `RadarMinThreshold`, `RadarMaxThreshold`, `TestTag`, and `RadarRequestDateTime`. Define `TestTag` and `RadarRequestDateTime` as empty to select all scenarios and use the current UTC time. Keep endpoint API keys secret. ICE requires `IceEndpoint`, `IceApiKeyHeaderName`, `IceApiKeyHeaderValue`, `IcePfxCertificateBase64`, and `IceCertificatePassword`; keep the API key, PFX and password secret. Azure DevOps supplies `BUILD_BUILDID` automatically.

The chosen agent must reach the database and external endpoints. Replace the template pricing mappings and paths before live runs; the pipeline does not infer them. Use an agent pool with the required network access if the hosted pool cannot reach those services.

The Radar job timeout is 360 minutes. At the configured overall limit of two request starts per second, 20,000 API calls across any combination of endpoints require approximately 2 hours 47 minutes, before latency and retries. NUnit still has four scenario workers; the remaining scenarios wait to be scheduled rather than entering the limiter together. Ensure the agent entitlement permits a job of this duration; a free hosted job capped at 60 minutes cannot complete that workload even with the YAML timeout increased. Longer runs may require a self-hosted agent and a larger timeout. Local regression timings exclude SQL and live API calls and are not an end-to-end throughput guarantee.

## Load scenario data

`PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `[Xml].[Request]` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `[Xml].[Request]`. New IDs are inserted. An existing ID is compared on its XML only: identical (normalized) XML is skipped and any other change updates `XML_request` alone; quote reference, product, scheme, tags and creation date are not compared or changed. The default batch size is 100 and SQL statements are limited to 333 rows. The loader doesn't call Radar or write baselines itself. The data-loader pipeline's optional **Create baselines** stage runs Radar in baseline-only mode (`RADAR_BASELINE_ONLY=true`) after the import, at the pipeline's `requestDateTime` (default `now`), so new scenarios get baselines and existing ones are left untouched.

See [the importer guide](PricingValidationFramework.DataLoader/README.md) for CSV headers, local commands, database prerequisites, and the manual [data-loader pipeline](azure-pipelines-data-loader.yml). Local real CSVs belong under `data/import/` and are ignored by Git; synthetic examples are under `data/import/examples/`. The importer checks the full dataset before writing and uses disk-backed, bounded batches for approximately 20,000 scenarios.

## Run the ICE validation flow

The ICE flow is an explicit, non-parallel NUnit integration fixture. Database-only discovery loads passing baselines and creates one `Ice_scenario_<ScenarioId>` test case per scenario through `TestCaseSource`. Each case calls ICE for its quote reference, compares the ICE premium with the XML baseline using exact equality, records its report row, and asserts independently. Fixture teardown writes one consolidated CSV.

### Prerequisites

- .NET SDK 10.0, matching the projects' `net10.0` target framework.
- Network access to the SQL Server database and ICE endpoint.
- SQL Server `[Xml].[Response]` rows with `Status = 'PASS'` and valid `XML_Response`, linked to `[Xml].[Request]` by unique `Scenario_id`; quote and product/scheme identifiers come from `[Xml].[Request]`.
- A valid ICE API key and client certificate.
- The client certificate's password, if the PFX is password protected.

The workload selects the newest `PASS` baseline for each `(Product_code, Scheme_code)` pair. It calls ICE using this URL shape:

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

The fixture runs its discovered cases sequentially using one shared client and certificate initialized in `OneTimeSetUp`. A mismatch or technical API/extraction error fails only that case; remaining cases continue, and the row is collected before the assertion. Empty selection, duplicate scenario IDs, and discovery failures produce an explicit failed discovery case. Teardown writes all completed scenario rows with a non-cancelled reporting token before disposing resources, preserving partial results after cancellation. Discovery does not load the certificate or create the API client. Scenario selection remains the newest passing baseline per product/scheme; test-tag filtering is not enabled for ICE.

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