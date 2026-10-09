# Pricing Validation Framework

## Run the test suite

```bash
dotnet test PricingValidationFramework.Tests
```

No database, endpoints, or credentials needed - this runs every unit and system test (extraction, matching, URL building, rate limiting, retry/config validation, CSV reporting, etc.). The two live validation flows below (`IceValidationTests`, `RadarValidationTests`) are `[Explicit]` and sit out of a normal run since they need real infrastructure; see their sections for how to opt into each.

## Run the Radar validation flow

The explicit Radar workload creates one NUnit test case per selected `xml_request` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads the scenario's `xml_response` row. If there is none (a newly loaded scenario), it calls Radar, inserts the response as the scenario's baseline with `Status = PASS`, and reports PASS without XSD validation or the pricing comparison. If the row exists but isn't `PASS`, the scenario is an ERROR. Otherwise it delegates the Radar request and pricing comparison to `RadarPricingService` in Core. The test persists the scenario-level result: on PASS it writes the status and the Radar response XML, which becomes the new baseline; on FAIL it updates only `xml_response.Status` and `Build_id`, leaving the baseline XML unchanged. It then collects one summary with its dynamic field results, and asserts the returned outcome. A single consolidated CSV is written after all cases finish.

One mandatory run-level `RadarRequestRateLimiter` staggers request starts across all Radar endpoints, including every retry. Configure `RadarRateLimitSettings:RequestsPerSecond` in appsettings or override it with `RadarRateLimitSettings__RequestsPerSecond`. The checked-in value of 2 gives a minimum 500 ms gap between starts; 4 gives 250 ms. The limiter uses monotonic elapsed time and starts the HTTP attempt inside a shared asynchronous gate, releasing the gate before awaiting the response. Slow responses can overlap, but an idle gap never causes catch-up bursts. There is no bounded rejection queue or `QueueLimit` setting: NUnit's four workers bound the active callers, which wait asynchronously with cancellation support. The shared budget is local to the run, not coordinated across separate processes or pipeline runs. ICE is unchanged and does not consume this budget.

HTTP 429 is a transient technical failure. When `Retry-After` contains valid delay-seconds or an HTTP date, the client caps that server delay at `RetrySettings:ApiRetryAfterMaxDelaySeconds` (60 seconds by default), then waits for the longer of the capped delay and local exponential backoff before entering the shared pacing gate again. A cap warning includes the supplied value, configured maximum, and effective delay. Missing or invalid headers use local backoff; invalid values are logged without the header content. Exhausted retries produce technical ERROR results without normal PASS/FAIL persistence.

Radar selects a route by exact `ProductCode` and `SchemeCode`, then selects its schema through `RadarSettings.ResponseXsdMappings[routeId]`. All schemes listed on that route use the same XSD for baseline and current response. `RadarPricingProfileFactory` resolves and compiles the schemas under `TestAssets/Xsd` during setup. The compiled schema sets, including their field/type definitions, are cached for the run and safely shared by concurrent scenarios. Missing mappings, missing files, and invalid schemas fail setup before HTTP resources are created; validation cannot be disabled.

`XsdValidator` validates each XML and extracts schema-typed decimal elements and attributes into a scenario-local `PricingDocument`. `RadarPricingService` validates and extracts the baseline before sending the request, then validates and extracts the API response. `FuzzyPricingMatcher` pairs values by full XML path using dictionary lookups, without per-field configuration or regex matching. The path is also the report field key. Each field passes when `MinThreshold <= Actual - Expected <= MaxThreshold`; both bounds are inclusive. Non-decimal fields are excluded from comparison but still validated against the schema. Optional or nil decimals absent on both sides are skipped; a value present on only one side fails. An empty decimal comparison is an ERROR, not a pass.

Schema integration point: add each approved XSD under `PricingValidationFramework.Tests/TestAssets/Xsd`, then map its route ID to the filename. If schemes need different schemas, place them in separate routes; those routes may still share the same endpoint and `RouteKey`. `PricingComparisonFixture.xsd` is test-only, not a production contract. The checked-in production schema filenames are placeholders until their approved XSD files are supplied.

No `PricingProfiles`, `Fields`, `FieldKey`, `ExpectedPath`, or `ActualPath` settings are required:

```json
{
  "RadarSettings": {
    "Routes": {
      "Route003": {
        "ProductCode": "MOTOR",
        "SchemeCodes": ["ABC"],
        "EndpointName": "Endpoint2",
        "RouteKey": "motor-abc"
      }
    },
    "ResponseXsdMappings": {
      "Route003": "Motor_ABC.xsd"
    }
  }
}
```

Repeated decimal elements use zero-based indexed paths, for example `/PricingResponse/Fees/Surcharge[0]`; attributes use `/@attributeName`. Baseline and response must follow the same contract and repeated records must have stable ordering. Field renames or business-key pairing cannot be inferred from an XSD alone.

The comparison and reporting paths have regression workloads of 20,000 scenarios. Comparison uses one pass over each XML tree and linear-time field lookups. Completed report payloads are spooled to a temporary file; memory retains scenario IDs and file offsets, not every completed XML document and field result. Teardown streams sorted rows into the final atomic CSV and disposal removes the spool. Allow local temporary disk space for both the spool and the final CSV. NUnit still retains the discovered request test cases, so overall memory depends on request size and the test runner as well as comparison work.

`RadarApiClient` accepts raw XML or a JSON envelope containing a top-level `response` XML string. It deserializes the envelope into `RadarJsonResponse`, then normalizes the extracted XML before XSD validation and pricing comparison. Approved JSON contracts and representative payloads belong under `PricingValidationFramework.Tests/TestAssets/Json`; `placeholder-response.json` is an empty directory placeholder, not a contract. Both asset folders are intentional and must be retained during cleanup.

Database operations open their own SQL connection per operation. Each HTTP attempt uses a new request with scenario-specific URL, authentication header, and XML body. Scenario data is not stored in global logging context; Radar logs retain build-level context and structured scenario identifiers without logging request/response XML or credentials. The final `Radar_<BuildId>.csv` contains one `SUMMARY` row per scenario followed by its dynamic `FIELD` rows. The stable columns include the canonical field key, both XML paths, expected/actual/delta/range/result, and summary counts; request, baseline, and API XML are emitted only on the summary row at the end of the CSV. Report writes use a temporary file before final replacement. ICE remains separate with sequential per-scenario NUnit cases and its existing exact-equality comparison.

The live workload is explicit and requires configured database access, Radar endpoints and credentials, approved XSD files, and pipeline inputs:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --filter "FullyQualifiedName~RadarValidationTests" -- NUnit.ExplicitMode=Relaxed
```

Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json`. Optional local environment files or environment variables can override these values. Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.

Pipeline environment variables `RADAR_MIN_THRESHOLD`, `RADAR_MAX_THRESHOLD`, `TEST_TAG`, and `RADAR_REQUEST_DATETIME` override the corresponding appsettings values. `BUILD_BUILDID` remains required outside local development. Azure DevOps can pass these variables to the Radar test step:

```yaml
env:
  RADAR_MIN_THRESHOLD: $(RadarMinThreshold)
  RADAR_MAX_THRESHOLD: $(RadarMaxThreshold)
  TEST_TAG: $(TestTag)
  RADAR_REQUEST_DATETIME: $(RadarRequestDateTime)
```

`PipelineSettings__MinThreshold`, `PipelineSettings__MaxThreshold`, `PipelineSettings__TestTag`, and `PipelineSettings__RequestTime` are also supported as standard configuration environment overrides. The `RADAR_*` and `TEST_TAG` variables take precedence when both forms are set.

### Azure DevOps integration pipelines

Register two separate YAML pipelines using `PricingValidationFramework/azure-pipelines-radar.yml` and `PricingValidationFramework/azure-pipelines-ice.yml` from the repository root. Both are manual-run only. Each selects its own explicit integration fixture, publishes NUnit TRX results to the Tests tab, and publishes its CSV report as a separate `RadarReports` or `IceReports` pipeline artifact, including when tests fail after report creation. No system or unit tests run in these pipelines.

Create the referenced pipeline variables in Azure DevOps before running them. Both pipelines require a database connection string (`RadarDatabaseConnectionString` or `IceDatabaseConnectionString`, stored as a secret). Radar requires `RadarEndpoint1Url`, `RadarEndpoint1KeyHeader`, `RadarEndpoint1ApiKey` through `RadarEndpoint3Url`, `RadarEndpoint3KeyHeader`, `RadarEndpoint3ApiKey`, plus `RadarMinThreshold`, `RadarMaxThreshold`, `TestTag`, and `RadarRequestDateTime`. Define `TestTag` and `RadarRequestDateTime` as empty to select all scenarios and use the current UTC time. Keep endpoint API keys secret. ICE requires `IceEndpoint`, `IceApiKeyHeaderName`, `IceApiKeyHeaderValue`, `IcePfxCertificateBase64`, and `IceCertificatePassword`; keep the API key, PFX and password secret. Azure DevOps supplies `BUILD_BUILDID` automatically.

The chosen agent must reach the database and external endpoints. Radar also needs approved XSDs under `PricingValidationFramework.Tests/TestAssets/Xsd` and a `ResponseXsdMappings` entry for every configured route; the comparison fixture is not a production schema. Use an agent pool with the required network access if the hosted pool cannot reach those services.

The Radar job timeout is 360 minutes. At the configured overall limit of two request starts per second, 20,000 API calls across any combination of endpoints require approximately 2 hours 47 minutes, before latency and retries. NUnit still has four scenario workers; the remaining scenarios wait to be scheduled rather than entering the limiter together. Ensure the agent entitlement permits a job of this duration; a free hosted job capped at 60 minutes cannot complete that workload even with the YAML timeout increased. Longer runs may require a self-hosted agent and a larger timeout. Local regression timings exclude SQL and live API calls and are not an end-to-end throughput guarantee.

## Load scenario data

`PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `xml_request` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `xml_request`. New IDs are inserted, identical normalized requests are skipped, and changed XML/quote references are updated only when product and scheme remain unchanged. Product/scheme changes reject the import; existing tags and creation dates are preserved. The default batch size is 100 and SQL statements are limited to 333 rows. The loader doesn't call Radar or write baselines: a separate Radar run creates one for each new scenario.

See [the importer guide](PricingValidationFramework.DataLoader/README.md) for CSV headers, local commands, database prerequisites, and the manual [data-loader pipeline](azure-pipelines-data-loader.yml). Local real CSVs belong under `data/import/` and are ignored by Git; synthetic examples are under `data/import/examples/`. The importer checks the full dataset before writing and uses disk-backed, bounded batches for approximately 20,000 scenarios.

## Run the ICE validation flow

The ICE flow is an explicit, non-parallel NUnit integration fixture. Database-only discovery loads passing baselines and creates one `Ice_scenario_<ScenarioId>` test case per scenario through `TestCaseSource`. Each case calls ICE for its quote reference, compares the ICE premium with the XML baseline using exact equality, records its report row, and asserts independently. Fixture teardown writes one consolidated CSV.

### Prerequisites

- .NET SDK 10.0, matching the projects' `net10.0` target framework.
- Network access to the SQL Server database and ICE endpoint.
- SQL Server `xml_response` rows with `Status = 'PASS'` and valid `XML_Response`, linked to `xml_request` by unique `Scenario_id`; quote and product/scheme identifiers come from `xml_request`.
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

ICE reads `xml_response` rows where `Status = 'PASS'`, joining `xml_request` on `Scenario_id` for quote and product/scheme identifiers. For each product and scheme, it selects the most recently updated row, using `Scenario_id` as a tie-breaker.

Each selected row must supply:

- `Scenario_id`
- `Quote_ref`
- `Product_code`
- `Scheme_code`
- `XML_Response`
- `Status`
- `Last_updated`

`XML_Response` must contain a baseline value in the XML format expected by `XmlValueExtractor`. ICE returns a JSON payload in the format expected by `JsonValueExtractor`.

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
| SQL connection error | Verify `DatabaseSettings:ConnectionString`, network access, SQL authentication, and access to `xml_request` and `xml_response`. |
| No report appears | Resolve discovery or setup failures first. Teardown writes a report when scenario rows have been collected or execution cancellation was observed. |
| Discovery case fails because no scenarios matched | Check that `xml_response` contains `PASS` rows linked to `xml_request` by `Scenario_id`. Empty selection is a test failure, not a passing empty workload. |

### Security checklist

- Never commit a PFX, certificate password, API key, or production connection string.
- Prefer an absolute certificate path on developer machines.
- Prefer Base64 certificate injection from a CI secret store in automated runs.
- Restrict read access to the certificate and configuration containing its password.