# Pricing Validation Framework Architecture

## Status

This document freezes the architecture, naming, configuration structure, and responsibilities before implementation begins.

## 1. Folder Structure

```text
PricingValidationFramework.Core/
├── Configuration/
│   ├── DatabaseSettings.cs
│   ├── IceSettings.cs
│   ├── ParallelExecutionSettings.cs
│   ├── RadarRateLimitSettings.cs
│   ├── RadarAuthenticationSettings.cs
│   ├── RadarEndpointSettings.cs
│   ├── RetrySettings.cs
│   └── SchemeSettings.cs
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
│       └── RadarRequestThrottler.cs
├── Extraction/
│   ├── JsonValueExtractor.cs
│   └── XmlValueExtractor.cs
├── Logging/
│   ├── IceTestRunLogger.cs
│   └── RadarTestRunLogger.cs
├── Matching/
│   └── ThresholdMatcher.cs
├── Models/
│   ├── Common/
│   │   └── PipelineSettings.cs
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
│   │   └── IceValidationTests.cs
│   └── Radar/
├── TestAssets/
│   ├── Ice/
│   ├── Radar/
│   └── Xsd/
├── TestResults/
│   ├── Reports/
│   └── Logs/
└── appsettings*.json
```

NUnit tests are the execution orchestrators. There is no application-level `ValidationOrchestrator`.

`TestResults/` is a runtime-generated output location, not a source-code folder. It must be excluded from source control. `Reports/` and `Logs/` must not exist as separate top-level folders outside `TestResults/`.

## 2. Class Responsibilities

### Configuration

- `DatabaseSettings`: owns `ConnectionString`.
- `SchemeSettings`: maps a scheme code to `RadarEndpointName`, `UrlKeyName`, and `XsdFile`.
- `RadarEndpointSettings`: maps an endpoint group to `BaseUrl` and `ApiKeyValue`.
- `RadarAuthenticationSettings`: owns the shared Radar `ApiKeyHeaderName`.
- `IceSettings`: owns the ICE endpoint, API authentication, and mandatory client-certificate configuration, including local PFX or Key Vault PFX material.
- `RetrySettings`: owns configurable database and API retry counts and delay settings.
- `ParallelExecutionSettings`: owns Radar and ICE maximum degrees of parallelism.
- `RadarRateLimitSettings`: owns the per-endpoint-group Radar request rate limit.
- `PipelineInputValidator`: validates all pipeline inputs before database access, API calls, or report generation.

### Models

- `Models/Common/PipelineSettings`: owns runtime values `BuildId`, `TestTag`, `RequestTime`, `MinThreshold`, and `MaxThreshold`.
- `Models/Database/ScenarioRequest`: represents a scenario read from `TB_REQUEST`.
- `Models/Database/IceBaselineScenario`: represents one passing `TB_RESPONSE` baseline selected for ICE processing. It contains `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`, `XmlResponse`, `Status`, and `LastUpdated`.
- `Models/Database/ScenarioResponse`: represents baseline/result data associated with `TB_RESPONSE`.
- `Models/Reporting/RadarValidationReportRow`: represents one Radar report row.
- `Models/Reporting/IceValidationReportRow`: represents one ICE report row.
- `Models/Enums`: standard location for future enums; no enum implementation is currently required.

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

- `ExternalAPIAccess/ApiClients/RadarApiClient`: sends XML requests to Radar using a resolved URL and endpoint-group API key.
- `ExternalAPIAccess/ApiClients/IceApiClient`: sends requests to ICE using a fully built URL, ICE authentication, and a client certificate. It does not construct URLs, append `QuoteRef`, or perform URL composition.
- `ExternalAPIAccess/UrlBuilders/RadarUrlBuilder`: owns Radar URL composition.
- `ExternalAPIAccess/UrlBuilders/IceUrlBuilder`: owns ICE URL composition.
- `ExternalAPIAccess/UrlBuilders/RequestTimeFormatter`: owns Radar request-time validation and formatting.
- `ExternalAPIAccess/Throttling/RadarRequestThrottler`: owns Radar request throttling per endpoint group.

`IceApiClient` is the concrete ICE HTTP boundary. `CsvReportWriter` is the concrete CSV output service shared by Radar and ICE. No additional interface is required for either single implementation.

API clients own HTTP communication only. URL builders own URL composition, and the Radar throttler owns Radar request throttling. These components are grouped under `ExternalAPIAccess` because they all support external service communication.

### Radar support

- `XsdFileResolver`: resolves the XSD file named by scheme configuration.
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

Each selected database record represents one scenario execution unit. `RequestDataReader` is the Radar scenario source, and `BaselineDataReader` is the ICE scenario source.

### Radar flow

```text
TB_REQUEST
  -> Scenario
  -> Radar processing
```

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

### SchemeSettings

One entry per scheme:

```text
RadarEndpointName
UrlKeyName
XsdFile
```

Example:

```text
Home -> Endpoint1, Home, Home.xsd
Motor -> Endpoint1, Motor, Motor.xsd
Travel -> Endpoint2, Travel, Travel.xsd
```

A scheme does not own credentials. Multiple schemes may reference the same Radar endpoint group.

### RadarEndpointSettings

One entry per Radar endpoint group:

```text
BaseUrl
ApiKeyValue
```

There are currently three groups: `Endpoint1`, `Endpoint2`, and `Endpoint3`.

`ApiKeyValue` is a secret in UAT and must come from Azure Key Vault.

### RadarAuthenticationSettings

```text
ApiKeyHeaderName
```

All Radar endpoint groups use the same header name. Only `ApiKeyValue` changes by endpoint group.

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
```

Retry counts mean the number of retries after the initial attempt. Delay values are base delays for exponential backoff. Retry settings are configurable and are not hard-coded in clients or database classes.

Recommended starting values are three retries and a two-second base delay for both database and API operations. Backoff should be capped at 30 seconds with bounded jitter. These are operational defaults and remain configurable through the environment settings.

### ParallelExecutionSettings

```text
RadarMaxDegreeOfParallelism
IceMaxDegreeOfParallelism
```

These values control concurrent scenario workers. They are upper bounds, not guarantees that all workers will be active simultaneously.

### RadarRateLimitSettings

```text
RequestsPerSecondPerEndpoint
```

The value applies independently to each Radar endpoint group and is configurable. It must not be hard-coded.

## 5. Validation and Comparison Rules

### Absolute difference

The framework uses absolute-value difference matching:

```text
Difference = ActualValue - BaselineValue
```

For Radar:

```text
Difference = RadarValue - BaselineValue
```

For ICE:

```text
Difference = IceValue - BaselineValue
```

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

ICE comparison is performed directly inside the NUnit ICE test. No matcher, comparison service, or validation engine participates in the ICE path. The test calculates the difference, applies the configured tolerance, asserts the result, and creates the report row.

A result passes when:

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
  "SchemeSettings": {},
  "RadarEndpointSettings": {},
  "RadarAuthenticationSettings": {
    "ApiKeyHeaderName": "X-API-KEY"
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
    "ApiRetryDelaySeconds": 2
  },
  "ParallelExecutionSettings": {
    "RadarMaxDegreeOfParallelism": 20,
    "IceMaxDegreeOfParallelism": 4
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecondPerEndpoint": 2
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
  "SchemeSettings": {
    "Home": {
      "RadarEndpointName": "Endpoint1",
      "UrlKeyName": "Home",
      "XsdFile": "Home.xsd"
    },
    "Motor": {
      "RadarEndpointName": "Endpoint1",
      "UrlKeyName": "Motor",
      "XsdFile": "Motor.xsd"
    },
    "Travel": {
      "RadarEndpointName": "Endpoint2",
      "UrlKeyName": "Travel",
      "XsdFile": "Travel.xsd"
    }
  },
  "RadarEndpointSettings": {
    "Endpoint1": {
      "BaseUrl": "https://localhost/radar/endpoint1/quote",
      "ApiKeyValue": "development-radar-key-a"
    },
    "Endpoint2": {
      "BaseUrl": "https://localhost/radar/endpoint2/quote",
      "ApiKeyValue": "development-radar-key-b"
    },
    "Endpoint3": {
      "BaseUrl": "https://localhost/radar/endpoint3/quote",
      "ApiKeyValue": "development-radar-key-c"
    }
  },
  "RadarAuthenticationSettings": {
    "ApiKeyHeaderName": "X-API-KEY"
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
    "ApiRetryDelaySeconds": 2
  },
  "ParallelExecutionSettings": {
    "RadarMaxDegreeOfParallelism": 20,
    "IceMaxDegreeOfParallelism": 4
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecondPerEndpoint": 2
  }
}
```

### appsettings.Uat.json

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Server=uat-db;Database=PricingValidation;Trusted_Connection=True;"
  },
  "SchemeSettings": {
    "Home": {
      "RadarEndpointName": "Endpoint1",
      "UrlKeyName": "Home",
      "XsdFile": "Home.xsd"
    },
    "Motor": {
      "RadarEndpointName": "Endpoint1",
      "UrlKeyName": "Motor",
      "XsdFile": "Motor.xsd"
    },
    "Travel": {
      "RadarEndpointName": "Endpoint2",
      "UrlKeyName": "Travel",
      "XsdFile": "Travel.xsd"
    }
  },
  "RadarEndpointSettings": {
    "Endpoint1": {
      "BaseUrl": "https://uat-radar-1.example.com/quote"
    },
    "Endpoint2": {
      "BaseUrl": "https://uat-radar-2.example.com/quote"
    },
    "Endpoint3": {
      "BaseUrl": "https://uat-radar-3.example.com/quote"
    }
  },
  "RadarAuthenticationSettings": {
    "ApiKeyHeaderName": "X-API-KEY"
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
    "ApiRetryDelaySeconds": 2
  },
  "ParallelExecutionSettings": {
    "RadarMaxDegreeOfParallelism": 20,
    "IceMaxDegreeOfParallelism": 4
  },
  "RadarRateLimitSettings": {
    "RequestsPerSecondPerEndpoint": 2
  }
}
```

UAT must not contain Radar API key values, the ICE API key value, certificate passwords, or certificate material.

## 7. Azure Key Vault Structure

Recommended configuration keys:

```text
RadarEndpointSettings--Endpoint1--ApiKeyValue
RadarEndpointSettings--Endpoint2--ApiKeyValue
RadarEndpointSettings--Endpoint3--ApiKeyValue
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
UrlKeyName
RequestTime
```

Rules:

- Use the supplied `RequestTime` when present.
- Otherwise use the current time from an injected clock.
- Use UTC unless Radar requires another timezone.
- Format the timestamp in `RequestTimeFormatter`.
- URL-encode parameter values.
- Keep timestamp formatting out of `RadarApiClient`.

`RadarApiClient` only sends the XML request and applies:

```text
RadarAuthenticationSettings.ApiKeyHeaderName
RadarEndpointSettings[EndpointName].ApiKeyValue
```

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
    -> SchemeCode
    -> SchemeSettings[SchemeCode]
    -> RadarEndpointName, UrlKeyName, XsdFile
    -> RadarEndpointSettings[RadarEndpointName]
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

Adding a scheme requires only a new `SchemeSettings` entry, XSD file, and test data. No switch statements or scheme-specific branching are permitted.

## 10. ICE Execution Flow

```text
NUnit test
  -> PipelineInputValidator
  -> PipelineSettings
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
    -> Difference = IceValue - BaselineValue
    -> NUnit assertion using configured thresholds
    -> ICE CSV report
    -> ICE log
```

ICE must not:

- Resolve `SchemeCode`.
- Read `SchemeSettings`.
- Read `UrlKeyName`.
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

`Models/Reporting/IceValidationReportRow` contains:

```text
BuildId
ScenarioId
QuoteRef
SchemeCode
ProductCode
IceValue
BaselineValue
Difference
Result
```

`CsvReportWriter` writes the ICE report to `Ice_<BuildId>.csv`.

ICE report field ownership:

- From `TB_RESPONSE`: `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`.
- From ICE: `IceValue`.
- Calculated in `IceValidationTests`: `Difference`, `Result`.

These fields remain correct for the final subtraction-based comparison model. `Difference` is the decimal result of `ActualValue - BaselineValue`.

`FuzzyMatch` remains on the Radar report for the existing report contract. It records whether the Radar difference is within the configured inclusive threshold range; it does not imply XML-specific matching.

Radar report field ownership:

- From `TB_REQUEST` and resolved configuration: `ScenarioId`, `QuoteRef`, `SchemeCode`, `ProductCode`.
- From Radar: `RadarValue`.
- From the baseline response: `BaselineValue`.
- Calculated in the NUnit Radar test: `Difference`, `Result`, `FuzzyMatch`.

Radar and ICE produce separate CSV reports and separate log files.

Radar logs contain:

- Request XML
- `ApiResponsePayload` containing response XML
- Database and API retry attempts
- XSD validation results
- Matching results
- Cancellation events
- Errors

ICE logs contain:

- Request URL
- `ApiResponsePayload` containing JSON
- Database and API retry attempts
- Comparison results
- Certificate errors
- Cancellation events
- Errors

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

`XsdFileResolver` resolves the configured `SchemeSettings.XsdFile` value relative to `TestAssets/Xsd`. Scheme onboarding requires a new configuration entry, XSD file, and test data only.

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

Retry HTTP 408, 429, 500, 502, 503, and 504, plus transient DNS, network, and socket failures. Do not retry HTTP 400, 401, 403, or 404. Respect `Retry-After` for HTTP 429 when present, subject to a configured maximum delay.

Use `ApiRetryCount` and exponential backoff based on `ApiRetryDelaySeconds`, with bounded jitter and cancellation support. Retry attempts are written to the Radar or ICE log that owns the operation.

Database reads and result updates use the database retry policy; API calls use the API retry policy. Permanent SQL errors are never retried.

## 15. Parallel Execution and External API Throttling

### Execution model

NUnit exposes one Radar test for the Radar workload and one ICE test for the ICE workload. The tests load scenarios once, create bounded asynchronous workers, and aggregate results. They do not create one NUnit test per scenario.

Radar supports 2000+ scenarios through bounded concurrency controlled by `RadarMaxDegreeOfParallelism`. ICE uses `IceMaxDegreeOfParallelism` for its smaller workload and does not require endpoint throttling.

### Radar throttling

`ExternalAPIAccess/Throttling/RadarRequestThrottler` determines the endpoint group from the resolved `SchemeSettings.RadarEndpointName` and applies the configured `RequestsPerSecondPerEndpoint` limit independently for each group.

Parallelism and throttling are separate controls:

```text
RadarMaxDegreeOfParallelism = 20
RequestsPerSecondPerEndpoint = 2
```

Up to 20 workers may exist, but each endpoint group may send no more than its configured rate. Throttling always gates the request before `RadarApiClient` sends it. Waiting workers observe cancellation and do not start a request after cancellation.

The throttler must use monotonic timing, avoid a single global lock across endpoint groups, and maintain bounded in-memory state. It must not accumulate an unbounded queue of scenarios.

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
- `RadarRequestThrottler`

When cancellation is requested:

1. Stop scheduling new scenarios.
2. Stop workers waiting for Radar throttling.
3. Do not start additional API requests.
4. Allow safe in-flight operations to observe cancellation and finish or abort according to their API contract.
5. Flush accepted report records and log entries.
6. Dispose HTTP, certificate, database, and writer resources.
7. Record a cancellation event in the relevant flow-specific log.
8. Exit without treating cancellation as a normal scenario failure.

Cancellation must not be swallowed by retry policies or converted into a retryable transient failure.

## 17. Thread-Safe Reporting and Logging

Parallel workers must never write directly to shared CSV or log files. Each flow uses a bounded producer/consumer channel:

```text
Scenario workers
  -> bounded report/log channel
  -> single flow-specific writer
  -> CSV or log file
```

The preferred strategy is one single writer per output file, with records enqueued by workers. This provides atomic record ordering, prevents interleaved log entries, avoids corrupted CSV rows, and gives cancellation a clear flush point. The bounded channel applies backpressure so output cannot grow without limit.

### Report ordering

Reports must be deterministic and sorted by `ScenarioId` ascending before final CSV generation. Radar report rows are sorted by `ScenarioId` ascending before writing `Radar_<BuildId>.csv`. ICE report rows are sorted by `ScenarioId` ascending before writing `Ice_<BuildId>.csv`.

Parallel execution order and scenario completion order are irrelevant to final report ordering. The report writer collects accepted terminal report rows, sorts them by `ScenarioId` ascending, and then performs final CSV generation. The same scenario set therefore produces consistent report ordering across executions. Log entries use timestamped, atomic entries in completion order because logs describe execution events rather than a business result sequence.

Radar and ICE each have independent report and log channels. There is no shared execution log and no shared file lock between flows. A writer owns file creation, header emission, record serialization, flush, and final disposal.

Duplicate records are prevented by assigning each scenario one terminal result and enqueueing that result exactly once. The writer does not deduplicate silently; duplicate terminal results are treated as an internal error and logged.

## 18. Updated Radar Execution Flow

```text
NUnit Radar test
  -> PipelineInputValidator
  -> Load PipelineSettings
  -> RequestDataReader
  -> TB_REQUEST scenarios
  -> Bounded Radar workers
  -> SchemeSettings[SchemeCode]
  -> RadarEndpointSettings[RadarEndpointName]
  -> RadarRequestThrottler
  -> RadarUrlBuilder
  -> RadarApiClient with transient retry policy
  -> XsdFileResolver
  -> XsdValidator
  -> XmlValueExtractor
  -> BaselineDataReader
  -> ThresholdMatcher
  -> ResultUpdater
  -> Radar report channel
  -> Radar log channel
  -> Single Radar report/log writers
```

If XSD validation fails, mark the scenario failed, log the validation errors, enqueue one Radar report entry, call `UpdateFailResultAsync`, skip extraction/difference/matching, and continue processing remaining scenarios. A single scenario failure does not terminate the run.

## 19. Updated ICE Execution Flow

```text
NUnit ICE test
  -> PipelineInputValidator
  -> Load PipelineSettings
  -> BaselineDataReader
  -> TB_RESPONSE PASS scenarios
  -> Latest PASS per ProductCode + SchemeCode
  -> IceBaselineScenario
  -> Bounded ICE workers
  -> QuoteRef
  -> IceUrlBuilder
  -> Final ICE URL
  -> IceApiClient with certificate and transient retry policy
  -> JsonValueExtractor
  -> ICE Premium
  -> XmlValueExtractor
  -> Baseline Premium
  -> Difference calculation
  -> Threshold evaluation
  -> NUnit assertion
  -> IceValidationReportRow
  -> CsvReportWriter
  -> ICE log
```

ICE remains independent from Radar. It does not resolve scheme configuration, use Radar authentication or throttling, perform XSD validation, call `ResultUpdater`, or update `TB_RESPONSE`.

ICE does not read `TB_REQUEST`. It uses `BaselineDataReader` as its only database reader and performs comparison/assertion logic directly in the NUnit test.

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
- `SchemeConfig`: replaced by `SchemeSettings`.
- `EndpointSettings`: replaced by `RadarEndpointSettings` and `IceSettings`.
- `XmlToleranceMatcher`: replaced by `ThresholdMatcher`.
- `ValueDifferenceMatcher`: replaced by `ThresholdMatcher`.
- `IIceApiClient`: removed because ICE has one concrete API client.
- `ICsvReportWriter`: removed because one concrete CSV writer serves both report models.
- `MatchResult`: removed because it has no genuine consumer; ICE comparison is inline in `IceValidationTests` and Radar does not require the model.
- Dapper with `Microsoft.Data.SqlClient` is the database standard; SQL remains explicit, parameterized, strongly typed, and cancellation-aware.
- No additional interfaces, abstractions, service layers, comparison layers, validation engines, result hierarchies, or generic managers are introduced.

`RadarUrlBuilder` is a focused URL-building service, not a generic endpoint resolver.

## 22. Final Design Decisions

- `BuildId`, `MinThreshold`, and `MaxThreshold` are required pipeline inputs.
- `TestTag` and `RequestTime` are optional pipeline inputs.
- `RequestTime` uses the literal-`Z` format `yyyy-MM-ddZHH:mm:ss`; missing values use current UTC time.
- `MinThreshold` and `MaxThreshold` are inclusive absolute-difference bounds and must satisfy `MinThreshold <= MaxThreshold`.
- `Difference` in both report models means `ActualValue - BaselineValue`.
- Threshold comparison is inclusive: `Difference >= MinThreshold && Difference <= MaxThreshold`.
- `RadarValue`, `IceValue`, `BaselineValue`, `Difference`, `MinThreshold`, and `MaxThreshold` are all `decimal`.
- `ThresholdMatcher` is used by Radar where required; ICE comparison remains inside the NUnit ICE test.
- `ThresholdMatcher` evaluates only a supplied Radar `Difference` against the configured bounds.
- Difference calculation, threshold evaluation, assertions, and report-row construction are owned by the NUnit tests.
- Radar is driven by `TB_REQUEST`; ICE is driven by passing `TB_RESPONSE` baseline records selected by `BaselineDataReader`.
- ICE uses `IceBaselineScenario` records and does not read `TB_REQUEST`.
- `IceUrlBuilder` owns ICE URL composition; `IceApiClient` receives a fully built URL and never appends `QuoteRef`.
- `RequestDataReader` reads only `TB_REQUEST`; `BaselineDataReader` reads baseline XML from `TB_RESPONSE`; `ResultUpdater` updates `TB_RESPONSE`.
- ICE does not call `ResultUpdater` or update `TB_RESPONSE`.
- `IceValidationTests` is the ICE pipeline orchestrator; no separate ICE application orchestrator is required.
- Database and API retries are transient-only, configurable, exponentially backed off, jittered, cancellation-aware, and logged by flow.
- `ResultUpdater` is retry-safe through scenario/build-scoped updates.
- All DTOs, contracts, result models, report row models, and future enums belong under `Models`; service folders contain services only.
- Business failures, validation failures, mismatches, and configuration failures are never retried.
- Radar and ICE use bounded parallel workers; Radar additionally applies independent per-endpoint rate limiting.
- Reports and logs use bounded producer/consumer channels with one writer per output file.
- Reports are sorted by `ScenarioId` ascending before final CSV generation; logs preserve atomic event entries in completion order.
- Cancellation stops scheduling, throttling waits, and new requests, then flushes output and disposes resources.
- Radar uses one shared header name and endpoint-group-specific API key values.
- ICE always requires a client certificate; there is no optional certificate flag.
- UAT Radar keys, ICE API key values, certificate passwords, and PFX content come from Azure Key Vault.
- UAT certificate material is loaded in memory and attached to an ICE-specific `HttpClientHandler`; agent certificate-store installation is not required.
- Radar and ICE produce separate reports and log files.
- Report and log names are fixed as `Radar_<BuildId>.csv`, `Ice_<BuildId>.csv`, `Radar_<BuildId>.log`, and `Ice_<BuildId>.log`.
- XSD files are resolved from `PricingValidationFramework.Tests/TestAssets/Xsd`.
- Radar XSD failure is scenario-level: log, report, call `UpdateFailResultAsync`, skip extraction and matching, then continue with the next scenario.

The architecture is frozen: NUnit tests orchestrate execution, schemes select Radar endpoint groups, endpoint groups own Radar credentials, and ICE is an independent reporting-only path.

## 23. Implementation Readiness Confirmation

The architecture is ready for implementation:

- Folder structure and ownership boundaries are defined.
- Business naming is finalized for Radar, ICE, baseline data, validation, matching, reporting, and logging.
- Radar and ICE execution are data-driven with no hardcoded test cases or static scenario definitions; Radar uses `TB_REQUEST`, while ICE uses passing `TB_RESPONSE` baselines.
- `IceUrlBuilder` centralizes ICE URL construction and `IceApiClient` owns only HTTP execution.
- `ExternalAPIAccess/ApiClients/` contains `RadarApiClient` and `IceApiClient`.
- `ExternalAPIAccess/UrlBuilders/` contains `RadarUrlBuilder`, `IceUrlBuilder`, and `RequestTimeFormatter`; API clients never build URLs.
- `ExternalAPIAccess/Throttling/` contains `RadarRequestThrottler`; throttling is Radar-specific and ICE does not require endpoint throttling.
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
- Retry, concurrency, throttling, cancellation, and output-writer behavior are defined.
- Result update retry safety is defined through idempotent scenario/build-scoped updates.
- Radar and ICE report ordering is deterministic and independent of parallel scenario completion order.
- Radar reports include `SchemeCode` for endpoint-routing diagnostics.
- `MatchResult` remains removed, and `ThresholdMatcher` remains Radar-only.
- Dapper database standards, explicit SQL ownership, and SQL-side filtering/ranking are documented.
- NUnit tests remain the orchestration and comparison layer; no additional abstractions were introduced.

The architecture is fully aligned with the physical solution structure and ready for implementation.
