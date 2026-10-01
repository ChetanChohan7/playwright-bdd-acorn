# Pricing Validation Framework

## Run the test suite

```bash
dotnet test PricingValidationFramework.Tests
```

No database, endpoints, or credentials needed - this runs every unit and system test (extraction, matching, URL building, rate limiting, retry/config validation, CSV reporting, etc.). The two live validation flows below (`IceValidationTests`, `RadarValidationTests`) are `[Explicit]` and sit out of a normal run since they need real infrastructure; see their sections for how to opt into each.

## Run the Radar validation flow

The explicit Radar workload creates one NUnit test case per selected `TB_REQUEST` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its baseline, runs one scenario through `RadarScenarioProcessor`, persists the existing PASS or FAIL outcome, adds one terminal row to a thread-safe collection, and asserts its own result. A single consolidated CSV is written after all cases finish.

Every configured logical endpoint has its own mandatory `SlidingWindowRateLimiter`: at most 2 request starts per rolling second and up to 4 waiting attempts. `PricingA`, `PricingB`, and `PricingC` therefore have independent budgets; saturation on one endpoint does not consume another endpoint's capacity. Initial requests and retries use the same endpoint limiter. There is no separate in-flight request limit; NUnit remains the sole scenario-concurrency owner with four workers. Checked-in `RadarRateLimitSettings` uses `RequestsPerSecond: 2` and `QueueLimit: 4` and cannot be disabled.

HTTP 429 is a transient technical failure. When `Retry-After` contains valid delay-seconds or an HTTP date, the client caps that server delay at `RetrySettings:ApiRetryAfterMaxDelaySeconds` (60 seconds by default), then waits for the longer of the capped delay and local exponential backoff before acquiring another endpoint permit. A cap warning includes the supplied value, configured maximum, and effective delay. Missing or invalid headers use local backoff; invalid values are logged without the header content. Exhausted retries and a full endpoint queue produce technical ERROR results without normal PASS/FAIL persistence.

Radar responses are validated against the response XSD selected by `RadarSettings:ResponseXsdMappings`, keyed by the existing route identifier (`Route001`, etc.), then deserialized into the focused `RadarResponse` model. The model currently contains only the evidenced `TotalAmount`; raw response XML remains available to the existing report and persistence paths. No request XML schema validation is performed.

Response XSD validation is controlled by the `RadarSettings:ValidateResponseXsd` feature flag, which defaults to `false` until the approved schemas are received. Set it to `true` in configuration, or with the environment variable `RadarSettings__ValidateResponseXsd=true`, to validate responses against `ResponseXsdMappings`. When it is off, the Radar log records a warning at startup, and scenarios are still compared, reported, and persisted as PASS or FAIL without a schema check. Responses must still deserialize to `Response/TotalAmount`.

Response schema integration point: add each approved XSD under `PricingValidationFramework.Tests/TestAssets/Xsd`, then map its filename to the applicable route identifier in `ResponseXsdMappings`. Schema names are not derived from ProductCode or SchemeCode. `placeholder-response-schema.xsd` documents the currently known minimal shape only and is not a client-approved production schema. The `RadarSystemTest.xsd` file is a system-test fixture.

JSON integration point: place future client JSON contracts and representative payloads under `PricingValidationFramework.Tests/TestAssets/Json`. `RadarJsonResponse` is intentionally empty until the client contract arrives. The future path is JSON deserialization into that typed model, followed by model-based validation and reporting; the current Radar flow does not deserialize JSON. `placeholder-response.json` is an empty directory placeholder, not a contract. Both asset folders are intentional and must be retained during cleanup.

Database operations open their own SQL connection per operation. Each HTTP attempt uses a new request with scenario-specific URL, authentication header, and XML body. Scenario data is not stored in global logging context; Radar logs retain build-level context and structured scenario identifiers without logging request/response XML or exception details. The final report is written once after all cases complete as `Radar_<BuildId>.csv`; report writes use a temporary file before final replacement. ICE remains separate and its single-workload execution and business behavior are unchanged.

The live workload is explicit and requires configured database access, Radar endpoints and credentials, approved XSD files, and pipeline inputs:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --filter "FullyQualifiedName~RadarValidationTests" -- NUnit.ExplicitMode=Relaxed
```

Radar run inputs live under `PipelineSettings` in `PricingValidationFramework.Tests/appsettings.json` (with local threshold examples in `appsettings.Development.json`). Set `MinThreshold` and `MaxThreshold` explicitly for live runs; empty values are rejected. An empty `TestTag` selects all scenarios, and an empty `RequestTime` generates the current UTC time. A supplied request time must use `yyyy-MM-ddZHH:mm:ss`.

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

The chosen agent must reach the database and external endpoints. Radar also needs approved XSD files under `PricingValidationFramework.Tests/TestAssets/Xsd` matching the configured `ResponseXsdMappings`; the checked-in placeholders are not production schemas. Use an agent pool with the required network access if the hosted pool cannot reach those services.

## Run the ICE validation flow

The ICE flow is an explicit NUnit integration test. It loads passing baseline scenarios from SQL Server, calls ICE for each quote reference, compares the ICE premium with the XML baseline value using exact equality, writes a CSV report, and fails once all mismatches have been collected.

### Prerequisites

- .NET SDK 10.0, matching the projects' `net10.0` target framework.
- Network access to the SQL Server database and ICE endpoint.
- A SQL Server `TB_RESPONSE` table containing `PASS` baseline rows with valid `Quote_ref` and `Xml_response` values.
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

ICE reads `TB_RESPONSE` rows where `Status = 'PASS'`. For each product and scheme, it selects the most recently updated row, using `Scenario_id` as a tie-breaker.

Each selected row must supply:

- `Scenario_id`
- `Quote_ref`
- `Product_code`
- `Scheme_code`
- `Xml_response`
- `Status`
- `LastUpdated`

`Xml_response` must contain a baseline value in the XML format expected by `XmlValueExtractor`. ICE returns a JSON payload in the format expected by `JsonValueExtractor`.

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

The workload executes one NUnit test and iterates through all selected scenarios internally. It continues after value mismatches so the final report contains every scenario; an unexpected request, extraction, database, or certificate error stops the run immediately.

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

It contains `BuildId`, scenario and quote identifiers, product/scheme data, ICE value, baseline value, and `PASS` or `FAIL` result.

### Troubleshooting

| Symptom | Cause and action |
| --- | --- |
| `Could not find ... client-certificate.pfx` | Use an absolute `PfxCertificateFile` path, or add the PFX copy rule above and place the file under `TestAssets/Ice`. |
| Certificate cryptographic error | Confirm the PFX exists, the password is correct, and the certificate includes its private key. |
| HTTP 401 or 403 | Verify `ApiKeyHeaderName` and `ApiKeyHeaderValue` with the ICE provider. |
| TLS or handshake error | Verify the client certificate is authorized by ICE and that `IceEndpoint` uses the expected host and certificate chain. |
| SQL connection error | Verify `DatabaseSettings:ConnectionString`, network access, SQL authentication, and access to `TB_RESPONSE`. |
| No report appears | Resolve setup/request/extraction failures first. Reports are written only after all scenarios complete. |
| Test reports no scenarios | Check that `TB_RESPONSE` contains `PASS` rows. |

### Security checklist

- Never commit a PFX, certificate password, API key, or production connection string.
- Prefer an absolute certificate path on developer machines.
- Prefer Base64 certificate injection from a CI secret store in automated runs.
- Restrict read access to the certificate and configuration containing its password.