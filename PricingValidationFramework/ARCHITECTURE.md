# Pricing Validation Framework Architecture

## Status

This document describes the current architecture, naming, configuration structure, and responsibilities.

## 1. Folder Structure

```text
PricingValidationFramework.Core/
├── Configuration/
│   ├── DatabaseSettings.cs
│   ├── IceSettings.cs
│   ├── RadarRateLimitSettings.cs
│   ├── RadarEndpointSettings.cs
│   ├── RadarRouteSettings.cs
│   ├── RadarSettings.cs
│   └── RetrySettings.cs
├── Database/
│   ├── SqlConnectionFactory.cs
│   ├── IBaselineDataReader.cs
│   ├── RequestDataReader.cs
│   ├── BaselineDataReader.cs
│   └── ResultUpdater.cs
├── ExternalAPIAccess/
│   ├── ApiClients/
│   │   ├── RadarApiClient.cs
│   │   └── IceApiClient.cs
│   ├── UrlBuilders/
│   │   ├── RadarUrlBuilder.cs
│   │   ├── IceUrlBuilder.cs
│   │   └── RequestTimeFormatter.cs
│   └── Throttling/
│       ├── IRadarRequestRateLimiter.cs
│       └── RadarRequestRateLimiter.cs
├── Extraction/
│   ├── JsonValueExtractor.cs
│   └── XmlValueExtractor.cs
├── Logging/
│   ├── IceTestRunLogger.cs

NUnit ICE test
  -> Read BUILD_BUILDID at runtime
│   └── RadarTestRunLogger.cs
├── Matching/
│   └── FuzzyPricingMatcher.cs
├── Models/
│   ├── Common/
│   │   └── PipelineSettings.cs
│   ├── External/
│   │   └── RadarJsonResponse.cs
│   ├── Database/
│   │   ├── ScenarioRequest.cs
│   │   ├── IceBaselineScenario.cs
│   │   └── ScenarioResponse.cs
│   ├── Reporting/
│   │   ├── RadarValidationReportRow.cs
│   │   └── IceValidationReportRow.cs
│   └── Enums/
├── Reporting/
│   └── CsvReportWriter.cs
└── Validation/
    ├── PipelineInputValidator.cs
    ├── PricingComparisonService.cs
    └── RadarPricingService.cs

PricingValidationFramework.Tests/
├── Integration/
│   ├── Ice/
│   │   ├── IceTestSetup.cs
│   │   └── IceValidationTests.cs
│   └── Radar/
├── TestAssets/
│   ├── Ice/
│   ├── Radar/
│   └── Json/
├── TestResults/
│   ├── Reports/
│   └── Logs/
└── appsettings*.json
```

NUnit owns scenario discovery and scenario-level concurrency. The Radar fixture creates one test case per selected scenario and uses an assembly worker limit of four. `IceTestSetup` owns shared ICE fixture setup; ICE execution and business behavior remain separate from Radar. There is no application-level `ValidationOrchestrator` or internal Radar worker pool.

`TestResults/` is a runtime-generated output location, not a source-code folder. It must be excluded from source control. `Reports/` and `Logs/` must not exist as separate top-level folders outside `TestResults/`.

## 2. Class Responsibilities

### Configuration

- `DatabaseSettings`: owns `ConnectionString`.
- `RadarSettings`: owns endpoint settings and in-memory routes derived from the central mapping file. Appsettings contains endpoints, not product/scheme routes.
- `PricingProfileConfiguration`: loads `PricingProfiles:MappingFile` and referenced XPath files once, validates unique product/scheme mappings and Ice field references, and derives Radar routes. All product-specific configuration lives under `config/radar`. `PricingRouteConfiguration.LoadRoutes` reads only the routing part of the same file (product/scheme to endpoint and route key) without opening XPath files, for callers that never compare.
- `RadarEndpointSettings`: owns one physical endpoint's `BaseUrl`, `ApiKeyHeaderName`, and `ApiKeyValue`.
- `RadarRouteSettings`: owns one explicitly keyed route's `ProductCode`, `SchemeCodes`, `EndpointName`, and `RouteKey`.
- `IceSettings`: owns the ICE endpoint, API authentication, and mandatory client-certificate configuration, including local PFX or Key Vault PFX material.
- `RetrySettings`: owns configurable database and API retry counts and delay settings, including the maximum server-provided Radar `Retry-After` delay.
- `RadarRateLimitSettings`: configures the mandatory Radar request rate for each logical endpoint. Requests-per-second and queue limit must be positive.
- `PipelineInputValidator`: validates all pipeline inputs before database access, API calls, or report generation.

### Models

- `Models/Common/PipelineSettings`: owns runtime values `BuildId`, `TestTag`, `RequestTime`, `MinThreshold`, and `MaxThreshold`.
- `Models/Database/ScenarioRequest`: represents a scenario read from `[Xml].[Request]`.
- `Models/Database/IceBaselineScenario`: represents one passing `[Xml].[Response]` baseline selected for ICE processing, joined to `[Xml].[Request]` by `Scenario_id` for quote and product/scheme identifiers. It contains `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`, `XmlResponse`, `Status`, and `LastUpdated`.
- `Models/Database/ScenarioResponse`: represents baseline/result data associated with `[Xml].[Response]`.
- `Models/External/RadarJsonResponse`: represents the JSON envelope's top-level `response` XML string. `RadarApiClient` deserializes the envelope and normalizes its XML; raw XML responses are also supported.
- `Models/Reporting/RadarValidationReportRow`: represents one Radar scenario summary with its dynamic decimal field results.
- `Models/Reporting/DecimalFieldComparison`: represents a named configured field comparison, retaining its XPath and decimal values.
- `Models/Reporting/IceValidationReportRow`: represents one ICE report row.
- `Models/Enums/ScenarioResult`: shared scenario outcome enum for PASS, FAIL, and ERROR semantics.

### Database

### Database Access Technology

The database standard is:

- `Microsoft.Data.SqlClient` for SQL Server connectivity.
- `Dapper` for strongly typed SQL mapping and command execution.

The framework does not use Entity Framework, Entity Framework Core, Unit of Work, generic ORM wrappers, or generic repositories. This is a data-driven automation platform rather than a CRUD application. The CSV loader delegates SQL reads and transactional, parameterised multi-row inserts to the focused Core `ScenarioImportRepository` through `IScenarioImportRepository`; CSV parsing and import policy remain in the loader.

SQL remains explicit and visible. Reader and updater classes own their SQL directly.

Preferred Dapper operations are:

- `QueryAsync<T>()`
- `QuerySingleAsync<T>()`
- `QuerySingleOrDefaultAsync<T>()`
- `ExecuteAsync()`

All database operations use strongly typed model mapping, parameterized SQL, explicit column selection, and `CancellationToken` support. `SELECT *` is prohibited.

Filtering, grouping, ranking, and selection belong in SQL whenever practical. The application must not load large datasets only to group or rank them in memory. For ICE, SQL performs `Status = 'PASS'` filtering, `ProductCode`/`SchemeCode` grouping, `LastUpdated DESC` ranking, and latest-PASS selection. `BaselineDataReader` returns only the dataset required for execution.

`RequestDataReader` exposes only:

```text
GetAllScenariosAsync()
GetScenariosByProductCodeAsync(string productCode)
GetScenarioByIdAsync(string scenarioId)
```

The user-facing `TestTag` input is a product-code selector. If supplied, pass it to `GetScenariosByProductCodeAsync`, which filters `[Xml].[Request].Product_code = @ProductCode` across all schemes. Otherwise use `GetAllScenariosAsync`. A specific scenario uses `GetScenarioByIdAsync`. Stored `Test_tags` remains scenario metadata and NUnit categories, not a workload filter.

All queries must explicitly select:

```text
Scenario_id
Quote_ref
Scheme_code
Product_code
Xml_request
Test_tags
row_creationdate
```

`SELECT *` is prohibited.

`BaselineDataReader` reads baseline XML from `[Xml].[Response]` and joins `[Xml].[Request]` on unique `Scenario_id` for quote and product/scheme identifiers. It owns baseline retrieval separately from `RequestDataReader`, which reads only `[Xml].[Request]`.

`IBaselineDataReader` is the database boundary for baseline retrieval and supports isolated tests with a fake reader. `BaselineDataReader.GetPassingBaselineScenariosAsync()` reads `[Xml].[Response]`, filters `Status = 'PASS'`, and returns the latest `IceBaselineScenario` per `ProductCode` and `SchemeCode`, ordered by `Last_updated DESC` and `Scenario_id`. Filtering, grouping, and ranking are performed in SQL.

`ResultUpdater.InsertBaselineAsync()` inserts a new scenario's first Radar response as its `PASS` baseline, guarded so it never replaces an existing row. `ResultUpdater.UpdateResultAsync()` updates `[Xml].[Response]` by unique `Scenario_id`, preserving `row_creationdate`/`row_createdby` and requiring exactly one affected row. On PASS it updates `Status`, `Last_updated` and `XML_Response`, so the Radar response becomes the new baseline. On FAIL it updates `Status` only, so the existing baseline XML is never overwritten. Both also set `row_modifieddate` and `row_modifiedby`.

Audit columns: both tables have `row_creationdate`, `row_createdby`, `row_modifieddate` and `row_modifiedby` (there is no `Create_date`). Inserts set all four; updates set the two `row_modified*` columns. Dates are `SYSUTCDATETIME()`. The user is `DatabaseSettings:AuditUser` when configured (e.g. `testuser`), otherwise SQL Server's `SUSER_SNAME()`, the login running the statement. Reads expose `row_creationdate` as `CreatedDate`, and Radar's scenario list is ordered by it. `BuildId` is supplied at runtime for reporting and logging; `[Xml].[Response]` does not require a build-ID column.

### External API access

- `ExternalAPIAccess/ApiClients/RadarApiClient`: Radar HTTP boundary that sends XML POST requests with endpoint-specific authentication, retries transient failures, honors `Retry-After`, and preserves cancellation.
- `ExternalAPIAccess/ApiClients/IceApiClient`: sends requests to ICE using a fully built URL, ICE authentication, and a client certificate. It does not construct URLs, append `QuoteRef`, or perform URL composition.
- `ExternalAPIAccess/UrlBuilders/RadarUrlBuilder`: URL-composition component that receives `BaseUrl`, `RouteKey`, and formatted request time. It does not resolve configuration, query `[Xml].[Request]`, or apply auth.
- `ExternalAPIAccess/UrlBuilders/IceUrlBuilder`: owns ICE URL composition.
- `ExternalAPIAccess/UrlBuilders/RequestTimeFormatter`: owns Radar request-time validation and formatting.
- `ExternalAPIAccess/Throttling/RadarRequestRateLimiter`: owns one independent sliding-window limiter per logical Radar endpoint for every outbound HTTP attempt, including retries.

`IceApiClient` is the concrete ICE HTTP boundary. `CsvReportWriter` is the concrete CSV output service shared by Radar and ICE. No additional interface is required for either single implementation.

API clients own HTTP communication only. `RadarApiClient` acquires a permit for the route's logical endpoint immediately before each outbound HTTP attempt and creates an independent `HttpRequestMessage` for that attempt. The endpoint-limiter registry is shared by every parallel Radar scenario; each endpoint has an independent budget. These components are grouped under `ExternalAPIAccess` because they support external service communication.

### Radar support

- `RadarPricingService`: resolves the API route, verifies a product/scheme pricing profile exists, validates the baseline before the HTTP call, calls Radar, then delegates the two-XML comparison to Core.
- `PricingComparisonService`: resolves a product/scheme profile, extracts both documents using the same named XPaths, and returns field-level and overall outcomes. Ice reuses its profile lookup and single-field baseline extraction.
- `XmlValueExtractor`: parses stored/raw XML safely and extracts exactly one decimal element or attribute per configured field. No XSD validation remains.
- `RadarTestRunLogger`: writes Radar-specific logs.

### Matching

- `FuzzyPricingMatcher`: compares scenario-local `PricingDocument` values by configured field name with inclusive `MinThreshold <= Actual - Expected <= MaxThreshold` bounds. Paths remain available for reporting.

Only configured fields are extracted. Missing, ambiguous or non-numeric values produce extraction ERRORs, even if absent on both sides; unrelated XML is ignored. Ice extracts only its selected field and keeps exact equality.

### ICE support

- `JsonValueExtractor`: extracts the ICE Premium value.
- `IceTestRunLogger`: writes ICE-specific logs.

## 3. Data-Driven Execution Model

Radar execution is driven by `[Xml].[Request]`. ICE execution is driven by passing baseline records from `[Xml].[Response]`, joined to `[Xml].[Request]` for identifiers; ICE does not load request XML or execute Radar requests. Neither flow uses hardcoded test cases or static scenario definitions.

Each selected database record represents one scenario execution unit. `RequestDataReader` is the Radar scenario source, and `BaselineDataReader` is the ICE scenario source. Radar uses NUnit `TestCaseSource` so each selected scenario appears as a separate test case. Each Radar case receives an independent NUnit result; one consolidated report is written after the fixture completes. ICE remains separate and is not changed by Radar parallel execution.

### Radar flow

```text
[Xml].[Request]
  -> ScenarioRequest
  -> Trim ProductCode and SchemeCode
  -> NUnit test loads baseline XML and calls RadarPricingService
  -> Resolve exactly one API route by ProductCode + SchemeCode
  -> Select the referenced XPath file from the shared mapping
  -> Extract configured baseline values
  -> Call Radar using route EndpointName + RouteKey
  -> Extract response values using the same XPaths into a scenario-local PricingDocument
  -> FuzzyPricingMatcher compares matching field names
  -> Return scenario outcome and field details to the test
  -> Test persists overall PASS/FAIL and spools the completed report row to disk
  -> Teardown streams one flat CSV with XML only on the first row per scenario
```

`config/radar/mappings.json` owns each profile's ID, product, schemes, endpoint reference, route key, paths filename and Ice field. Referenced files under `XPathProfiles` contain only named XPath expressions. Setup loads them once and rejects missing/invalid mappings before HTTP resources are created. The 16 shipped files are editable templates, not production contracts. Multiple mappings may share a paths file.

Radar writes one row per configured field, repeating scenario identifiers and overall result. Request, baseline and API XML appear only on the first row of each scenario. A scenario with no comparisons still gets an outcome row. Completed results are held in a disk spool with only IDs and offsets indexed in memory. Teardown streams scenario-sorted results; disposal deletes the spool. A 20,000-scenario regression workload covers this path. NUnit retains request test cases separately.

Scenario discovery preserves the existing test-tag filter and rejects duplicate `ScenarioId` values before execution. Test names include only a sanitized, bounded `ScenarioId`; discovery errors become one visible failed discovery case. The Radar fixture uses `[Parallelizable(ParallelScope.Children)]`, with `[assembly: LevelOfParallelism(4)]`. No internal worker pool or scenario scheduler exists.

### ICE flow

```text
[Xml].[Response] with Status = PASS, joined to [Xml].[Request] by Scenario_id
  -> Newest IceBaselineScenario per product/scheme
  -> Database-only discovery creates TestCaseSource entries
  -> OneTimeSetUp initializes shared ICE client and certificate
  -> Each non-parallel NUnit case performs ICE comparison
  -> Record report row before asserting that case
  -> OneTimeTearDown writes one consolidated report and disposes resources
```

Adding or changing Radar scenarios is a data and configuration concern, not a code change. ICE scenario units are selected from passing `[Xml].[Response]` data by `BaselineDataReader`.

## 4. Configuration Models

### DatabaseSettings

```text
ConnectionString
```

### RadarSettings and RadarEndpointSettings

`RadarSettings.Endpoints` contains entries keyed by explicit physical endpoint codes such as `Endpoint1` and `Endpoint2`.

`RadarSettings.Routes` is populated in memory from the central mappings, keyed by profile ID. It is not read from appsettings.

`PricingProfiles:MappingFile` points to the central mapping file. Each mapping references a separate XPath dictionary used against both baseline and API XML. Named fields and routing are not duplicated in appsettings.

Each `RadarEndpointSettings` entry contains:

```text
BaseUrl
ApiKeyHeaderName
ApiKeyValue
```

Each `RadarRouteSettings` entry contains:

```text
ProductCode
SchemeCodes
EndpointName
RouteKey
```

Radar request XML is sent unchanged. Configured decimal fields are extracted from both the stored baseline and API XML; no full-document schema validation is performed.

Runtime selection trims the database ProductCode and SchemeCode values, then performs case-sensitive field matching. Exactly one route must match. Zero or multiple matches are scenario ERRORs.

`RadarSettings` contains no lookup, validation, or routing behavior. New combinations require configuration changes only; no switch statements or product-specific branches are permitted.

The route dictionary key is not used to match database scenario values. The selected route's `RouteKey` is written to the `KeyName` URL query parameter.

`ApiKeyValue` is a secret in UAT and must come from Azure Key Vault.

### IceSettings

```text
IceEndpoint
ApiKeyHeaderName
ApiKeyHeaderValue
CertificateHost
PfxCertificateFile
CertificatePassword
PfxCertificateBase64
```

Client certificates are mandatory for ICE. `CertificateHost`, `PfxCertificateFile`, and `CertificatePassword` are required configuration concepts. In UAT, the PFX content and password are supplied at runtime from Azure Key Vault rather than from JSON configuration.

### Models/Common/PipelineSettings

```text
BuildId
TestTag
RequestTime
MinThreshold
MaxThreshold
```

Sources:

- `BuildId`: Azure DevOps.
- `TestTag`: `PipelineSettings:TestTag` or the `TEST_TAG` pipeline override; the value identifies `[Xml].[Request].Product_code`.
- `RequestTime`: `PipelineSettings:RequestTime` or the `RADAR_REQUEST_DATETIME` pipeline override.
- `MinThreshold`: `PipelineSettings:MinThreshold` or the `RADAR_MIN_THRESHOLD` pipeline override.
- `MaxThreshold`: `PipelineSettings:MaxThreshold` or the `RADAR_MAX_THRESHOLD` pipeline override.
- `BaselineOnly`: `PipelineSettings:BaselineOnly` or the `RADAR_BASELINE_ONLY` pipeline override (`true`/`false`, default false). When true, scenarios with an existing `[Xml].[Response]` row are skipped and only missing baselines are created.

Environment overrides take precedence over appsettings. Thresholds must be supplied for live runs; empty tag and request time select all scenarios and generate UTC request time respectively.

Required values:

- `BuildId` must be present.
- `MinThreshold` and `MaxThreshold` must be valid decimals.
- `MinThreshold` must be less than or equal to `MaxThreshold`.

Optional values:

- `TestTag` selects all request scenarios with the supplied product code when supplied, regardless of their scheme or stored `Test_tags`.
- `RequestTime` must match `yyyy-MM-ddZHH:mm:ss` when supplied. The `Z` is a literal separator in this business format, not a UTC offset suffix.

`PipelineInputValidator` runs before any database access, API call, report generation, or log generation. Invalid input fails execution immediately with clear validation messages.

`RequestTimeFormatter` validates and preserves a supplied value. When no value is supplied, it generates the current UTC time and formats it as `yyyy-MM-ddZHH:mm:ss`.

### RetrySettings

```text
DatabaseRetryCount
DatabaseRetryDelaySeconds
ApiRetryCount
ApiRetryDelaySeconds
ApiRetryAfterMaxDelaySeconds
```

Retry counts mean the number of retries after the initial attempt. Delay values are base delays for exponential backoff. `ApiRetryAfterMaxDelaySeconds` caps a valid server-provided Radar `Retry-After` delay; local exponential backoff is not capped by this setting. Retry settings are configurable and are not hard-coded in clients or database classes. The configured default cap is 60 seconds.

Recommended starting values are three retries and a two-second base delay for both database and API operations. Backoff should be capped at 30 seconds with bounded jitter. These are operational defaults and remain configurable through the environment settings.

### RadarRateLimitSettings

```text
RequestsPerSecond
```

Radar limiting is mandatory and cannot be disabled. Checked-in configuration uses `RequestsPerSecond = 2`, giving a minimum 500 ms gap between HTTP starts across all Radar endpoints combined. A value of 4 gives 250 ms. One shared asynchronous gate uses monotonic elapsed time and starts each initial attempt or retry before releasing the gate, without waiting for the response. There are no catch-up bursts, separate endpoint budgets, bounded rejection queue, or `QueueLimit` setting. NUnit's four scenario workers bound the callers waiting asynchronously with cancellation support. The budget is local to one run and is not shared with ICE or other processes.

## 5. Validation and Comparison Rules

### Shared scenario result model

ICE and Radar share the same scenario result vocabulary:

```text
PASS = validation completed and passed
FAIL = validation completed but business rule failed
ERROR = validation could not be completed because of technical or operational failure
Cancellation = cancellation is not an ERROR result and is allowed to propagate as OperationCanceledException
```

`Models/Enums/ScenarioResult` defines:

```text
Pass
Fail
Error
```

### ICE validation rule

ICE does not use thresholds and does not calculate or report a `Difference` value.

ICE validation is exact equality:

```text
passed = iceValue == baselineValue
```

If comparison cannot complete because of a technical failure, the row is written with `ScenarioResult.Error` and nullable values. Cancellation remains cancellation and is not converted into an ERROR result.

### Radar comparison model

```text
Baseline XML + Radar response XML
  -> Extract configured fields using the same XPath file
  -> Build shared PricingDocument values for each XML
  -> FuzzyPricingMatcher compares decimal fields by configured name
  -> PricingComparisonResult (field results + overall result)
  -> RadarValidationReportRow
  -> ResultUpdater
  -> Flat CSV field rows, with payloads only on the first row per scenario
```

Pricing comparison and XPath extraction live in Core. NUnit loads the baseline, invokes the service, persists eligible PASS/FAIL results and asserts the outcome. There is no whole-document schema validation. Initial-baseline creation still stores the API response without extraction or comparison.

ICE comparison remains in the ICE test and is unchanged.

Each discovered decimal field passes when:

```text
Delta = Actual - Expected
Delta >= MinThreshold
AND
Delta <= MaxThreshold
```

Both bounds are inclusive. All compared fields must pass. An empty comparison or an extraction failure is ERROR, not PASS/FAIL persistence. Repeated elements require explicit one-based positions or identifying predicates in the mapping. Each XPath must select exactly one element or attribute.

No percentage difference or zero-baseline special rule is used. A baseline value of zero is valid because the comparison is subtraction-based.

Retries are infrastructure concerns only. A retry must not convert a threshold mismatch, extraction error or configuration error into a pass.

### Financial data types

Radar expected values, actual values, deltas, and min/max bounds use `decimal`. ICE premium values and baselines remain `decimal`.

`decimal` is required because these values represent financial amounts and tolerance values. It provides base-10 arithmetic that is appropriate for currency calculations and avoids the binary floating-point rounding behavior of `float` and `double`.

## 6. Configuration Files

### appsettings.json

Shared structure and non-secret defaults only:

```json
{
  "DatabaseSettings": {
    "ConnectionString": ""
  },
  "RadarSettings": {
    "Endpoints": {}
  },
  "PricingProfiles": {
    "MappingFile": "Radar/mappings.json"
  },
  "IceSettings": {
    "IceEndpoint": "",
    "ApiKeyHeaderName": "",
    "CertificateHost": "",
    "PfxCertificateFile": ""
  },
  "RetrySettings": {
    "DatabaseRetryCount": 3,
    "DatabaseRetryDelaySeconds": 2,
    "ApiRetryCount": 3,
    "ApiRetryDelaySeconds": 2,
    "ApiRetryAfterMaxDelaySeconds": 60
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2
  }
}
```

Each central mapping declares profile ID, product/schemes, Radar routing, paths file and Ice field. XPath files contain named expressions only. Separate mappings may share a route key or paths file. The endpoint and runtime settings below remain environment-specific.

### appsettings.Development.json

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=localhost;Database=PricingValidation;Trusted_Connection=True;"
  },
  "RadarSettings": {
    "Endpoints": {
      "Endpoint1": {
        "BaseUrl": "https://placeholder-radar-1.example.com/quote",
        "ApiKeyHeaderName": "PLACEHOLDER-HEADER-1",
        "ApiKeyValue": "development-placeholder-value-1"
      },
      "Endpoint2": {
        "BaseUrl": "https://placeholder-radar-2.example.com/quote",
        "ApiKeyHeaderName": "PLACEHOLDER-HEADER-2",
        "ApiKeyValue": "development-placeholder-value-2"
      }
    }
  },
  "IceSettings": {
    "IceEndpoint": "https://localhost/ice/quote",
    "ApiKeyHeaderName": "X-ICE-API-KEY",
    "ApiKeyHeaderValue": "development-ice-key",
    "CertificateHost": "ice.localhost",
    "PfxCertificateFile": "TestAssets/Ice/client-certificate.pfx",
    "CertificatePassword": "development-certificate-password"
  },
  "RetrySettings": {
    "DatabaseRetryCount": 3,
    "DatabaseRetryDelaySeconds": 2,
    "ApiRetryCount": 3,
    "ApiRetryDelaySeconds": 2,
    "ApiRetryAfterMaxDelaySeconds": 60
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2
  }
}
```

### appsettings.Uat.json

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=uat-db;Database=PricingValidation;Trusted_Connection=True;"
  },
  "RadarSettings": {
    "Endpoints": {
      "Endpoint1": {
        "BaseUrl": "https://placeholder-radar-1.example.com/quote",
        "ApiKeyHeaderName": "PLACEHOLDER-HEADER-1",
        "ApiKeyValue": "development-placeholder-value-1"
      },
      "Endpoint2": {
        "BaseUrl": "https://placeholder-radar-2.example.com/quote",
        "ApiKeyHeaderName": "PLACEHOLDER-HEADER-2",
        "ApiKeyValue": "development-placeholder-value-2"
      }
    }
  },
  "IceSettings": {
    "IceEndpoint": "https://uat-ice.example.com/quote",
    "ApiKeyHeaderName": "X-ICE-API-KEY",
    "CertificateHost": "ice.example.com"
  },
  "RetrySettings": {
    "DatabaseRetryCount": 3,
    "DatabaseRetryDelaySeconds": 2,
    "ApiRetryCount": 3,
    "ApiRetryDelaySeconds": 2,
    "ApiRetryAfterMaxDelaySeconds": 60
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2
  }
}
```

UAT must not contain Radar API key values, the ICE API key value, certificate passwords, or certificate material.

## 7. Azure Key Vault Structure

Recommended configuration keys:

```text
RadarSettings--Endpoints--Endpoint1--ApiKeyValue
RadarSettings--Endpoints--Endpoint2--ApiKeyValue
RadarSettings--Endpoints--Endpoint3--ApiKeyValue
IceSettings--ApiKeyHeaderValue
IceSettings--CertificatePassword
IceSettings--PfxCertificateBase64
```

The Key Vault provider should be loaded after JSON configuration so secrets populate or override the missing UAT values.

The certificate should be stored as a base64-encoded PFX secret with a separate password secret.

## 8. External API URL Building

URL builders are grouped under `ExternalAPIAccess/UrlBuilders` because they exist only to support Radar and ICE HTTP communication. API clients never build URLs.

`RadarUrlBuilder` owns URL composition. It receives:

```text
BaseUrl
RouteKey
RequestTime
```

Rules:

- Use the supplied `RequestTime` when present.
- Otherwise use the current time from an injected clock.
- Use UTC unless Radar requires another timezone.
- Format the timestamp in `RequestTimeFormatter`.
- URL-encode parameter values.
- Keep timestamp formatting out of `RadarApiClient`.

`RadarApiClient` sends the XML request and applies:

```text
RadarSettings.Endpoints[routeSettings.EndpointName].ApiKeyHeaderName
RadarSettings.Endpoints[routeSettings.EndpointName].ApiKeyValue
```

Radar requests use `POST`, `Content-Type: application/xml`, and `Accept: application/xml`. The workload-level `HttpClient` is configured once with automatic gzip, deflate, and brotli decompression. Radar retries HTTP 408, 429, 500, 502, 503, and 504 plus statusless transient network failures using `RetrySettings`. A valid `Retry-After` delta-seconds or HTTP-date is capped by `ApiRetryAfterMaxDelaySeconds`, then combined with local exponential backoff using the longer delay. When capped, the warning records the supplied value, configured maximum, and effective delay. Each attempt consumes a permit from its logical endpoint's limiter.

`RouteKey` is used during URL construction and remains distinct from the API key secret.

`IceUrlBuilder` owns ICE URL composition. It receives:

```text
IceSettings.IceEndpoint
Scenario.QuoteRef
```

Responsibilities:

- Compose the final ICE request URL.
- URL-encode values when required.
- Centralize ICE URL generation.

For example:

```text
IceEndpoint: https://ice.company.com/quote/
QuoteRef: ABC123
Final URL: https://ice.company.com/quote/ABC123
```

`IceApiClient` receives the fully built URL. It does not build URLs, append `QuoteRef`, or perform URL composition.

## 9. Radar Execution Flow

```text
NUnit test
  -> PipelineInputValidator
  -> PipelineSettings
    -> RequestDataReader
    -> [Xml].[Request] scenarios
    -> Trim ProductCode and SchemeCode
    -> Match exactly one configured route by fields
    -> Resolve selected route EndpointName
    -> BaseUrl, ApiKeyValue
    -> RadarUrlBuilder
    -> RadarApiClient
    -> Radar XML response
    -> RadarPricingService
    -> Resolve API route + pricing profile independently
    -> BaselineDataReader provides [Xml].[Response] baseline XML
    -> Extract baseline and Radar decimals using the shared XPath file
    -> Match baseline/response decimal values by full XML path
    -> Compare each Actual - Expected against inclusive delta range
    -> UpdateResultAsync: PASS writes status + Radar response XML; FAIL writes status + build ID only
    -> Radar CSV report
    -> Radar log
```

Adding a product and scheme endpoint combination requires only a new `RadarSettings.Endpoints` entry. No switch statements or product/scheme-specific branching are permitted.

## 10. ICE Execution Flow

```text
NUnit test
  -> BaselineDataReader
  -> [Xml].[Response] passing baseline scenarios joined to [Xml].[Request] for identifiers
  -> IceBaselineScenario
  -> QuoteRef
  -> IceUrlBuilder
  -> Final ICE URL
  -> IceSettings authentication and certificate
  -> In-memory client certificate
  -> IceApiClient
  -> JSON response
  -> JsonValueExtractor
  -> ICE Premium
  -> XmlValueExtractor
  -> Baseline TotalAmount
  -> ICE validation: IceValue == BaselineValue
  -> ICE CSV report
  -> ICE log
```

ICE must not:

- Resolve `SchemeCode`.
- Read `RadarSettings`.
- Use Radar endpoint settings or Radar authentication.
- Perform pricing extraction or comparison.
- Call `ResultUpdater`.
- Update `[Xml].[Response]`.
- Store results in the database.

ICE obtains baseline XML from `[Xml].[Response]` and identifiers from the linked `[Xml].[Request]`; it does not load request XML or write database results.

## 11. Reporting and Logging

### Radar report

`Models/Reporting/RadarValidationReportRow` contains:

```text
BuildId
ScenarioId
QuoteRef
SchemeCode
ProductCode
ProfileId
FieldComparisons
FailureStage
Error
MinThreshold
MaxThreshold
Result
```

### ICE report

`Models/Reporting/IceValidationReportRow` contains:

```text
BuildId
ScenarioId
QuoteRef
SchemeCode
ProductCode
IceValue
BaselineValue
Result
ProfileId
FieldName
XPath
Error
```

The `Result` field serializes to `PASS`, `FAIL`, or `ERROR`. Ice also reports `ProfileId`, `FieldName`, `XPath` and `Error`. It has no difference/threshold columns or XML payload columns.

`CsvReportWriter` writes the ICE report to `Ice_<BuildId>.csv`.

ICE report field ownership:

- From `[Xml].[Request]` linked by `Scenario_id`: `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`.
- From ICE: `IceValue`.
- From baseline XML: `BaselineValue`.
- Calculated in `IceValidationTests`: `Result`.

ICE validation is exact equality and does not calculate or report `Difference` values.

Radar CSV is flat with the header `ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml`. Difference is API minus baseline. There are no summary or record-type rows. XML appears only on the first row per scenario; remaining rows refer back by ScenarioId. Outcomes without comparisons still produce one row. Completed payloads are spooled and streamed to the atomic final CSV.

ICE and Radar use the same operational logging standard but different report columns.

Radar and ICE produce separate CSV reports and separate log files.

Radar logs contain:

- Lifecycle start and completion
- API retry attempts
- Operational exceptions
- Cancellation events

Radar logs do not include request or response XML, API-key values, validation outcomes, report rows, or financial values. Radar logs are written to `Radar_<BuildId>.log`; ICE logs remain in `Ice_<BuildId>.log`.

ICE logs contain:

- Lifecycle start and completion
- API retry warnings
- Operational exceptions
- Cancellation events
- Certificate, database, and HTTP failures
- Unexpected execution diagnostics

ICE logs do not include PASS/FAIL/ERROR outcome values, report rows, or validation summaries.

### Output structure and naming

Each execution writes runtime-generated artifacts to:

```text
TestResults/
├── Reports/
│   ├── Radar_<BuildId>.csv
│   └── Ice_<BuildId>.csv
└── Logs/
    ├── Radar_<BuildId>.log
    └── Ice_<BuildId>.log
```

These report and log names are mandatory. Radar and ICE output remains separate.

`TestResults/` is not part of the source-code structure and must be excluded from source control. Separate top-level `Reports/` and `Logs/` folders are not permitted.

### Extraction failure handling

Baseline extraction fails before the Radar call. Response extraction errors occur after the call. Both are technical ERROR results: log the field/path diagnostics, report the scenario, do not update the baseline, and continue with other cases. Initial-baseline creation deliberately bypasses extraction.

Local outputs are written under the test output's `TestResults` directory. Both pipelines stage Reports and Logs and publish them with `always()` conditions, including after test failures. Missing outputs are surfaced after available files are staged. Cancellation publication is best-effort; process/agent loss cannot guarantee files. Routine logs exclude credentials and XML; restrict access to the CSV payloads.

## 12. Pricing Mapping Storage

Files are stored under:

```text
PricingValidationFramework.Tests/
└── TestAssets/
    └── Radar/
        ├── mappings.json
        └── XPathProfiles/
            ├── Profile01.json
            └── ... Profile16.json
```

Mappings contain product/scheme selection, routing, paths file reference and Ice field selection. Paths files contain only a `fields` dictionary. Appsettings retains the mapping-file location and environment/runtime settings, not duplicate routes. URLs/credentials stay outside assets. Files are copied to output and loaded once per setup. Namespace-prefix configuration is not provided; expressions must work with the actual XML. See README for the editable template format and production setup checklist.

## 13. Certificate Strategy

### Development

- Read the PFX path from `PfxCertificateFile`.
- Read the password from development configuration or local secrets.
- Load the certificate into memory.
- Attach it to an ICE-specific `HttpClientHandler`.
- Do not install the certificate into the Windows certificate store.

### UAT

- Load base64-encoded PFX content from Azure Key Vault.
- Load the certificate password from Azure Key Vault.
- Construct the certificate in memory.
- Attach it to the ICE handler.
- Do not write the certificate to disk.
- Do not install it into the agent certificate store.

The certificate is scoped to ICE only. Radar does not use it.

## 14. Retry Strategy

### Database retries

Database retry behavior applies to `SqlConnectionFactory`, `RequestDataReader`, `BaselineDataReader`, and `ResultUpdater`.

Retry only transient failures, including SQL timeouts, connection timeouts, deadlocks, temporary SQL outages, network interruptions, and connection-pool exhaustion. Do not retry invalid SQL, missing tables or columns, constraint violations, or other permanent query/design errors.

Use the configured retry count and exponential backoff based on `DatabaseRetryDelaySeconds`. Add bounded jitter to prevent many workers from retrying simultaneously. Cancellation stops the retry sequence immediately. Every retry attempt is written to the active flow-specific log.

`ResultUpdater` is included in this policy. Its updates are scoped by `Scenario_id` and set absolute values, so a retried transient failure cannot create duplicate logical results.

Read operations may retry the same read operation because they do not mutate state. Update operations may retry only the same idempotent, scenario/build-scoped database command after a transient database failure. An update retry never reruns API calls, extraction, matching, or the scenario workflow.

### API retries

API retry behavior applies to `RadarApiClient` and `IceApiClient`.

Retry HTTP 408, 429, 500, 502, 503, and 504, plus transient DNS, network, and socket failures. Do not retry HTTP 400, 401, 403, or 404. For a valid `Retry-After` delta-seconds or HTTP-date, cap the server delay at `ApiRetryAfterMaxDelaySeconds`, then wait for the maximum of local exponential backoff and the capped value. Missing or invalid values use local backoff; invalid values are logged without including the header value.

Use `ApiRetryCount` and exponential backoff based on `ApiRetryDelaySeconds`, with cancellation support. Retry attempts are written to the Radar or ICE log that owns the operation. Radar retry warnings contain status or transient-network category, attempt, maximum attempts, and delay only.

Database reads and result updates use the database retry policy; API calls use the API retry policy. Permanent SQL errors are never retried.

## 15. Parallel Execution and External API Throttling

### Execution model

NUnit owns Radar scenario discovery and concurrency. Each selected scenario is one NUnit case marked `[Parallelizable(ParallelScope.Children)]`; the assembly-level worker limit is four. There is no internal worker pool, `Task.WhenAll` across scenarios, or custom scenario scheduler. Each case delegates one scenario to Core's `RadarPricingService`. ICE remains separate and is not changed by Radar parallel execution.

### Radar throttling

One run-level `RadarRequestRateLimiter` is shared by every parallel Radar scenario and injected into `RadarApiClient`. A single `SemaphoreSlim` guards HTTP dispatch across all configured endpoints, whose names remain registered case-insensitively for validation. The minimum interval is calculated from `RequestsPerSecond`: 2 gives 500 ms and 4 gives 250 ms. Monotonic elapsed time is rechecked after asynchronous delays; the next interval is measured from actual dispatch, so scheduling delays never cause catch-up bursts. The gate is released before awaiting the response, allowing in-flight requests to overlap. NUnit's four workers bound the active callers; waiting is cancellable and there is no queue-full rejection. Separate processes and pipeline runs do not share the budget.

`RadarTestSetup` binds and validates the required settings, rejects case-insensitive duplicate endpoint keys, and verifies every route points to a configured endpoint before database discovery. It creates and disposes the limiter once for the Radar run. `RadarApiClient` creates an independent `HttpRequestMessage` and dispatches it through the shared gate for every outbound attempt. Each retry first waits for local backoff or valid `Retry-After` (whichever is longer), then enters that same pacing gate again. Exhausted HTTP 429 remains a technical ERROR without PASS/FAIL persistence. ICE is unchanged and does not use the Radar gate.

## 16. Cancellation Strategy

Azure DevOps cancellation is represented by a `CancellationToken` passed through the full execution path.

Cancellation applies to:

- `RequestDataReader`
- `BaselineDataReader`
- `ResultUpdater`
- `RadarApiClient`
- `IceApiClient`
- `PipelineInputValidator`
- `CsvReportWriter`
- `RadarTestRunLogger`
- `IceTestRunLogger`
- `IRadarRequestRateLimiter`

When cancellation is requested:

1. Let NUnit stop or cancel active scenario cases.
2. Cancel cases waiting for a Radar limiter permit.
3. Do not start additional API requests.
4. Allow safe in-flight operations to observe cancellation and finish or abort according to their API contract.
5. Write completed terminal report rows once from fixture teardown.
6. Dispose HTTP, certificate, database, and writer resources.
7. Record a cancellation event in the relevant flow-specific log.
8. Exit without treating cancellation as a normal scenario failure.

Cancellation must not be swallowed by retry policies or converted into a retryable transient failure.

## 17. Thread-Safe Reporting and Logging

Radar cases never write directly to a shared CSV. Each case creates one terminal `RadarValidationReportRow` locally and adds it to a fixture-level `ConcurrentDictionary` keyed by `ScenarioId`. A duplicate terminal row throws rather than replacing or silently dropping a result. NUnit waits for child cases before `OneTimeTearDown`; teardown snapshots rows in ordinal `ScenarioId` order, writes one temporary CSV with the existing writer, then atomically moves it to the sanitized `Radar_<BuildId>.csv` final path. Failed writes remove the temporary file, retain collected rows, log only the error type, and surface as teardown failures.

### Report ordering

Reports sort by ScenarioId, then Radar field name. XML appears on the first row per scenario; sorting/filtering can move or hide that row. Radar writes `Radar_<BuildId>.csv`; Ice writes `Ice_<BuildId>.csv`.

Parallel completion order does not affect report ordering. ICE reporting and logging remain separate and are not changed by this Radar implementation.

Database readers and `ResultUpdater` create and dispose an independent SQL connection per operation; no connection is shared across NUnit cases, and there is no transaction around an API call. `ResultUpdater` validates the affected row count and writes the latest response XML for PASS only; FAIL updates the status and build ID only. No scenario-specific state is stored in fixture-global or NLog global context.

NLog keeps separate ICE and Radar targets. Build identifiers may remain in `GlobalDiagnosticsContext`; scenario identifiers are structured event properties. Radar logging records exception type only and does not emit exception payloads, request/response XML, API keys, authorization headers, passwords, certificates, or connection strings. `IceTestSetup` does not call process-wide `LogManager.Shutdown` during fixture disposal, so it cannot terminate Radar logging in the same test process.

## 18. Updated Radar Execution Flow

```text
RequestDataReader
  -> Selected [Xml].[Request] scenarios and optional test-tag filter
  -> Reject duplicate ScenarioIds
  -> NUnit TestCaseSource: one Radar test case per scenario
  -> Up to four NUnit child cases execute concurrently
  -> Each case loads its baseline XML and delegates to Core RadarPricingService
  -> Resolve API route and exact product/scheme pricing profile separately
  -> Extract baseline XPath values before making the API request
  -> RadarApiClient acquires shared limiter permit per HTTP attempt
  -> Extract API values using the same XPath file into a scenario-local PricingDocument
  -> FuzzyPricingMatcher returns decimal field details and the overall result
  -> When an API response exists: PASS persists status + response XML; FAIL persists status + build ID only
  -> Add one summary row with its field details to the concurrent collection
  -> One flat Radar CSV writes dynamic field rows with XML once per scenario
```

Each case asserts its own outcome without preventing unrelated cases from completing. Baseline and Radar response values are extracted with the same configured XPaths; missing profiles are errors, not fallbacks. No whole-document validation is performed. Cancellation propagates separately. Ice shares profile resolution and single-field extraction, not Radar API execution.

## 19. Updated ICE Execution Flow

```text
IceTestSetup
  -> Load configuration and shared ICE dependencies
  -> BaselineDataReader
  -> [Xml].[Response] PASS scenarios joined to [Xml].[Request] for identifiers
  -> Latest PASS per ProductCode + SchemeCode
  -> One explicit IceValidationTests workload
  -> QuoteRef
  -> IceUrlBuilder
  -> Final ICE URL
  -> IceApiClient with certificate and transient retry policy
  -> JsonValueExtractor
  -> ICE Premium
  -> Resolve product/scheme mapping and its iceField
  -> XmlValueExtractor evaluates only that configured XPath
  -> Baseline Premium
  -> Exact value comparison
  -> IceValidationReportRow
  -> Independent NUnit assertion
  -> One Ice_<BuildId>.csv report per run
  -> ICE log
```

Ice reads the shared product/scheme mappings but does not use Radar credentials or throttling, call `ResultUpdater`, or update `[Xml].[Response]`. Its API JSON extraction and exact comparison are unchanged.

ICE uses `BaselineDataReader` as its only database reader, obtains identifiers through the `[Xml].[Request]` join without loading request XML, and performs comparison/assertion logic directly in the NUnit test.

ICE uses one named `TestCaseSource` case per selected baseline and remains explicitly `[NonParallelizable]`. Discovery loads only database/retry configuration and passing baseline data, without creating the API client or loading its certificate. Empty discovery, duplicate IDs, and discovery exceptions produce a failing discovery case. One-time setup creates the shared execution resources; each scenario records PASS/FAIL/ERROR before its independent assertion, and subsequent cases continue after a failed case. Teardown uses a non-cancelled reporting token to write one combined report, including completed rows after cancellation, and disposes resources in `finally`. Baseline selection, exact comparison, authentication, and retries are unchanged; ICE tag filtering remains pending.

## 20. Final Design Decisions

- `BuildId` is the required ICE report identifier.
- `TestTag` and `RequestTime` are optional pipeline inputs for Radar only; `TestTag` retains its user-facing name but filters `[Xml].[Request].Product_code`.
- `RequestTime` uses the literal-`Z` format `yyyy-MM-ddZHH:mm:ss`; missing values use current UTC time.
- ICE validation is exact equality: `IceValue == BaselineValue`.
- ICE does not use Radar thresholds, `MinThreshold`, `MaxThreshold`, or `FuzzyPricingMatcher`.
- ICE does not calculate or report `Difference` values.
- `IceValue` and `BaselineValue` are `decimal`.
- ICE reads `BUILD_BUILDID` at execution time for `BuildId` and report naming.
- ICE passes only when `IceValue` exactly equals `BaselineValue`.
- Equality comparison, assertion, and report-row construction remain owned by the NUnit ICE test.
- Radar is driven by `[Xml].[Request]`; ICE is driven by passing `[Xml].[Response]` baseline records selected by `BaselineDataReader`.
- ICE uses `IceBaselineScenario` records with identifiers joined from `[Xml].[Request]`; it does not load request XML.
- `IceUrlBuilder` owns ICE URL composition; `IceApiClient` receives a fully built URL and never appends `QuoteRef`.
- `RequestDataReader` reads only `[Xml].[Request]`; `BaselineDataReader` reads baseline XML from `[Xml].[Response]` joined to `[Xml].[Request]`; `ResultUpdater` updates `[Xml].[Response]` by unique `Scenario_id`.
- ICE does not call `ResultUpdater` or update `[Xml].[Response]`.
- `IceValidationTests` is the ICE pipeline orchestrator; no separate ICE application orchestrator is required.
- ICE runs one named, sequential NUnit case per selected baseline through `TestCaseSource`, with independent assertions and one combined report in fixture teardown.
- Database and API retries are transient-only, configurable, exponentially backed off, jittered, cancellation-aware, and logged by flow.
- `ResultUpdater` is retry-safe through `Scenario_id`-scoped updates that set absolute values.
- All DTOs, contracts, result models, report row models, and future enums belong under `Models`; service folders contain services only.
- Business failures, validation failures, mismatches, and configuration failures are never retried.
- NUnit owns Radar concurrency with four workers and one configurable staggered request budget shared by every endpoint and retry. There is no separate in-flight request limit; ICE cases remain sequential and do not use Radar pacing.
- Radar cases collect one terminal row each in a thread-safe keyed collection; fixture teardown writes one deterministic CSV. NLog remains flow-separated and scenario values use structured properties, not global context.
- Cancellation stops active work and limiter waits without turning cancellation into FAIL or ERROR.
- Radar uses one shared header name and endpoint-group-specific API key values.
- ICE always requires a client certificate; there is no optional certificate flag.
- UAT Radar keys, ICE API key values, certificate passwords, and PFX content come from Azure Key Vault.
- UAT certificate material is loaded in memory and attached to an ICE-specific `HttpClientHandler`; agent certificate-store installation is not required.
- Radar and ICE produce separate reports and log files.
- Report and log names are fixed as `Radar_<BuildId>.csv`, `Ice_<BuildId>.csv`, `Radar_<BuildId>.log`, and `Ice_<BuildId>.log`.
- Pricing mappings and XPath files live in `config/radar` and are copied to `Radar/` in the Tests and DataLoader outputs; test fixtures stay under `Tests/TestAssets/Radar/Fixtures`.
- Baseline/response extraction failures are technical ERRORs. Log and report them without updating the baseline; continue with other scenarios.

NUnit owns scenario execution, schemes select Radar endpoint groups, endpoint groups own Radar credentials, and ICE remains an independent validation path.

## 21. Current Implementation Confirmation

The architecture reflects the current implementation:

- Folder structure and ownership boundaries are defined.
- Business naming is finalized for Radar, ICE, baseline data, validation, matching, reporting, and logging.
- Radar and ICE execution are data-driven with no hardcoded test cases or static scenario definitions; Radar uses `[Xml].[Request]`, while ICE uses passing `[Xml].[Response]` baselines.
- `IceUrlBuilder` centralizes ICE URL construction and `IceApiClient` owns only HTTP execution.
- `ExternalAPIAccess/ApiClients/` contains `RadarApiClient` and `IceApiClient`.
- `ExternalAPIAccess/UrlBuilders/` contains `RadarUrlBuilder`, `IceUrlBuilder`, and `RequestTimeFormatter`; API clients never build URLs.
- `ExternalAPIAccess/Throttling/` contains `IRadarRequestRateLimiter` and `RadarRequestRateLimiter`; Radar limiting is shared across parallel cases and ICE does not use it.
- Configuration separates scheme metadata, Radar endpoint groups, shared Radar authentication, ICE authentication, and certificate material.
- Azure Key Vault supplies UAT Radar keys, ICE secrets, and certificate material.
- ICE certificates are mandatory and loaded in memory without agent certificate-store installation.
- Radar endpoint grouping allows multiple schemes to share one endpoint group and credential.
- New schemes require a mapping, a matching XPath file and test data, not product-specific classes.
- NUnit tests orchestrate execution; no application-level orchestrator is required.
- Pipeline input validation occurs before database access, API calls, or output generation.
- Database responsibilities are separated between request reads, baseline reads, and result updates.
- ICE uses `BaselineDataReader.GetPassingBaselineScenariosAsync()` and `IceBaselineScenario`, with identifiers joined from `[Xml].[Request]` without loading request XML.
- Radar extraction failures are isolated to the current scenario and do not stop the run.
- Radar and ICE have separate reports, logs, output names, and flow-specific responsibilities.
- Mapping-file location is configurable; paths-file locations are relative to that file.
- NUnit owns Radar concurrency with four workers; there is no internal scenario worker pool.
- Radar rate limiting is mandatory with a configurable overall request rate, staggered starts, and no catch-up bursts; the checked-in 2 requests-per-second gives a minimum 500 ms gap.
- Every Radar HTTP retry enters the shared pacing gate after the longer of local backoff and valid `Retry-After`; one sorted report is written after scenario cases complete.
- Result update retry safety is defined through idempotent scenario/build-scoped updates.
- Radar and ICE report ordering is deterministic and independent of parallel scenario completion order.
- Radar reports include `SchemeCode` for endpoint-routing diagnostics.
- `PricingComparisonResult` carries Radar field details and the scenario-level result; ICE remains separate.
- Dapper database standards, explicit SQL ownership, and SQL-side filtering/ranking are documented.
- NUnit tests remain the orchestration and comparison layer; no additional abstractions were introduced.

The architecture is aligned with the current solution structure and implementation.
