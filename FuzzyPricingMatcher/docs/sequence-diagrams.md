# FuzzyPricingMatcher Sequence Diagrams

## Purpose

This document shows the runtime order of the main workflows. The diagrams reflect the current implementation and are intended to help an architect review control flow, ownership, external boundaries, persistence order, and failure handling.

## Participants

```text
NUnit test fixture
    -> AutomationCompositionRoot
    -> configuration and validation
    -> loader or comparison workflow
    -> processing and routing services
    -> API and SQL infrastructure
    -> XML validation and amount extraction
    -> evidence and reporting
```

## 1. Composition And Service Startup

```mermaid
sequenceDiagram
    participant Test as NUnit fixture
    participant Root as AutomationCompositionRoot
    participant Config as MatcherConfiguration
    participant DI as ServiceCollection/ServiceProvider
    participant Validate as Global configuration validation

    Test->>Root: Create(testDirectory)
    Root->>Config: Load(basePath)
    Config->>Config: appsettings.json
    Config->>Config: optional appsettings.Local.json
    Config->>Config: environment variables
    Config-->>Root: MatcherConfiguration
    Root->>Validate: ValidateGlobal(configuration)
    Validate-->>Root: Valid or ConfigurationValidationException
    Root->>DI: Register configuration and services
    Root->>DI: BuildServiceProvider(ValidateOnBuild = true)
    DI-->>Root: ServiceProvider
    Root-->>Test: AutomationCompositionRoot
```

Live integration tests perform an explicit `FUZZY_RUN_INTEGRATION=true` check before creating or using the live composition root.

## 2. Baseline Loader End-to-End

```mermaid
sequenceDiagram
    participant Test as BaselineLoaderFixture
    participant Root as AutomationCompositionRoot
    participant Config as MatcherConfiguration
    participant CSV as CsvScenarioReader
    participant Guard as IntegrationConfigurationValidator
    participant Loader as LoaderSynchronizationService
    participant RepoAdapter as ProductionLoaderRepository
    participant APIAdapter as ProductionLoaderApiClient
    participant DB as FuzzyMatcherRepository
    participant API as XmlApiClient
    participant Response as SchemaLoaderResponseValidator
    participant Summary as LoaderSummaryWriter
    participant Files as Evidence/TestResults

    Test->>Test: Check FUZZY_RUN_INTEGRATION
    Test->>Root: Create(testDirectory)
    Root-->>Test: Composition root
    Test->>Root: Get MatcherConfiguration
    Test->>Root: Get CsvScenarioReader
    Test->>Test: FindSolutionRoot()
    Test->>CSV: Read(csvPath, requireDataRows = true)
    CSV->>CSV: Validate headers and rows
    CSV->>CSV: Normalize IDs and tags
    CSV->>CSV: Parse request XML metadata
    CSV->>CSV: Compute XML fingerprints
    CSV-->>Test: IReadOnlyList<PreparedBaselineScenario>
    Test->>Guard: ValidateLoader(csvPath, scenarios)
    Guard-->>Test: Approved configuration or failure
    Test->>Loader: Synchronize(scenarios, buildId)
    Loader->>RepoAdapter: GetByScenarioId(scenarioId)
    RepoAdapter->>DB: GetRequestAsync/GetResponseAsync
    DB-->>RepoAdapter: Existing request/response rows
    RepoAdapter-->>Loader: BaselineDatabaseSnapshot
    alt Scenario is new
        Loader->>APIAdapter: Fetch(LoaderApiRequest, route)
        APIAdapter->>API: SendAsync(XmlApiRequest)
        API-->>APIAdapter: ApiCallResult
        APIAdapter-->>Loader: LoaderApiResponse
        Loader->>Response: Validate(response, route)
        Response-->>Loader: Valid response
        Loader->>RepoAdapter: Insert(command)
        RepoAdapter->>DB: InsertBaselineAsync(command)
    else XML changed
        Loader->>APIAdapter: Fetch(LoaderApiRequest, route)
        APIAdapter->>API: SendAsync(XmlApiRequest)
        API-->>APIAdapter: ApiCallResult
        APIAdapter-->>Loader: LoaderApiResponse
        Loader->>Response: Validate(response, route)
        Response-->>Loader: Valid response
        Loader->>RepoAdapter: Update(command)
        RepoAdapter->>DB: UpdateBaselineAsync(command)
    else Tags changed only
        Loader->>RepoAdapter: UpdateTags(command)
        RepoAdapter->>DB: UpdateTagsAsync(command)
    else No changes
        Loader-->>Loader: Return Unchanged result
    end
    alt Every current scenario succeeded
        Loader->>RepoAdapter: GetAllScenarioIds()
        RepoAdapter->>DB: GetAllRequestScenarioIdsAsync()
        DB-->>RepoAdapter: Existing IDs
        Loader->>RepoAdapter: DeleteObsolete(command)
        RepoAdapter->>DB: DeleteObsoleteAsync(command)
    else Any scenario failed
        Loader-->>Loader: Skip obsolete deletion
    end
    Loader-->>Test: LoaderSynchronizationResult
    Test->>Summary: Write(summaryPath, result)
    Summary->>Files: Write loader summary
    Test->>Test: Assert result.Successful
```

### Loader safety property

Obsolete deletion is deliberately after all scenario processing. A partial failure prevents cleanup of potentially valid existing baseline data.

## 3. CSV Preparation Detail

```mermaid
sequenceDiagram
    participant Reader as CsvScenarioReader
    participant CSV as CsvHelper
    participant ID as ScenarioIdNormalizer
    participant XML as RequestXmlMetadataReader
    participant Tags as TagNormalizer
    participant Fingerprint as XmlFingerprintService
    participant Duplicate as DuplicateScenarioDetector

    Reader->>CSV: Open and read header
    CSV-->>Reader: Header indexes
    loop Each CSV row
        Reader->>CSV: Read row
        CSV-->>Reader: BaselineScenarioCsvRow
        Reader->>ID: Normalize(ScenarioId)
        ID-->>Reader: NormalizedScenarioId
        Reader->>XML: Read(XmlRequest)
        XML-->>Reader: RequestXmlMetadata
        Reader->>Tags: Normalize(TestTags)
        Tags-->>Reader: NormalizedTags
        Reader->>Fingerprint: CreateFingerprint(Document)
        Fingerprint-->>Reader: XmlFingerprint
        Reader-->>Reader: Create PreparedBaselineScenario
    end
    Reader->>Duplicate: Detect(csv rows)
    Duplicate-->>Reader: Duplicate results
    Reader-->>Reader: Return prepared scenarios or validation error
```

## 4. API Request Sequence

```mermaid
sequenceDiagram
    participant Caller as Loader adapter or comparison executor
    participant Client as XmlApiClient
    participant Resource as ApiResourceBuilder
    participant Factory as RestClientFactory
    participant Retry as ApiRetryPipeline
    participant Limit as ApiRateLimiter
    participant Executor as RestRequestExecutor
    participant Rest as RestSharp
    participant Endpoint as External XML API

    Caller->>Client: SendAsync(XmlApiRequest)
    Client->>Client: Validate route, endpoint, and raw XML
    Client->>Resource: Build(EndpointSettings, ApiDate)
    Resource->>Resource: Validate date placement and format
    Resource-->>Client: ApiResource
    Client->>Factory: GetClient(endpointName, endpoint)
    Factory->>Factory: Validate endpoint
    Factory->>Factory: Get or create cached authenticated RestClient
    Factory-->>Client: RestClient
    Client->>Retry: ExecuteAsync(operationName, attempt)
    Retry->>Limit: WaitAsync()
    Limit-->>Retry: Permit for physical attempt
    Retry->>Client: Invoke attempt delegate
    Client->>Client: Create new RestRequest(Method.Post)
    Client->>Client: Add Content-Type header
    Client->>Client: Add exact RawXml body
    alt Query-string date
        Client->>Client: Add date query parameter
    else Path date
        Client->>Client: Use date in ResourcePath
    end
    Client->>Executor: ExecuteAsync(RestClient, RestRequest)
    Executor->>Rest: ExecuteAsync(request)
    Rest->>Endpoint: HTTP POST
    Endpoint-->>Rest: HTTP response
    Rest-->>Executor: RestResponse
    Executor-->>Client: RestResponse
    Client->>Client: Convert status/content/retry-after
    Client-->>Retry: ApiCallResult
    alt Successful 2xx
        Retry-->>Caller: Success result with raw XML
    else Retryable exception or status
        Retry->>Retry: Backoff, jitter, and Retry-After handling
        Retry->>Limit: WaitAsync() for next physical attempt
    else Non-retryable failure or exhausted attempts
        Retry-->>Caller: Failed ApiCallResult or exception
    end
```

The API request does not send `QuoteRef` as a separate header or parameter. The policy reference is sent only if it exists in the raw XML body.

## 5. API Retry Sequence

```mermaid
flowchart TD
    Start[ExecuteAsync begins] --> Count[Calculate maximum attempts]
    Count --> Cancel[Check cancellation]
    Cancel --> Permit[Acquire shared rate-limit permit]
    Permit --> Physical[Run one physical HTTP attempt]
    Physical --> Exception{Exception?}
    Exception -->|Caller cancellation| Cancelled[Propagate cancellation]
    Exception -->|Transient transport exception| RetryException{Attempts remain?}
    RetryException -->|Yes| Delay[Exponential delay + jitter]
    RetryException -->|No| Exhausted[Return/propagate final failure]
    Exception -->|No exception| Status{HTTP result}
    Status -->|2xx| Success[Return success]
    Status -->|408, 429, 502, 503, 504| RetryStatus{Attempts remain?}
    RetryStatus -->|Yes| RetryAfter[Honor Retry-After minimum]
    RetryAfter --> Delay
    RetryStatus -->|No| FinalFailure[Return failed ApiCallResult]
    Status -->|Other status| FinalFailure
    Delay --> Cancel
```

Every physical attempt, including a retry, passes through the shared rate limiter.

## 6. SQL Read Sequence

```mermaid
sequenceDiagram
    participant Workflow as Loader or comparison workflow
    participant Repo as FuzzyMatcherRepository
    participant Retry as DatabaseRetryExecutor
    participant Factory as SqlConnectionFactory
    participant SQL as SqlConnection
    participant Dapper as Dapper
    participant DB as SQL Server

    Workflow->>Repo: GetScenarioForComparisonAsync(scenarioId)
    Repo->>Retry: ExecuteAsync(operation, query)
    Retry->>Factory: Create()
    Factory-->>Retry: SqlConnection
    Retry->>SQL: OpenAsync(cancellationToken)
    SQL->>DB: Open connection
    DB-->>SQL: Connection opened
    Repo->>Dapper: QueryAsync<DatabaseRequestRecord>(CommandDefinition)
    Dapper->>DB: Parameterized request SELECT
    DB-->>Dapper: Request rows
    Dapper-->>Repo: DatabaseRequestRecord rows
    Repo->>Dapper: QueryAsync<DatabaseResponseRecord>(CommandDefinition)
    Dapper->>DB: Parameterized response SELECT
    DB-->>Dapper: Response rows
    Dapper-->>Repo: DatabaseResponseRecord rows
    Repo->>Repo: RequireAtMostOne per table
    Repo-->>Workflow: DatabaseScenarioSnapshot
    alt Transient SQL failure
        Retry->>Retry: Backoff and retry operation
    else Duplicate rows
        Repo-->>Workflow: DatabaseConsistencyException
    end
```

Dapper maps SQL aliases such as `Scenario_id AS ScenarioId` to C# properties such as `ScenarioId`. Values are parameters. Configured table identifiers are validated before inclusion in SQL text.

## 7. SQL Mutation Sequence

```mermaid
sequenceDiagram
    participant Workflow as Loader or comparison workflow
    participant Repo as FuzzyMatcherRepository
    participant Plans as SqlCommandPlanFactory
    participant Mutation as SqlMutationExecutor
    participant Session as Mutation session/transaction
    participant DB as SQL Server

    Workflow->>Repo: Insert/Update/Pass/Fail mutation
    Repo->>Plans: Create parameterized SqlCommandPlan list
    Plans-->>Repo: SQL plans and parameters
    Repo->>Mutation: Execute plan(s)
    Mutation->>Session: Create session
    Session->>DB: Begin transaction when required
    loop Each command plan
        Session->>DB: Execute parameterized SQL
        DB-->>Session: Rows affected
    end
    Session->>DB: Commit transaction
    Mutation-->>Repo: Completed
    Repo-->>Workflow: Mutation result
```

## 8. Comparison End-to-End

```mermaid
sequenceDiagram
    participant Fixture as ComparisonTests
    participant Repo as FuzzyMatcherRepository
    participant Executor as ComparisonScenarioExecutor
    participant Metadata as RequestXmlMetadataReader
    participant Route as ConfiguredRouteResolver
    participant API as XmlApiClient
    participant Validation as ResponseValidationService
    participant Amount as AmountReaderRegistry
    participant Threshold as ThresholdEvaluator
    participant Evidence as ScenarioEvidenceWriter
    participant Output as NUnit/NLog

    Fixture->>Repo: SelectScenariosAsync(tags, matchMode)
    Repo-->>Fixture: DatabaseScenarioSelection list
    loop Each selected NUnit scenario
        Fixture->>Executor: Execute(scenario, comparisonInput)
        Executor->>Repo: GetScenarioForComparisonAsync(scenarioId)
        Repo-->>Executor: DatabaseScenarioSnapshot
        Executor->>Evidence: Save(initial ScenarioEvidence)
        Executor->>Metadata: Read(stored request XML)
        Metadata-->>Executor: SchemeCode and PolicyReference
        Executor->>Route: Resolve(SchemeCode)
        Route-->>Executor: Endpoint and route
        Executor->>API: SendAsync(XmlApiRequest)
        API-->>Executor: ApiCallResult with raw current XML
        alt API unsuccessful
            Executor->>Repo: UpdateComparisonFailAsync
            Executor->>Evidence: Save failure evidence
            Executor->>Output: Log outcome
            Executor-->>Fixture: Failed ComparisonExecutionResult
        else API successful
            Executor->>Validation: ValidateAndReadAmount(current response)
            Validation->>Amount: Resolve amount reader
            Amount-->>Validation: Current amount
            Validation-->>Executor: Validated current amount
            Executor->>Validation: ValidateAndReadAmount(stored baseline)
            Validation->>Amount: Resolve amount reader
            Amount-->>Validation: Baseline amount
            Validation-->>Executor: Validated baseline amount
            Executor->>Threshold: Evaluate(current, baseline, min, max)
            Threshold-->>Executor: ThresholdResult
            alt Threshold passed
                Executor->>Repo: UpdateComparisonPassAsync
                Executor->>Evidence: Save final pass evidence
                Executor->>Output: Log pass
                Executor-->>Fixture: Passed result
            else Threshold failed
                Executor->>Repo: UpdateComparisonFailAsync
                Executor->>Evidence: Save final failure evidence
                Executor->>Output: Log failure
                Executor-->>Fixture: Failed result
            end
        end
        Fixture->>Fixture: Assert database updated
        Fixture->>Fixture: Assert comparison passed
    end
```

## 9. Evidence Timing

Evidence starts before the API call:

```text
Initial evidence
    -> request XML
    -> stored baseline XML
    -> no API XML yet
    -> Started outcome

API call

Final evidence
    -> request XML
    -> stored baseline XML
    -> API XML when received
    -> final outcome and error
```

This preserves diagnostic material even when the external call fails before returning a response.

## 10. Failure Outcomes

```text
Configuration failure
    -> integration configuration exception
    -> test setup failure

CSV/request validation failure
    -> CSV or request XML validation exception
    -> loader does not proceed with invalid scenario

Database unavailable
    -> database retry policy
    -> DatabaseFailed result when exhausted

Duplicate database rows
    -> DatabaseConsistencyException
    -> comparison or loader failure

API transport failure
    -> ApiTransportException
    -> API retry pipeline
    -> API failure result when exhausted

Retryable HTTP status
    -> 408, 429, 502, 503, or 504
    -> retry with limiter and backoff

Response schema failure
    -> validation failure
    -> comparison/loader failure

Threshold outside range
    -> ThresholdFailed
    -> response table Fail update
    -> NUnit failure
```

## 11. Architect Review Focus

These sequence points are useful review checkpoints:

1. Is scenario selection correctly owned by SQL Server while NUnit remains an execution/reporting engine?
2. Is it safe to retry every configured API `POST`, or should retry safety be route-specific?
3. Should API and SQL workflow calls be fully asynchronous end-to-end?
4. Is the order of response validation, persistence, and evidence capture correct for operational recovery?
5. Should a Pass update replace the stored baseline response, or should current comparison output be stored separately?
6. Are raw XML evidence retention and attachment behavior acceptable for sensitive data?
7. Is the database consistency rule of at most one request and response row sufficient?
8. Should amount extraction be versioned or selected by a stronger schema/route contract?
9. Is the configured API timeout actually applied at the RestSharp boundary?
10. Are integration guards and placeholder checks strong enough for CI and developer workflows?
