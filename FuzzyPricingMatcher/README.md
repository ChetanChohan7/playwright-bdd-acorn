# FuzzyPricingMatcher

## Purpose

FuzzyPricingMatcher is a .NET 10 NUnit solution with a Baseline Loader and Comparison Test Runner. It uses one authoritative CSV, an existing fixed SQL schema, configured XML endpoints, and raw XML evidence.

## Current status

The local Unit suite passes. All committed routes and endpoints are disabled. The CSV is currently a headings-only safe template. Live SQL/API integration and Azure DevOps pipeline execution have not been run.

## Baseline Loader

The loader reads `TestAsset/baseline-scenarios.csv`, compares each scenario with stored baseline rows, calls the API only for new or changed XML, validates responses, and updates the existing schema. Obsolete deletion is allowed only after a clean data-bearing run.

## Comparison Test Runner

The comparison runner discovers selected database scenarios, sends raw request XML, validates the current and stored responses, reads comparison amounts, applies inclusive thresholds, and persists Pass/Fail before the NUnit assertion.

## Project structure

Implementation is grouped under `Api/`, `Comparison/`, `Configuration/`, `Data/`, `Evidence/`, `Loader/`, `Processing/`, `Routing/`, `Validation/`, and `Resilience/`. Unit tests are under `Tests/Unit`; guarded entry points are under `Tests/Integration/Loader` and `Tests/Integration/Comparison`.

## Documentation

- [Architecture](docs/architecture.md)
- [Execution path reference](docs/execution-path-reference.md)
- [Sequence diagrams](docs/sequence-diagrams.md)
- [Configuration guide](docs/configuration-guide.md)
- [Developer guide](docs/developer-guide.md)
- [Operations runbook](docs/operations-runbook.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Beginner user guide](docs/user-guide.md)
- [Client testing team guide](docs/client-testing-team-guide.md)
- [Code walkthrough for new starters](docs/code-walkthrough-for-new-starters.md)
- [Glossary](docs/glossary.md)
- [Assumptions and decisions](docs/assumptions-and-decisions.md)
- [Naming and style guide](docs/naming-and-style-guide.md)
- [Original solution review](docs/solution-review.md) (historical findings)
- [Post-remediation review](docs/post-remediation-review.md)
- [Final local verification](docs/final-verification.md) (release-gate status)
- Response schema and authoritative CSV guidance are included below.

## Quick local verification

```text
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --filter "TestCategory=Unit"
dotnet test --configuration Release --filter "TestCategory=Unit" --collect:"XPlat Code Coverage"
```

Unit tests use fakes and do not require SQL Server or live APIs. Real loader and comparison categories require approved local infrastructure and private `appsettings.Local.json` settings.

## Integration categories

Run `LoadBaseline` or `Compare` only with approved infrastructure and `FUZZY_RUN_INTEGRATION=true`. These categories are guarded and are not part of the normal Unit command.

## Configuration

Base settings are in `src/FuzzyPricingMatcher.Tests/appsettings.json`. Keep private overrides in the ignored `appsettings.Local.json` or environment variables. The authoritative CSV remains `TestAsset/baseline-scenarios.csv`.

## Safety controls

Do not enable placeholder routes or endpoints. Do not commit credentials, live connection strings, DDL, or raw XML logs. API and SQL retries remain separate, and every physical API attempt uses the shared limiter.

## Current limitations

Real SchemeCode mappings, endpoint contracts, credentials, response XSDs, amount paths, SQL permissions, and support contacts still require human approval. Coverage is collected but no percentage threshold is enforced.

## API foundation

`XmlApiClient` sends the exact raw XML string supplied by the caller and never serializes an `XDocument`. `RestClientFactory` caches one disposable `RestClient` per endpoint; each physical attempt creates a new `RestRequest`.

The process-wide `ApiRateLimiter` reserves one asynchronous permit for every physical attempt. At the default rate of two starts per second, reserved starts are spaced by approximately 500 milliseconds.

The retry pipeline retries transport failures, safe timeouts, and HTTP 408, 429, 502, 503, and 504 responses. It does not retry authentication, client, configuration, XML, validation, deserialization, amount, threshold, or cancellation failures. `Retry-After` is honored for 429 responses.

POST retries require confirmation from the API owner that repeating the operation is safe. No idempotency header is assumed. Unit tests use fake request executors, rate limiters, and clocks; no unit test contacts an external service.

## Database foundation

`FuzzyMatcherRepository` interpolates only schema-qualified identifiers that pass `SafeSqlIdentifierValidator`. Identifiers must contain exactly two safe SQL name parts; arbitrary SQL fragments, brackets, whitespace, semicolons, and comments are rejected before a connection is used.

Repository SQL is limited to parameterized `SELECT`, `INSERT`, `UPDATE`, and `DELETE` statements. Table schema changes are intentionally out of scope. Each operation creates and disposes its own connection; transactional mutations create one connection and one transaction for the complete operation.

## Processing foundation

CSV headers are matched case-insensitively after trimming surrounding header whitespace. Required headers remain `Scenario_id`, `XML_request`, and `Test_tags`; data values are not silently generated.

Request XML is parsed once with DTD processing prohibited and no resolver. The parsed document supplies SchemeCode and PolicyReference metadata while the original XML remains available on `BaselineScenarioCsvRow` for future API requests.

The initial XML fingerprint is a deterministic SHA-256 over a formatting-tolerant structural representation. It is not full W3C XML canonicalization. Whitespace-only indentation is ignored when elements contain child elements without meaningful text; mixed-content text is retained.

Tags are represented internally as a sorted, case-insensitive, duplicate-free `IReadOnlyList<string>`.

## Response schemas

`Scheme01-response.xsd` through `Scheme17-response.xsd` are non-production placeholder rulebooks. Each has a unique namespace and defines `PlaceholderResponse/PlaceholderAmount` as `xs:decimal`. They are copied to NUnit output and compiled by `SchemaRegistry` once per registered filename.

Real schema imports/includes must remain local and trusted. External schema resolution is rejected. Replace a placeholder only after adding the real route mapping, processor, and validation tests.

## Authoritative test asset

`TestAsset/baseline-scenarios.csv` is the single authoritative CSV and currently contains headings only:

```text
Scenario_id,XML_request,Test_tags
```

The CSV is the master scenario list. `Scenario_id` is a unique reference number, `XML_request` contains raw request XML, and `Test_tags` contains optional comma-separated labels. Do not move this file without updating configuration and future pipeline templates.

The loader must fail safely when the template has no data rows and must never delete database rows from an empty source. Headers are matched case-insensitively after surrounding whitespace is trimmed.

## Next step

Confirm one approved scheme's external contracts, then configure it privately and run a separately reviewed guarded integration test.