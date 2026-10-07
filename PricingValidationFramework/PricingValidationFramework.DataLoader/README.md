# Scenario Data Importer

The importer loads `xml_request` only, keyed by unique `Scenario_id`. It never writes `xml_response`: a newly loaded scenario has no baseline until the next Radar run, which calls the Radar API, stores the response in `xml_response` with `Status = PASS`, and reports the scenario as PASS without running the pricing comparison. Later Radar runs compare against that baseline.

## Ownership

- `CsvScenarioReader`: RFC4180 CSV parsing, including quoted XML and multiline values.
- `ScenarioDataLoader`: required-field/XML validation, duplicate detection, existing-record comparison, import planning, batching, cancellation, and summaries.
- `ImportSpool`: temporary normalized payload storage; memory retains IDs/offsets and a working batch, not all XML records.
- Core `IScenarioImportRepository` and `ScenarioImportRepository`: database lookups and transactional, parameterised inserts only. They reuse `SqlConnectionFactory.OpenAsync` and do not parse CSVs or decide whether a baseline is approved.

## Input Files

Local datasets belong under `PricingValidationFramework/data/import/`. Real CSVs there are ignored by Git; only synthetic files under `examples/` are tracked. Do not store customer XML in C# source folders or commit real datasets.

Request headers (the same shape as the `scenarios.csv` the CSV loader UI produces):

```csv
scenario_id,xml
```

`Quote_ref`, `Product_code` and `Scheme_code` are read from each request's XML:

| `xml_request` column | XML element |
|---|---|
| `Quote_ref` | `/Message/Policy/PolicyReference` |
| `Product_code` | `/Message/Policy/ProductCode` |
| `Scheme_code` | `/Message/Policy/SchemeCode` |

Each element must appear exactly once and be non-empty; otherwise the row is reported and nothing is imported. `Test_tags` is stored empty and is ignored when deciding whether an existing request is identical.

`--requests` takes a CSV file or a folder. A file is loaded as given. For a folder, the loader picks the newest `scenarios-{yyyyMMdd-HHmmss-fff}.csv` (the versioned files the CSV loader UI saves) by the timestamp in its **filename**, not the file's modified time, which changes when files are copied or checked out. Other CSVs in the folder, including an unversioned `scenarios.csv`, are ignored. If the folder has no versioned file, it falls back to `requests.csv`; if there's neither, the run fails with an issue. The chosen file is printed and recorded as `RequestFile` in the JSON summary.

Headers are case-insensitive and both are required. XML declarations are removed and DTDs are prohibited. This importer checks well-formed XML and the three identifier elements, not pricing correctness or XSD conformance. Offline validation cannot check for conflicts with existing rows; `import` checks them before any writes. The synthetic examples demonstrate import mechanics, not a production Radar request/response contract.

## Run Locally

From the repository root:

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- validate --requests PricingValidationFramework/data/import/examples/requests.csv --report TestResults/DataLoader/validation-summary.json
```

For database import, configure `DatabaseSettings__ConnectionString` directly in your environment or a secure configuration file containing the existing `DatabaseSettings.ConnectionString` section. Environment configuration takes precedence over `--settings`. Never pass a connection string as a command-line argument or commit it.

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- import --requests PricingValidationFramework/data/import/requests.csv --batch-size 500 --settings PricingValidationFramework/PricingValidationFramework.Tests/appsettings.json --report TestResults/DataLoader/import-summary.json
```

Exit codes: 0 success, 1 validation/import failure, 2 usage without a command, 130 cancellation. The JSON summary includes row counts, committed batches, rejected IDs/reasons, and cancellation state. It contains no XML or connection strings.

## Database Behavior

The target schema must already exist. Enforce a unique/primary key on `xml_request.Scenario_id`. `XML_request` is `NVARCHAR(MAX)`. The importer does not create tables or change database constraints. Table names use the connection user's existing schema resolution, like the Core readers.

Every input record is validated and spooled before database access. Existing records are checked for the complete dataset before writing. Identical requests are skipped (`Test_tags` is ignored in that comparison, since the CSV doesn't carry it). Conflicting records reject the run. There is deliberately no replacement/upsert switch.

Each batch uses one connection and serializable transaction, with `SET XACT_ABORT ON` so any error rolls the whole batch back. Rows are sent as parameters in multi-row `INSERT ... SELECT ... FROM (VALUES ...)` statements of up to 333 rows: six parameters per row keeps each statement under SQL Server's 2,100-parameter limit. Nothing is created in the database: no temporary tables, table types, stored procedures or bulk copy, so the loader needs only `SELECT` and `INSERT` on `xml_request`. Each statement skips any `Scenario_id` that appeared since preflight (`NOT EXISTS` with `UPDLOCK, HOLDLOCK`); if any row is skipped that way, the batch is rolled back and the run stops. SQL supplies `Create_date` in UTC. Existing audit fields are never overwritten by the loader.

Default batch size is 500; maximum is 1000 to stay below SQL Server's parameter limit. Transactions are batch-scoped, not dataset-scoped: if a later batch fails, earlier commits remain and are counted in the summary. Check database state and rerun the same immutable dataset to skip committed records. A lost connection during commit can make the last batch's outcome uncertain; never assume the entire run rolled back. No blind write retries are performed.

Allow temporary disk space for a normalized copy of the dataset, plus the input files. The spool is removed on disposal. Import timing depends on payload size, SQL indexes, and network latency; the 20,000-scenario test uses a fake repository and does not benchmark live SQL.

## Azure DevOps

Register `PricingValidationFramework/azure-pipelines-data-loader.yml` as a separate manual pipeline.

1. Configure an agent pool with .NET installation support and access to the target SQL Server. The YAML defaults to the self-hosted `Default` pool; set `agentPool` for your environment.
2. Create the `pricing-data-import` environment, or choose another with `importEnvironment`. Configure required approval and exclusive-lock checks in Azure DevOps before using a shared database. These checks cannot be configured by this YAML alone. Restrict environment and pipeline permissions to authorized operators.
3. Add secret variable `DataLoaderDatabaseConnectionString` for that environment. Use a dedicated database identity with only SELECT and INSERT permission on `xml_request`. It needs no permission to create anything, including temporary tables.
4. Choose `repository` for approved synthetic/test files, or `artifact` for a controlled dataset. Artifact mode requires a numeric pipeline definition ID and run ID and an artifact named `scenario-data` with `scenarios-{timestamp}.csv` file(s) or a `requests.csv` at its root; the newest versioned file is used. Do not select an unversioned latest artifact.
5. The first stage restores/builds/tests the importer, validates CSV/XML without SQL, and pins those exact files as `scenario-import-data`. Review `DataLoaderValidation` before approving the environment deployment.
6. The second stage downloads the current run's pinned dataset, checks database links/conflicts, and inserts through the Core repository. Review `DataLoaderImport` for counts and errors. Run Radar afterwards to create the new scenarios' baselines.

Dataset artifacts contain XML and may contain sensitive data. Restrict artifact access and retention appropriately. Do not publish the dataset publicly. Environment exclusive locking prevents overlapping import runs only when its check is actually configured; other tools writing these tables still need database constraints.