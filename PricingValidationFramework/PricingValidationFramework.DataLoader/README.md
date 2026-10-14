# Scenario Data Importer

The DataLoader has three commands:

- `validate` checks a requests CSV without a database.
- `import` loads `[Xml].[Request]` only, keyed by unique `Scenario_id`. It never writes `[Xml].[Response]`.
- `seed` creates the first baseline for every scenario that has no `[Xml].[Response]` row: it calls Radar and stores the response with `Status = PASS`, without the pricing comparison. See [Seed baselines](#seed-baselines).

Radar compares only. A scenario with no baseline is a Radar ERROR (`MissingBaseline`), so run `seed` after an import and before Radar.

## Ownership

- `CsvScenarioReader`: RFC4180 CSV parsing, including quoted XML and multiline values.
- `ScenarioDataLoader`: required-field/XML validation, duplicate detection, existing-record comparison, import planning, batching, cancellation, and summaries.
- `ImportSpool`: temporary normalized payload storage; memory retains IDs/offsets and a working batch, not all XML records.
- `SeedCommand`: the `seed` command's options and settings; Core `BaselineSeeder` finds scenarios without a baseline, calls Radar through `RadarPricingService.CreateBaselineAsync`, and inserts through `ResultUpdater.TryInsertBaselineAsync`.
- Core `IScenarioImportRepository` and `ScenarioImportRepository`: database lookups and transactional, parameterised inserts and updates. They reuse `SqlConnectionFactory.OpenAsync` and do not parse CSVs or decide whether a baseline is approved.

## Input Files

Local datasets belong under `PricingValidationFramework/data/import/`. Real CSVs there are ignored by Git; only synthetic files under `examples/` are tracked. Do not store customer XML in C# source folders or commit real datasets.

Request headers (the same shape as the `scenarios.csv` the CSV loader UI produces), plus an optional `test_tags`:

```csv
scenario_id,xml[,test_tags]
```

`Quote_ref`, `Product_code` and `Scheme_code` are read from each request's XML:

| `[Xml].[Request]` column | XML element |
|---|---|
| `Quote_ref` | `/Message/Policy/PolicyReference` |
| `Product_code` | `/Message/Policy/ProductCode` |
| `Scheme_code` | `/Message/Policy/SchemeCode` |

Each element must appear exactly once and be non-empty; otherwise the row is reported and nothing is imported.

An optional third column, `test_tags`, gives each scenario's tags, separated by commas or semicolons, e.g. `smoke,regression`. Tags may use letters, digits, `_`, `-` and `.` (at most 64 characters each); duplicates are removed and they are stored comma-separated in `Test_tags`. An invalid tag is reported and nothing is imported. Tags are set only when a scenario is first inserted: like every column except the XML, they are not compared or changed for an existing scenario, so change an existing scenario's tags in the database. Without the column, `Test_tags` is stored empty. Radar and ICE can then select by one tag with `SCENARIO_TAG`.

`--requests` takes a CSV file or a folder. A file is loaded as given. For a folder, the loader picks the newest `scenarios-{yyyyMMdd-HHmmss-fff}.csv` (the versioned files the CSV loader UI saves) by the timestamp in its **filename**, not the file's modified time, which changes when files are copied or checked out. Other CSVs in the folder, including an unversioned `scenarios.csv`, are ignored. If the folder has no versioned file, it falls back to `requests.csv`; if there's neither, the run fails with an issue. The chosen file is printed and recorded as `RequestFile` in the JSON summary.

Headers are case-insensitive and both are required. XML declarations are removed and DTDs are prohibited. This importer checks well-formed XML and the three identifier elements, not pricing correctness. Offline validation cannot check for conflicts with existing rows; `import` checks them before any writes. The synthetic examples demonstrate import mechanics, not a production Radar request/response contract.

## Run Locally

From the repository root:

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- validate --requests PricingValidationFramework/data/import/examples/requests.csv --report TestResults/DataLoader/validation-summary.json
```

For database import, configure `DatabaseSettings__ConnectionString` directly in your environment or a secure configuration file containing the existing `DatabaseSettings.ConnectionString` section. Environment configuration takes precedence over `--settings`. Never pass a connection string as a command-line argument or commit it.

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- import --requests PricingValidationFramework/data/import/requests.csv --batch-size 100 --settings PricingValidationFramework/PricingValidationFramework.Tests/appsettings.json --report TestResults/DataLoader/import-summary.json
```

Exit codes: 0 success, 1 validation/import failure, 2 usage without a command, 130 cancellation. The JSON summary includes inserted, updated and skipped request counts, committed batches, rejected IDs/reasons, and cancellation state. It contains no XML or connection strings.

## Seed Baselines

`seed` finds every `[Xml].[Request]` scenario with no `[Xml].[Response]` row (of any status), calls Radar for each and inserts the response as its `PASS` baseline. It doesn't compare anything, and it never reads or changes a scenario that already has a baseline, so it is safe to rerun: a second run finds nothing to do.

```text
DataLoader seed [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>] [--max-parallel <1-16>]
```

Settings are read from the `--settings` JSON (optional), with environment variables on top. They use the same names as the Radar tests and pipeline:

| Setting | JSON | Environment variable |
|---|---|---|
| Database | `DatabaseSettings.ConnectionString`, `DatabaseSettings.AuditUser` | `DatabaseSettings__ConnectionString`, `DatabaseSettings__AuditUser` |
| Radar endpoints | `RadarSettings.Endpoints.Endpoint1.BaseUrl` / `.ApiKeyHeaderName` / `.ApiKeyValue` (and `Endpoint2`, `Endpoint3`) | `RadarSettings__Endpoints__Endpoint1__BaseUrl` ... |
| Request time | `PipelineSettings.RequestTime` | `RADAR_REQUEST_DATETIME` (`--request-time` wins over both) |
| Routing | `PricingProfiles.MappingFile` (default `Radar/mappings.json`) | `PricingProfiles__MappingFile` |
| Rate limit / retries | `RadarRateLimitSettings`, `RetrySettings` (defaults: 2 requests per second, 3 API retries) | `RadarRateLimitSettings__RequestsPerSecond` ... |

The request time is UTC `yyyy-MM-ddZHH:mm:ss`, rounded down to the hour, or `now` (the default) for the current hour. It's resolved once, so every scenario in a run uses the same time.

Only the endpoints your scenarios use need to be set. An endpoint whose URL, header and key are all empty is ignored, so a local settings file can fill in `Endpoint1` and leave the others blank. A partly filled endpoint is rejected. A scenario whose route names an endpoint that isn't configured fails with `EndpointResolution`; one whose product and scheme match no route in `mappings.json` fails with `RouteResolution`. Neither calls Radar.

The summary (default `TestResults/DataLoader/seed-summary.json`) lists `RequestTime`, `ScenariosWithoutBaseline`, `Seeded`, `AlreadyExisted` (a baseline written by another run in the meantime; left unchanged), `Failed`, `NotRun` (after cancellation), any start-up `Issues`, and one result per scenario with its route, outcome, failure stage and error. It contains no XML, connection strings or keys. Exit codes: 0 every scenario seeded (or none needed it), 1 any failure or a configuration problem, 130 cancelled.

The database identity needs SELECT on `[Xml].[Request]` and `[Xml].[Response]` and INSERT on `[Xml].[Response]`. Nothing is created in the database.

### Run it locally

1. Copy `data/import/examples/local-settings.example.json` to `data/import/local-settings.json` (ignored by Git) and fill in the connection string and at least the endpoint your scenarios use. Keep keys out of tracked files.
2. Check `config/radar/mappings.json` has a route for your scenarios' `Product_code` and `Scheme_code`.
3. From the `PricingValidationFramework` folder:

```bash
dotnet run --project PricingValidationFramework.DataLoader -- seed --settings data/import/local-settings.json --max-parallel 1
```

To debug in VS Code, open the `PricingValidationFramework` folder and choose **3. DataLoader: seed baselines into [Xml].[Response]** in Run and Debug. The `.vscode` folder also has **1. validate** and **2. import** configurations using the same settings file, so a full local run is 1, 2, 3 and then the Radar tests, which read the same `local-settings.json` (its `PipelineSettings` section holds Radar's thresholds and filters). To debug Radar or ICE, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process. Breakpoints that are useful: `BaselineSeeder.RunAsync` (the scenarios found), `RadarPricingService.CreateBaselineAsync` (route and URL), and `ResultUpdater.TryInsertBaselineAsync` (the insert).

## Database Behavior

The target schema must already exist. Enforce a unique/primary key on `[Xml].[Request].Scenario_id`. `XML_request` is `VARCHAR(MAX)` and its parameter is explicitly bound as ANSI MAX-length text. Ensure the database code page or UTF-8 collation supports the XML's characters. The importer does not create tables or change database constraints. Table names use the connection user's existing schema resolution, like the Core readers.

Every input record is validated and spooled before database access. Existing records are checked for the complete dataset before writing. New IDs are inserted. An existing ID is compared on its XML only, after normalising: identical XML is skipped, any other difference updates `XML_request` and nothing else. `Quote_ref`, `Product_code`, `Scheme_code`, `Test_tags` and the creation date are neither compared nor changed, so if the new XML has a different product or scheme, those columns keep their old values and Radar still routes on them; use a new scenario ID when a scenario really changes product or scheme. Stored XML that can't be parsed counts as different and is replaced. `import` never changes `[Xml].[Response]` or calls Radar.

Each batch uses one connection and serializable transaction, with `SET XACT_ABORT ON`. Inserts and updates use separate parameterised `VALUES` statements of up to 333 rows: six parameters per row keeps each statement under SQL Server's 2,100-parameter limit. Nothing is created in the database: no temporary tables, table types, stored procedures or bulk copy, so the loader needs `SELECT`, `INSERT` and `UPDATE` on `[Xml].[Request]`. An update target that no longer exists, or an insert ID that appeared since preflight, rolls the batch back. Counts are returned only after commit. Inserts set `row_creationdate`, `row_createdby`, `row_modifieddate` and `row_modifiedby`; updates set `row_modifieddate` and `row_modifiedby` and preserve the creation columns and tags. Dates are UTC (`SYSUTCDATETIME()`). The user is `DatabaseSettings__AuditUser` (or `DatabaseSettings.AuditUser` in the `--settings` file) when set, otherwise the SQL login (`SUSER_SNAME()`).

Default batch size is 100 for large XML payloads; maximum is 1000, with each batch split into SQL statements of at most 333 rows. Transactions are batch-scoped, not dataset-scoped: if a later batch fails, earlier commits remain and are counted in the summary. Check database state and rerun the same immutable dataset to skip committed inserts and updates. A lost connection during commit can make the last batch's outcome uncertain; never assume the entire run rolled back. No blind write retries are performed.

Allow temporary disk space for a normalized copy of the dataset, plus the input files. The spool is removed on disposal. Import timing depends on payload size, SQL indexes, and network latency; the 20,000-scenario test uses a fake repository and does not benchmark live SQL.

## Azure DevOps

Register `PricingValidationFramework/azure-pipelines-data-loader.yml` as a separate manual pipeline.

1. Configure an agent pool with .NET installation support and access to the target SQL Server. The YAML defaults to the self-hosted `Default` pool; set `agentPool` for your environment.
2. The Import stage targets the `pricing-data-import` environment (or another chosen with `importEnvironment`). Azure DevOps creates it on the first run if it doesn't exist. **Import is auto-approved: it runs as soon as Validate succeeds.** Do not add an Approvals check to the environment, as that would make every run wait for sign-off. Add an Exclusive lock check so two imports can't write at the same time; checks are configured in Azure DevOps, not in this YAML. Restrict environment and pipeline permissions to authorized operators.
3. Add secret variable `DataLoaderDatabaseConnectionString` for that environment. Use a dedicated database identity with SELECT, INSERT and UPDATE permission on `[Xml].[Request]`. It needs no permission to create anything, including temporary tables.
4. Choose `repository` for approved synthetic/test files, or `artifact` for a controlled dataset. Artifact mode requires a numeric pipeline definition ID and run ID and an artifact named `scenario-data` with `scenarios-{timestamp}.csv` file(s) or a `requests.csv` at its root; the newest versioned file is used. Do not select an unversioned latest artifact.
5. The first stage restores/builds/tests the importer, validates CSV/XML without SQL, and pins those exact files as `scenario-import-data`. It also resolves the `requestDateTime` parameter (default `now`) with Core's `RequestTimeFormatter`, the same rule Radar uses: UTC, `yyyy-MM-ddZHH:mm:ss`, rounded down to the hour, with `now` meaning the current hour at validation. An invalid value fails this stage. The resolved time is shown in the log and in `validation-summary.json` (`RequestTime`). Import starts automatically once this stage succeeds; a failed validation stops the run before any database write. Review `DataLoaderValidation` for the details of any rejected rows.
6. The second stage downloads the current run's pinned dataset, inserts new scenarios and updates changed XML through the Core repository. Review `DataLoaderImport` for counts and errors.
7. When `seedBaselines` is true (the default), a third stage, **Seed baselines**, runs `DataLoader seed --request-time <time resolved in step 5>`. Every scenario without an `[Xml].[Response]` row gets its first Radar response stored as its PASS baseline; scenarios that already have one are not read or changed. `seedMaxParallel` (default 4) sets how many scenarios are seeded at once; the Radar rate limit still applies. The summary is published as `SeedSummary`. The stage fails if any scenario could not be seeded. It needs `RadarDatabaseConnectionString` (with SELECT on both tables and INSERT on `[Xml].[Response]`), the three endpoint URL/header/key variable sets, all defined (an unused endpoint's three may be empty, and is then ignored), and an agent that can reach the Radar endpoints. It doesn't need thresholds. Set `seedBaselines` to false to load only, and run `seed` later.
8. To seed without importing, tick `seedOnly`. Validate and Import are skipped, and only **Seed baselines** runs, for the scenarios already in `[Xml].[Request]` that have no baseline. It uses `requestDateTime` directly (`now` means the current UTC hour when the stage runs) and needs no dataset parameters. `seedOnly` overrides `seedBaselines`.

Dataset artifacts contain XML and may contain sensitive data. Restrict artifact access and retention appropriately. Do not publish the dataset publicly. Environment exclusive locking prevents overlapping import runs only when its check is actually configured; other tools writing these tables still need database constraints.