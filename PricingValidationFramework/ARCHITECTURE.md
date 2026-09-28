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
│   └── ThresholdMatcher.cs
├── Models/
│   ├── Common/
│   │   └── PipelineSettings.cs
│   ├── External/
│   │   └── RadarResponse.cs
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
    ├── XsdFileResolver.cs
    └── XsdValidator.cs

PricingValidationFramework.Tests/
├── Integration/
│   ├── Ice/
│   │   ├── IceTestSetup.cs
│   │   └── IceValidationTests.cs
│   └── Radar/
├── TestAssets/
│   ├── Ice/
│   ├── Radar/
│   ├── Xsd/
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
- `RadarSettings`: owns the case-sensitive `Endpoints` and `Routes` dictionaries as configuration data only.
- `RadarEndpointSettings`: owns one physical endpoint's `BaseUrl`, `ApiKeyHeaderName`, and `ApiKeyValue`.
- `RadarRouteSettings`: owns one explicitly keyed route's `ProductCode`, `SchemeCode`, `EndpointName`, and `RouteKey`.
- `IceSettings`: owns the ICE endpoint, API authentication, and mandatory client-certificate configuration, including local PFX or Key Vault PFX material.
- `RetrySettings`: owns configurable database and API retry counts and delay settings, including the maximum server-provided Radar `Retry-After` delay.
- `RadarRateLimitSettings`: configures the mandatory Radar request rate for each logical endpoint. Requests-per-second and queue limit must be positive.
- `PipelineInputValidator`: validates all pipeline inputs before database access, API calls, or report generation.

### Models

- `Models/Common/PipelineSettings`: owns runtime values `BuildId`, `TestTag`, `RequestTime`, `MinThreshold`, and `MaxThreshold`.
- `Models/Database/ScenarioRequest`: represents a scenario read from `TB_REQUEST`.
- `Models/Database/IceBaselineScenario`: represents one passing `TB_RESPONSE` baseline selected for ICE processing. It contains `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`, `XmlResponse`, `Status`, and `LastUpdated`.
- `Models/Database/ScenarioResponse`: represents baseline/result data associated with `TB_RESPONSE`.
- `Models/External/RadarResponse`: represents only the currently consumed Radar response field, `TotalAmount`.
- `Models/External/RadarJsonResponse`: empty placeholder for the future client JSON contract; it has no guessed fields or deserialization behavior.
- `Models/Reporting/RadarValidationReportRow`: represents one Radar report row.
- `Models/Reporting/IceValidationReportRow`: represents one ICE report row.
- `Models/Enums/ScenarioResult`: shared scenario outcome enum for PASS, FAIL, and ERROR semantics.

### Database

### Database Access Technology

The database standard is:

- `Microsoft.Data.SqlClient` for SQL Server connectivity.
- `Dapper` for strongly typed SQL mapping and command execution.

The framework does not use Entity Framework, Entity Framework Core, the Repository Pattern, Unit of Work, generic ORM wrappers, or generic data-access abstractions. This is a data-driven automation platform rather than a CRUD application.

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
GetScenariosByTestTagAsync(string testTag)
GetScenarioByIdAsync(string scenarioId)
```

If a test tag is supplied, use `GetScenariosByTestTagAsync`. Otherwise use `GetAllScenariosAsync`. A specific scenario uses `GetScenarioByIdAsync`.

All queries must explicitly select:

```text
Scenario_id
Quote_ref
Scheme_code
Product_code
Xml_request
Test_tags
Created_date
```

`SELECT *` is prohibited.

`BaselineDataReader` reads the baseline XML from `TB_RESPONSE`. It owns baseline retrieval separately from `RequestDataReader`, which reads only `TB_REQUEST`.

`IBaselineDataReader` is retained as the database boundary for baseline retrieval because future system-flow tests may substitute a mocked baseline reader. `BaselineDataReader.GetPassingBaselineScenariosAsync()` reads `TB_RESPONSE`, filters `Status = 'PASS'`, and returns the latest `IceBaselineScenario` per `ProductCode` and `SchemeCode`, ordered by `LastUpdated DESC`. Filtering, grouping, and ranking are performed in SQL.

`ResultUpdater` exposes:

- `UpdatePassResultAsync()`: updates `Status`, `BuildId`, `LastUpdated`, and `XmlResponse`.
- `UpdateFailResultAsync()`: updates `Status`, `BuildId`, and `LastUpdated`; it must not update `XmlResponse`.

### External API access

- `ExternalAPIAccess/ApiClients/RadarApiClient`: Radar HTTP boundary that sends XML POST requests with endpoint-specific authentication, retries transient failures, honors `Retry-After`, and preserves cancellation.
- `ExternalAPIAccess/ApiClients/IceApiClient`: sends requests to ICE using a fully built URL, ICE authentication, and a client certificate. It does not construct URLs, append `QuoteRef`, or perform URL composition.
- `ExternalAPIAccess/UrlBuilders/RadarUrlBuilder`: URL-composition component that receives `BaseUrl`, `RouteKey`, and formatted request time. It does not resolve configuration, query `TB_REQUEST`, or apply auth.
- `ExternalAPIAccess/UrlBuilders/IceUrlBuilder`: owns ICE URL composition.
- `ExternalAPIAccess/UrlBuilders/RequestTimeFormatter`: owns Radar request-time validation and formatting.
- `ExternalAPIAccess/Throttling/RadarRequestRateLimiter`: owns one independent sliding-window limiter per logical Radar endpoint for every outbound HTTP attempt, including retries.

`IceApiClient` is the concrete ICE HTTP boundary. `CsvReportWriter` is the concrete CSV output service shared by Radar and ICE. No additional interface is required for either single implementation.

API clients own HTTP communication only. `RadarApiClient` acquires a permit for the route's logical endpoint immediately before each outbound HTTP attempt and creates an independent `RestRequest` for that attempt. The endpoint-limiter registry is shared by every parallel Radar scenario; each endpoint has an independent budget. These components are grouped under `ExternalAPIAccess` because they support external service communication.

### Radar support

- `XsdFileResolver`: resolves the response XSD filename selected by the route-keyed `RadarSettings.ResponseXsdMappings` configuration.
- `XsdValidator`: validates Radar XML responses.
- `RadarTestRunLogger`: writes Radar-specific logs.

### Matching

- `ThresholdMatcher`: Radar-only service that evaluates a supplied `Difference` against inclusive `MinThreshold` and `MaxThreshold` bounds and returns a boolean outcome.

`ThresholdMatcher` does not calculate differences, build result objects, create report rows, or perform assertions. Difference calculation and report creation remain in the NUnit Radar test. ICE comparison is intentionally kept inside the NUnit ICE test and does not use this service.

### ICE support

- `JsonValueExtractor`: extracts the ICE Premium value.
- `IceTestRunLogger`: writes ICE-specific logs.

## 3. Data-Driven Execution Model

Radar execution is driven by `TB_REQUEST`. ICE execution is driven by passing baseline records from `TB_RESPONSE`; ICE does not read `TB_REQUEST`. Neither flow uses hardcoded test cases or static scenario definitions.

Each selected database record represents one scenario execution unit. `RequestDataReader` is the Radar scenario source, and `BaselineDataReader` is the ICE scenario source. Radar uses NUnit `TestCaseSource` so each selected scenario appears as a separate test case. Each Radar case receives an independent NUnit result; one consolidated report is written after the fixture completes. ICE remains separate and is not changed by Radar parallel execution.

### Radar flow

```text
TB_REQUEST
  -> ScenarioRequest
  -> Trim ProductCode and SchemeCode
  -> Match exactly one RadarSettings.Routes entry by ProductCode and SchemeCode
  -> Resolve the selected route's EndpointName
  -> BaseUrl, RouteKey, ApiKeyValue
  -> Radar URL construction
  -> Radar request authentication
  -> ValidateResponseXml using ResponseXsdMappings[RouteIdentifier], when mapped
  -> Deserialize RadarResponse.TotalAmount
  -> Radar response XSD validation before deserialization
  -> NUnit scenario assertion
  -> Thread-safe terminal-row collection
  -> One sorted consolidated Radar CSV after all cases finish
```

Scenario discovery preserves the existing test-tag filter and rejects duplicate `ScenarioId` values before execution. Test names include only a sanitized, bounded `ScenarioId`; discovery errors become one visible failed discovery case. The Radar fixture uses `[Parallelizable(ParallelScope.Children)]`, with `[assembly: LevelOfParallelism(4)]`. No internal worker pool or scenario scheduler exists.

### ICE flow

```text
TB_RESPONSE with Status = PASS
  -> IceBaselineScenario
  -> ICE processing
```

Adding or changing Radar scenarios is a data and configuration concern, not a code change. ICE scenario units are selected from passing `TB_RESPONSE` data by `BaselineDataReader`.

## 4. Configuration Models

### DatabaseSettings

```text
ConnectionString
```

### RadarSettings and RadarEndpointSettings

`RadarSettings.Endpoints` contains entries keyed by explicit physical endpoint codes such as `Endpoint1` and `Endpoint2`.

`RadarSettings.Routes` contains entries keyed by explicit user-supplied route codes such as `Route001` and `Route002`. Route dictionary keys have no business meaning and are not derived from scenario data.

`RadarSettings.ResponseXsdMappings` maps those existing route identifiers to response XSD filenames relative to `TestAssets/Xsd`. It is the only response schema registration point; schema names are not derived from ProductCode/SchemeCode and no schema count is fixed. Missing or empty mappings skip response schema validation.

Each `RadarEndpointSettings` entry contains:

```text
BaseUrl
ApiKeyHeaderName
ApiKeyValue
```

Each `RadarRouteSettings` entry contains:

```text
ProductCode
SchemeCode
EndpointName
RouteKey
```

Radar requests have no XSD validation. Response schemas are selected by `ResponseXsdMappings[routeIdentifier]`.

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
- `TestTag`: Azure DevOps.
- `RequestTime`: runtime input.
- `MinThreshold`: Azure DevOps pipeline variable.
- `MaxThreshold`: Azure DevOps pipeline variable.

Pipeline values must not be stored in appsettings files.

Required values:

- `BuildId` must be present.
- `MinThreshold` and `MaxThreshold` must be valid decimals.
- `MinThreshold` must be less than or equal to `MaxThreshold`.

Optional values:

- `TestTag` selects tagged scenarios when supplied.
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
QueueLimit
```

Radar limiting is mandatory and cannot be disabled. Checked-in configuration uses `RequestsPerSecond = 2` and `QueueLimit = 4`. Each configured logical endpoint receives one independent `SlidingWindowRateLimiter` with a one-second window divided into 10 segments, oldest-first queueing, and automatic replenishment. Every initial attempt and retry consumes a permit from that endpoint's limiter. There is no concurrent in-flight request limit.

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
RadarValue
  -> BaselineValue
  -> Difference calculation in NUnit Radar test
  -> ThresholdMatcher
  -> Boolean outcome
  -> RadarValidationReportRow
  -> ResultUpdater
  -> Radar log
```

The NUnit Radar test owns actual-value retrieval, baseline-value retrieval, difference calculation, `ThresholdMatcher` invocation, and report-row creation. `ThresholdMatcher` evaluates only the supplied `Difference`; it does not calculate values, create result objects, create reports, or perform assertions.

ICE comparison is performed directly inside the NUnit ICE test. ICE does not use pipeline thresholds, `ThresholdMatcher`, `MatchResult`, or another comparison service. The test performs an exact `IceValue == BaselineValue` comparison, asserts the result, and creates the ICE report row. ICE does not calculate or report `Difference` values.

Radar results pass when:

```text
Difference >= MinThreshold
AND
Difference <= MaxThreshold
```

For example, with `RadarValue = 1005`, `BaselineValue = 1000`, `MinThreshold = -10`, and `MaxThreshold = 10`, the difference is `5` and the result is `PASS`.

No percentage difference or zero-baseline special rule is used. A baseline value of zero is valid because the comparison is subtraction-based.

Retries are infrastructure concerns only. A retry must never recalculate business results or convert a threshold failure, premium mismatch, XSD failure, pipeline validation failure, configuration failure, or data validation failure into a pass. Once a scenario has a business failure, it remains failed.

### Financial data types

`RadarValue`, `IceValue`, `BaselineValue`, `Difference`, `MinThreshold`, and `MaxThreshold` must all use `decimal`.

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
    "Endpoints": {},
    "Routes": {},
    "ResponseXsdMappings": {}
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
  "RadarJsonSettings": {
    "ResponseContractFile": "TestAssets/Json/placeholder-response.json"
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2,
    "QueueLimit": 4
  }
}
```

Do not store API key values, certificate passwords, certificate content, pipeline values, or matching thresholds here.

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
    },
    "Routes": {
      "Route001": {
        "ProductCode": "HOME",
        "SchemeCode": "ABC",
        "EndpointName": "Endpoint1",
        "RouteKey": "home-abc"
      },
      "Route002": {
        "ProductCode": "HOME",
        "SchemeCode": "XYZ",
        "EndpointName": "Endpoint1",
        "RouteKey": "home-xyz"
      }
    },
    "ResponseXsdMappings": {
      "Route001": "Home_ABC.xsd",
      "Route002": "Home_XYZ.xsd"
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
  "RadarJsonSettings": {
    "ResponseContractFile": "TestAssets/Json/placeholder-response.json"
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2,
    "QueueLimit": 4
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
    },
    "Routes": {
      "Route001": {
        "ProductCode": "HOME",
        "SchemeCode": "ABC",
        "EndpointName": "Endpoint1",
        "RouteKey": "home-abc"
      }
    },
    "ResponseXsdMappings": {
      "Route001": "Home_ABC.xsd"
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
  "RadarJsonSettings": {
    "ResponseContractFile": "TestAssets/Json/placeholder-response.json"
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecond": 2,
    "QueueLimit": 4
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

Radar requests use `POST`, `Content-Type: application/xml`, and `Accept: application/xml`. The workload-level `RestClient` is configured once with automatic gzip, deflate, and brotli decompression. Radar retries HTTP 408, 429, 500, 502, 503, and 504 plus statusless transient network failures using `RetrySettings`. A valid `Retry-After` delta-seconds or HTTP-date is capped by `ApiRetryAfterMaxDelaySeconds`, then combined with local exponential backoff using the longer delay. When capped, the warning records the supplied value, configured maximum, and effective delay. Each attempt consumes a permit from its logical endpoint's limiter.

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
    -> TB_REQUEST scenarios
    -> Trim ProductCode and SchemeCode
    -> Match exactly one configured route by fields
    -> Resolve selected route EndpointName
    -> BaseUrl, ApiKeyValue
    -> RadarUrlBuilder
    -> RadarApiClient
    -> Radar XML response
    -> XsdFileResolver
    -> XsdValidator
    -> XmlValueExtractor
    -> Radar TotalAmount
    -> BaselineDataReader
    -> TB_RESPONSE baseline XML
    -> Baseline TotalAmount
    -> Difference = RadarValue - BaselineValue
    -> Threshold comparison
    -> UpdatePassResultAsync or UpdateFailResultAsync
    -> Radar CSV report
    -> Radar log
```

Adding a product and scheme endpoint combination requires only a new `RadarSettings.Endpoints` entry. No switch statements or product/scheme-specific branching are permitted.

## 10. ICE Execution Flow

```text
NUnit test
  -> BaselineDataReader
  -> TB_RESPONSE passing baseline scenarios
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
- Perform XSD validation.
- Call `ResultUpdater`.
- Update `TB_RESPONSE`.
- Store results in the database.

ICE reads `TB_RESPONSE` only to obtain the baseline XML.

## 11. Reporting and Logging

### Radar report

`Models/Reporting/RadarValidationReportRow` contains:

```text
BuildId
ScenarioId
QuoteRef
SchemeCode
ProductCode
Result
RadarValue
BaselineValue
Difference
FuzzyMatch
```

### ICE report

`Models/Reporting/IceValidationReportRow` contains only:

```text
BuildId
ScenarioId
QuoteRef
SchemeCode
ProductCode
IceValue
BaselineValue
Result
```

The `Result` field uses `ScenarioResult` and serializes to `PASS`, `FAIL`, or `ERROR` in CSV. `Difference`, `MinThreshold`, `MaxThreshold`, `FuzzyMatch`, request XML, response XML, and failure-reason fields are not part of the ICE report.

`CsvReportWriter` writes the ICE report to `Ice_<BuildId>.csv`.

ICE report field ownership:

- From `TB_RESPONSE`: `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`.
- From ICE: `IceValue`.
- From baseline XML: `BaselineValue`.
- Calculated in `IceValidationTests`: `Result`.

ICE validation is exact equality and does not calculate or report `Difference` values.

`FuzzyMatch` remains on the Radar report for the existing report contract. It records whether the Radar difference is within the configured inclusive threshold range; it does not imply XML-specific matching.

Radar report field ownership:

- From `TB_REQUEST` and resolved configuration: `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`.
- From Radar: `RadarValue`.
- From the baseline response: `BaselineValue`.
- Calculated in the NUnit Radar test: `Difference`, `Result`, `FuzzyMatch`.

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

### Radar XSD validation failure handling

When a Radar response fails XSD validation:

1. Mark the current scenario as `FAILED`.
2. Write the validation errors to the Radar log.
3. Create a Radar report entry.
4. Call `UpdateFailResultAsync`.
5. Do not extract XML values, calculate a difference, or run matching logic.
6. Continue processing the remaining scenarios.

A single scenario validation failure must not terminate the full execution.

## 12. XSD Storage

XSD files are stored under:

```text
PricingValidationFramework.Tests/
└── TestAssets/
    └── Xsd/
        ├── Home.xsd
        ├── Motor.xsd
        └── Travel.xsd
```

Future XSD-to-combination configuration is outside the endpoint model prepared here. `RadarEndpointSettings` remains limited to `BaseUrl` and `ApiKeyValue`.

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

`ResultUpdater` is included in this policy. Its updates must be scoped by scenario identity and build identity so a retried transient failure cannot create duplicate logical results.

Read operations may retry the same read operation because they do not mutate state. Update operations may retry only the same idempotent, scenario/build-scoped database command after a transient database failure. An update retry never reruns API calls, extraction, matching, or the scenario workflow.

### API retries

API retry behavior applies to `RadarApiClient` and `IceApiClient`.

Retry HTTP 408, 429, 500, 502, 503, and 504, plus transient DNS, network, and socket failures. Do not retry HTTP 400, 401, 403, or 404. For a valid `Retry-After` delta-seconds or HTTP-date, cap the server delay at `ApiRetryAfterMaxDelaySeconds`, then wait for the maximum of local exponential backoff and the capped value. Missing or invalid values use local backoff; invalid values are logged without including the header value.

Use `ApiRetryCount` and exponential backoff based on `ApiRetryDelaySeconds`, with cancellation support. Retry attempts are written to the Radar or ICE log that owns the operation. Radar retry warnings contain status or transient-network category, attempt, maximum attempts, and delay only.

Database reads and result updates use the database retry policy; API calls use the API retry policy. Permanent SQL errors are never retried.

## 15. Parallel Execution and External API Throttling

### Execution model

NUnit owns Radar scenario discovery and concurrency. Each selected scenario is one NUnit case marked `[Parallelizable(ParallelScope.Children)]`; the assembly-level worker limit is four. There is no internal worker pool, `Task.WhenAll` across scenarios, or custom scenario scheduler. `RadarScenarioProcessor` continues to process exactly one scenario. ICE remains separate and is not changed by Radar parallel execution.

### Radar throttling

One run-level `RadarRequestRateLimiter` registry is shared by every parallel Radar scenario and injected into `RadarApiClient`. It contains one .NET `SlidingWindowRateLimiter` per configured logical endpoint, keyed case-insensitively. Each has a one-second window divided into 10 segments, permits 2 starts per window, and queues up to 4 waiting attempts in oldest-first order. These budgets are independent: saturation on `PricingA` does not consume `PricingB` or `PricingC` capacity. Limiting is mandatory, cannot be disabled, and there is no separate in-flight request limit.

`RadarTestSetup` binds and validates the required settings, rejects case-insensitive duplicate endpoint keys, and verifies every route points to a configured endpoint before database discovery. It creates and disposes the registry once for the Radar run. `RadarApiClient` acquires the selected endpoint's permit immediately before each outbound attempt and creates an independent `RestRequest`. Each retry first waits for local backoff or valid `Retry-After` (whichever is longer), then acquires another permit from that same endpoint limiter. Queue rejection sends no HTTP request and becomes a technical ERROR. Exhausted HTTP 429 likewise remains a technical ERROR without PASS/FAIL persistence.

## 16. Cancellation Strategy

Azure DevOps cancellation is represented by a `CancellationToken` passed through the full execution path.

Cancellation applies to:

- `RequestDataReader`
- `BaselineDataReader`
- `ResultUpdater`
- `RadarApiClient`
- `IceApiClient`
- `PipelineInputValidator`
- `XsdValidator`
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

Reports must be deterministic and sorted by `ScenarioId` ascending before final CSV generation. Radar report rows are sorted by `ScenarioId` ascending before writing `Radar_<BuildId>.csv`. ICE report rows are sorted by `ScenarioId` ascending before writing `Ice_<BuildId>.csv`.

Parallel completion order does not affect report ordering. ICE reporting and logging remain separate and are not changed by this Radar implementation.

Database readers and `ResultUpdater` create and dispose an independent SQL connection per operation; no connection is shared across NUnit cases, and there is no transaction around an API call. `ResultUpdater` retains existing affected-row validation and PASS/FAIL update semantics. No scenario-specific state is stored in fixture-global or NLog global context.

NLog keeps separate ICE and Radar targets. Build identifiers may remain in `GlobalDiagnosticsContext`; scenario identifiers are structured event properties. Radar logging records exception type only and does not emit exception payloads, request/response XML, API keys, authorization headers, passwords, certificates, or connection strings. `IceTestSetup` does not call process-wide `LogManager.Shutdown` during fixture disposal, so it cannot terminate Radar logging in the same test process.

## 18. Updated Radar Execution Flow

```text
RequestDataReader
  -> Selected TB_REQUEST scenarios and optional test-tag filter
  -> Reject duplicate ScenarioIds
  -> NUnit TestCaseSource: one Radar test case per scenario
  -> Up to four NUnit child cases execute concurrently
  -> Each case loads its own baseline and processes one scenario
  -> RadarApiClient acquires shared limiter permit per HTTP attempt
  -> RadarScenarioProcessor: URL, optional response XSD by route ID, typed TotalAmount, comparison
  -> Persist existing PASS or FAIL result semantics
  -> Add exactly one terminal report row to concurrent collection
  -> One Radar CSV sorted by ScenarioId in OneTimeTearDown
```

Each case asserts its own PASS, FAIL, or ERROR outcome, so one failed scenario does not prevent unrelated cases from completing. Request XML is not XSD-validated. Response XSD validation runs only when `ResponseXsdMappings` contains a nonempty filename for the selected route identifier; otherwise it is skipped. The typed Radar response currently contains only the evidenced `TotalAmount`; additional response fields await confirmed schema and validation requirements. Future JSON contracts and payloads belong under `TestAssets/Json`, with the placeholder `RadarJsonResponse` populated only after the client contract is received. Cancellation propagates separately and does not add a normal comparison row. ICE remains a separate flow and is not changed by this Radar implementation.

## 19. Updated ICE Execution Flow

```text
IceTestSetup
  -> Load configuration and shared ICE dependencies
  -> BaselineDataReader
  -> TB_RESPONSE PASS scenarios
  -> Latest PASS per ProductCode + SchemeCode
  -> One explicit IceValidationTests workload
  -> QuoteRef
  -> IceUrlBuilder
  -> Final ICE URL
  -> IceApiClient with certificate and transient retry policy
  -> JsonValueExtractor
  -> ICE Premium
  -> XmlValueExtractor
  -> Baseline Premium
  -> Difference calculation
  -> Exact value comparison
  -> NUnit assertion
  -> IceValidationReportRow
  -> One Ice_<BuildId>.csv report per run
  -> ICE log
```

ICE remains independent from Radar. It does not resolve scheme configuration, use Radar authentication or throttling, perform XSD validation, call `ResultUpdater`, or update `TB_RESPONSE`.

ICE does not read `TB_REQUEST`. It uses `BaselineDataReader` as its only database reader and performs comparison/assertion logic directly in the NUnit test.

ICE remains its existing single workload test, iterating its selected baselines and producing one combined report. This Radar change does not alter ICE execution or business behavior.

## 20. Classes to Rename

| Existing class | Final class |
|---|---|
| `XmlApiClient` | `RadarApiClient` |
| `JsonApiClient` | `IceApiClient` |
| `XmlToleranceMatcher` | `ThresholdMatcher` |
| `ValueDifferenceMatcher` | `ThresholdMatcher` |

If the existing generic report or logger types cannot represent the separate flows clearly, replace them with the Radar and ICE-specific types defined above.

## 21. Classes to Remove

Remove:

- `ValidationOrchestrator`: NUnit tests orchestrate execution.
- `EndpointResolver`: direct configuration lookup is sufficient.
- `SchemeConfig`: replaced by direct `RadarSettings` product/scheme lookup.
- `EndpointSettings`: replaced by `RadarSettings`, `RadarEndpointSettings`, and `IceSettings`.
- `XmlToleranceMatcher`: replaced by `ThresholdMatcher`.
- `ValueDifferenceMatcher`: replaced by `ThresholdMatcher`.
- `IIceApiClient`: removed because ICE has one concrete API client.
- `ICsvReportWriter`: removed because one concrete CSV writer serves both report models.
- `MatchResult`: removed because it has no genuine consumer; ICE comparison is inline in `IceValidationTests` and Radar does not require the model.
- Dapper with `Microsoft.Data.SqlClient` is the database standard; SQL remains explicit, parameterized, strongly typed, and cancellation-aware.
- The focused `IRadarRequestRateLimiter` is the Radar API-boundary contract; no generic concurrency, retry, workflow, or reporting frameworks are introduced.

`RadarUrlBuilder` is a focused URL-building service, not a generic endpoint resolver.

## 22. Final Design Decisions

- `BuildId` is the required ICE report identifier.
- `TestTag` and `RequestTime` are optional pipeline inputs for Radar only.
- `RequestTime` uses the literal-`Z` format `yyyy-MM-ddZHH:mm:ss`; missing values use current UTC time.
- ICE validation is exact equality: `IceValue == BaselineValue`.
- ICE does not use thresholds, `MinThreshold`, `MaxThreshold`, or `ThresholdMatcher`.
- ICE does not calculate or report `Difference` values.
- `IceValue` and `BaselineValue` are `decimal`.
- ICE reads `BUILD_BUILDID` at execution time for `BuildId` and report naming.
- ICE passes only when `IceValue` exactly equals `BaselineValue`.
- Equality comparison, assertion, and report-row construction remain owned by the NUnit ICE test.
- Radar is driven by `TB_REQUEST`; ICE is driven by passing `TB_RESPONSE` baseline records selected by `BaselineDataReader`.
- ICE uses `IceBaselineScenario` records and does not read `TB_REQUEST`.
- `IceUrlBuilder` owns ICE URL composition; `IceApiClient` receives a fully built URL and never appends `QuoteRef`.
- `RequestDataReader` reads only `TB_REQUEST`; `BaselineDataReader` reads baseline XML from `TB_RESPONSE`; `ResultUpdater` updates `TB_RESPONSE`.
- ICE does not call `ResultUpdater` or update `TB_RESPONSE`.
- `IceValidationTests` is the ICE pipeline orchestrator; no separate ICE application orchestrator is required.
- ICE retains its existing single workload test over selected baseline scenarios and one combined report per run.
- Database and API retries are transient-only, configurable, exponentially backed off, jittered, cancellation-aware, and logged by flow.
- `ResultUpdater` is retry-safe through scenario/build-scoped updates.
- All DTOs, contracts, result models, report row models, and future enums belong under `Models`; service folders contain services only.
- Business failures, validation failures, mismatches, and configuration failures are never retried.
- NUnit owns Radar concurrency with four workers; each logical endpoint has an independent 2-per-second sliding-window budget and a queue of four waiting attempts. Every retry consumes another permit. There is no in-flight request limit, and ICE behavior is unchanged.
- Radar cases collect one terminal row each in a thread-safe keyed collection; fixture teardown writes one deterministic CSV. NLog remains flow-separated and scenario values use structured properties, not global context.
- Cancellation stops active work and limiter waits without turning cancellation into FAIL or ERROR.
- Radar uses one shared header name and endpoint-group-specific API key values.
- ICE always requires a client certificate; there is no optional certificate flag.
- UAT Radar keys, ICE API key values, certificate passwords, and PFX content come from Azure Key Vault.
- UAT certificate material is loaded in memory and attached to an ICE-specific `HttpClientHandler`; agent certificate-store installation is not required.
- Radar and ICE produce separate reports and log files.
- Report and log names are fixed as `Radar_<BuildId>.csv`, `Ice_<BuildId>.csv`, `Radar_<BuildId>.log`, and `Ice_<BuildId>.log`.
- XSD files are resolved from `PricingValidationFramework.Tests/TestAssets/Xsd`.
- Radar XSD failure is scenario-level: log, report, call `UpdateFailResultAsync`, skip extraction and matching, then continue with the next scenario.

NUnit owns scenario execution, schemes select Radar endpoint groups, endpoint groups own Radar credentials, and ICE remains an independent validation path.

## 23. Current Implementation Confirmation

The architecture reflects the current implementation:

- Folder structure and ownership boundaries are defined.
- Business naming is finalized for Radar, ICE, baseline data, validation, matching, reporting, and logging.
- Radar and ICE execution are data-driven with no hardcoded test cases or static scenario definitions; Radar uses `TB_REQUEST`, while ICE uses passing `TB_RESPONSE` baselines.
- `IceUrlBuilder` centralizes ICE URL construction and `IceApiClient` owns only HTTP execution.
- `ExternalAPIAccess/ApiClients/` contains `RadarApiClient` and `IceApiClient`.
- `ExternalAPIAccess/UrlBuilders/` contains `RadarUrlBuilder`, `IceUrlBuilder`, and `RequestTimeFormatter`; API clients never build URLs.
- `ExternalAPIAccess/Throttling/` contains `IRadarRequestRateLimiter` and `RadarRequestRateLimiter`; Radar limiting is shared across parallel cases and ICE does not use it.
- Configuration separates scheme metadata, Radar endpoint groups, shared Radar authentication, ICE authentication, and certificate material.
- Azure Key Vault supplies UAT Radar keys, ICE secrets, and certificate material.
- ICE certificates are mandatory and loaded in memory without agent certificate-store installation.
- Radar endpoint grouping allows multiple schemes to share one endpoint group and credential.
- New schemes require only configuration, an XSD file, and test data.
- NUnit tests orchestrate execution; no application-level orchestrator is required.
- Pipeline input validation occurs before database access, API calls, or output generation.
- Database responsibilities are separated between request reads, baseline reads, and result updates.
- ICE uses `BaselineDataReader.GetPassingBaselineScenariosAsync()` and `IceBaselineScenario`; it does not read `TB_REQUEST`.
- Radar XSD failures are isolated to the current scenario and do not stop the run.
- Radar and ICE have separate reports, logs, output names, and flow-specific responsibilities.
- XSD files have a fixed location and are resolved from scheme configuration.
- NUnit owns Radar concurrency with four workers; there is no internal scenario worker pool.
- Radar rate limiting is mandatory with the checked-in 2 requests-per-second and 4 queued attempts per endpoint.
- Every Radar HTTP retry acquires the same endpoint's permit after the longer of local backoff and valid `Retry-After`; one sorted report is written after scenario cases complete.
- Result update retry safety is defined through idempotent scenario/build-scoped updates.
- Radar and ICE report ordering is deterministic and independent of parallel scenario completion order.
- Radar reports include `SchemeCode` for endpoint-routing diagnostics.
- `MatchResult` remains removed, and `ThresholdMatcher` remains Radar-only.
- Dapper database standards, explicit SQL ownership, and SQL-side filtering/ranking are documented.
- NUnit tests remain the orchestration and comparison layer; no additional abstractions were introduced.

The architecture is aligned with the current solution structure and implementation.
