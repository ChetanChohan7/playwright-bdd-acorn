# Pricing Validation Framework

## Run the Radar validation flow

The explicit Radar workload creates one NUnit test case per selected `TB_REQUEST` scenario. NUnit owns scenario concurrency with an assembly worker limit of four; there is no application worker pool. Each case loads its baseline, runs one scenario through `RadarScenarioProcessor`, persists the existing PASS or FAIL outcome, adds one terminal row to a thread-safe collection, and asserts its own result. A single consolidated CSV is written after all cases finish.

Every configured logical endpoint has its own mandatory `SlidingWindowRateLimiter`: at most 2 request starts per rolling second and up to 4 waiting attempts. `PricingA`, `PricingB`, and `PricingC` therefore have independent budgets; saturation on one endpoint does not consume another endpoint's capacity. Initial requests and retries use the same endpoint limiter. There is no separate in-flight request limit; NUnit remains the sole scenario-concurrency owner with four workers. Checked-in `RadarRateLimitSettings` uses `RequestsPerSecond: 2` and `QueueLimit: 4` and cannot be disabled.

HTTP 429 is a transient technical failure. When `Retry-After` contains valid delay-seconds or an HTTP date, the client caps that server delay at `RetrySettings:ApiRetryAfterMaxDelaySeconds` (60 seconds by default), then waits for the longer of the capped delay and local exponential backoff before acquiring another endpoint permit. A cap warning includes the supplied value, configured maximum, and effective delay. Missing or invalid headers use local backoff; invalid values are logged without the header content. Exhausted retries and a full endpoint queue produce technical ERROR results without normal PASS/FAIL persistence.

Radar responses are validated against the response XSD selected by `RadarSettings:ResponseXsdMappings`, keyed by the existing route identifier (`Route001`, etc.), then deserialized into the focused `RadarResponse` model. The model currently contains only the evidenced `TotalAmount`; raw response XML remains available to the existing report and persistence paths. No request XML schema validation is performed.

Response schema integration point: add each approved XSD under `PricingValidationFramework.Tests/TestAssets/Xsd`, then map its filename to the applicable route identifier in `ResponseXsdMappings`. Schema names are not derived from ProductCode or SchemeCode. `placeholder-response-schema.xsd` documents the currently known minimal shape only and is not a client-approved production schema. The `RadarSystemTest.xsd` file is a system-test fixture.

JSON integration point: place future client JSON contracts and representative payloads under `PricingValidationFramework.Tests/TestAssets/Json`. `RadarJsonResponse` is intentionally empty until the client contract arrives. The future path is JSON deserialization into that typed model, followed by model-based validation and reporting; the current Radar flow does not deserialize JSON. `placeholder-response.json` is an empty directory placeholder, not a contract. Both asset folders are intentional and must be retained during cleanup.

Database operations open their own SQL connection per operation. Each HTTP attempt uses a new request with scenario-specific URL, authentication header, and XML body. Scenario data is not stored in global logging context; Radar logs retain build-level context and structured scenario identifiers without logging request/response XML or exception details. The final report is written once after all cases complete as `Radar_<BuildId>.csv`; report writes use a temporary file before final replacement. ICE remains separate and its single-workload execution and business behavior are unchanged.

The live workload is explicit and requires configured database access, Radar endpoints and credentials, approved XSD files, and pipeline inputs:

```powershell
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --filter "FullyQualifiedName~RadarValidationTests"
```

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
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --no-restore --filter "FullyQualifiedName~IceValidationTests"
```

The workload executes one NUnit test and iterates through all selected scenarios internally. It continues after value mismatches so the final report contains every scenario; an unexpected request, extraction, database, or certificate error stops the run immediately.

To set the report build identifier, set `BUILD_BUILDID` before running. If it is not set, the report uses `local`.

```powershell
$env:BUILD_BUILDID = "20260923.1"
dotnet test .\PricingValidationFramework.Tests\PricingValidationFramework.Tests.csproj --no-restore --filter "FullyQualifiedName~IceValidationTests"
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