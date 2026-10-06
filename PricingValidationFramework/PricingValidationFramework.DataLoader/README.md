# Scenario Data Importer

The importer loads `xml_request` and `xml_response`, linked by unique `Scenario_id`. It is separate from the CSV-authoring UI and Radar validation runs.

## Ownership

- `CsvScenarioReader`: RFC4180 CSV parsing, including quoted XML and multiline values.
- `ScenarioDataLoader`: required-field/XML validation, duplicate detection, existing-record comparison, import planning, batching, cancellation, and summaries.
- `ImportSpool`: temporary normalized payload storage; memory retains IDs/offsets and a working batch, not all XML records.
- Core `IScenarioImportRepository` and `ScenarioImportRepository`: database lookups and transactional bulk inserts only. They reuse `SqlConnectionFactory.OpenAsync` and do not parse CSVs or decide whether a baseline is approved.

## Input Files

Local datasets belong under `PricingValidationFramework/data/import/`. Real CSVs there are ignored by Git; only synthetic files under `examples/` are tracked. Do not store customer XML in C# source folders or commit real datasets.

Request headers:

```csv
Scenario_id,Quote_ref,Product_code,Schem_code,XML_request,Test_tags
```

Response headers:

```csv
Scenario_id,XML_Response,Build_id,Status
```

Headers are case-insensitive, but `Schem_code` is the actual database spelling. All listed headers are required. Tags may be empty. An empty response `Build_id` uses `--build-id`, then `BUILD_BUILDID`, then `local-import`. Status must explicitly be PASS, FAIL, or ERROR; the loader never marks every response PASS automatically. Only approved baselines should carry PASS.

Response payloads may be XML or a JSON object with a top-level `response` XML string. JSON is parsed, not stripped with string replacement. XML declarations are removed and DTDs are prohibited. This importer checks well-formed XML, not pricing correctness or XSD conformance; Radar performs contract and pricing validation separately.

Provide requests only, responses only, or both. Response-only imports require existing parent requests. Offline validation cannot verify database links or conflicts; `import` checks them before any writes. The synthetic examples demonstrate import mechanics, not a production Radar request/response contract.

## Run Locally

From the repository root:

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- validate --requests PricingValidationFramework/data/import/examples/requests.csv --responses PricingValidationFramework/data/import/examples/responses.csv --report TestResults/DataLoader/validation-summary.json
```

For database import, configure `DatabaseSettings__ConnectionString` directly in your environment or a secure configuration file containing the existing `DatabaseSettings.ConnectionString` section. Environment configuration takes precedence over `--settings`. Never pass a connection string as a command-line argument or commit it.

```powershell
dotnet run --project PricingValidationFramework/PricingValidationFramework.DataLoader -- import --requests PricingValidationFramework/data/import/requests.csv --responses PricingValidationFramework/data/import/responses.csv --batch-size 500 --settings PricingValidationFramework/PricingValidationFramework.Tests/appsettings.json --report TestResults/DataLoader/import-summary.json
```

Exit codes: 0 success, 1 validation/import failure, 2 usage without a command, 130 cancellation. The JSON summary includes row counts, committed batches, rejected IDs/reasons, and cancellation state. It contains no XML or connection strings.

## Database Behavior

The target schema must already exist. Enforce a unique/primary key on each table's `Scenario_id` and a foreign key from `xml_response.Scenario_id` to `xml_request.Scenario_id`. The importer does not create tables or change database constraints. Table names use the connection user's existing schema resolution, like the Core readers.

Every input record is validated and spooled before database access. Existing records are checked for the complete dataset before writing. Identical request records or response XML/status are skipped; response build/audit metadata is not rewritten on a re-import. Conflicting records reject the run. There is deliberately no replacement/upsert switch.

Each batch uses one connection and serializable transaction. Typed temporary staging tables mirror the real columns; `SqlBulkCopy` stages rows, requests are inserted first, and responses are linked by `Scenario_id`. SQL guards against duplicates and missing parents, including races after preflight. SQL supplies `Create_date` and `Last_updated` in UTC. Existing audit fields are never overwritten by the loader.

Default batch size is 500; maximum is 1000 to stay below SQL Server's parameter limit. Transactions are batch-scoped, not dataset-scoped: if a later batch fails, earlier commits remain and are counted in the summary. Check database state and rerun the same immutable dataset to skip committed records. A lost connection during commit can make the last batch's outcome uncertain; never assume the entire run rolled back. No blind write retries are performed.

Allow temporary disk space for a normalized copy of the dataset, plus the input files. The spool is removed on disposal. Import timing depends on payload size, SQL indexes, and network latency; the 20,000-scenario test uses a fake repository and does not benchmark live SQL.

## Azure DevOps

Register `PricingValidationFramework/azure-pipelines-data-loader.yml` as a separate manual pipeline.

1. Configure an agent pool with .NET installation support and access to the target SQL Server. The YAML defaults to the self-hosted `Default` pool; set `agentPool` for your environment.
2. Create the `pricing-data-import` environment, or choose another with `importEnvironment`. Configure required approval and exclusive-lock checks in Azure DevOps before using a shared database. These checks cannot be configured by this YAML alone. Restrict environment and pipeline permissions to authorized operators.
3. Add secret variable `DataLoaderDatabaseConnectionString` for that environment. Use a dedicated database identity with SELECT/INSERT permission on the two tables and permission to create local temporary tables, not schema-altering permissions.
4. Choose `repository` for approved synthetic/test files, or `artifact` for a controlled dataset. Artifact mode requires a numeric pipeline definition ID and run ID and an artifact named `scenario-data` with `requests.csv` and/or `responses.csv` at its root. Do not select an unversioned latest artifact.
5. The first stage restores/builds/tests the importer, validates CSV/XML without SQL, and pins those exact files as `scenario-import-data`. Review `DataLoaderValidation` before approving the environment deployment.
6. The second stage downloads the current run's pinned dataset, checks database links/conflicts, and inserts through the Core repository. Review `DataLoaderImport` for counts and errors. Run Radar separately after loading approved baselines.

Dataset artifacts contain XML and may contain sensitive data. Restrict artifact access and retention appropriately. Do not publish the dataset publicly. Environment exclusive locking prevents overlapping import runs only when its check is actually configured; other tools writing these tables still need database constraints.