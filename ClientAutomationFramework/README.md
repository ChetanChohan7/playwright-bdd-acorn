# ClientAutomationFramework

Three projects: `ClientAutomationFramework.Core` (reusable building blocks - no test framework dependency) and `ClientAutomationFramework.Tests` (NUnit fixtures that wire those blocks into the two runnable checks) under `src/`, plus `pricing-xml-csv-loader/` - a separate, self-contained CLI + browser UI for authoring the scenario CSV, imported whole from its own repo and kept segregated in its own folder rather than merged into `src/`.

## Folder structure

```
ClientAutomationFramework/
├── ClientAutomationFramework.slnx
├── sql/001_create_tables.sql
├── src/
│   ├── ClientAutomationFramework.Core/
│   │   ├── Configuration/    DatabaseSettings, AppConfiguration (loader)
│   │   ├── Database/         SqlConnectionFactory, RequestDataReader, ResponseDataReader, ResultUpdater
│   │   ├── ExternalAPIAccess/ XmlApiClient, JsonApiClient, EndpointResolver, SchemeConfig
│   │   ├── Validation/       XsdValidator, JsonResponseValidator
│   │   ├── Extraction/       XmlValueExtractor, JsonValueExtractor
│   │   ├── Matching/         XmlToleranceMatcher, JsonExactMatcher, MatchResult, MatchSettings
│   │   ├── Logging/          TestRunLogger
│   │   ├── Reporting/        ReportManager
│   │   └── Models/           ScenarioRequest, ScenarioResponse, NewScenarioResponse
│   └── ClientAutomationFramework.Tests/
│       ├── Integration/Xml/XmlToleranceTests.cs
│       ├── Integration/Json/JsonComparisonTests.cs
│       ├── TestBase/TestSetup.cs
│       ├── TestBase/JsonFileResponseSource.cs   TEMPORARY database stand-in, see below
│       ├── TestAssets/Xsd/TechPriceResponse.xsd
│       ├── appsettings.json / appsettings.Local.example.json
│       └── nlog.config
├── data/xml-response.json        TEMPORARY - stands in for the XML_Response table
└── pricing-xml-csv-loader/      everything below is self-contained; see its own section
    ├── azure-pipelines.yml      CI for ui/ only (test + deploy to Azure Static Web Apps)
    ├── scenario-tool.bat        Windows wrapper for PricingXml.ScenarioTool
    ├── launch-ui.bat            Windows launcher for ui/index.html + csv-viewer.html
    ├── edits.example.json / elements.example.json
    ├── request_xmls/            sample scenario-*.xml input files
    ├── ui/                      browser UI (index.html, csv-viewer.html, scenario-loader.js + tests)
    └── src/PricingXml.ScenarioTool/   CLI: build-csv, update-csv, add-elements
```

## pricing-xml-csv-loader - scenario CSV authoring

Imported whole from a separate repo of the same name, kept in its own top-level folder (own project name/namespace too - `PricingXml.ScenarioTool`, not `ClientAutomationFramework.*`) rather than folded into `src/`, so it stays segregated: everything it needs lives under `pricing-xml-csv-loader/`, and nothing under `src/` references it. `ClientAutomationFramework.slnx` includes its `.csproj` (in its own `/pricing-xml-csv-loader/src/` solution folder, separate from `/src/`) so it still builds and shows up in an IDE alongside the other two projects, but it's otherwise independent - no project references either direction.

It builds and maintains `scenarios.csv` (`scenario_id`, `xml`) from files in `request_xmls/`, and bulk-edits that CSV - either a single field (`update-csv`) or repeated sibling elements (`add-elements`) - driven by JSON edit files rather than code changes. `ui/index.html` is a no-install browser equivalent of the same three operations, with `ui/csv-viewer.html` as a read-only companion; `ui/scenario-loader.js` holds the shared logic, covered by `ui/scenario-loader.test.js` (`cd pricing-xml-csv-loader/ui && npm install && npm test` - 19 tests, all passing).

```bash
dotnet run --project pricing-xml-csv-loader/src/PricingXml.ScenarioTool -- build-csv
dotnet run --project pricing-xml-csv-loader/src/PricingXml.ScenarioTool -- update-csv --edits pricing-xml-csv-loader/edits.example.json
dotnet run --project pricing-xml-csv-loader/src/PricingXml.ScenarioTool -- add-elements --elements pricing-xml-csv-loader/elements.example.json
```

`azure-pipelines.yml`'s trigger path, `workingDirectory` and `SourceFolder` are written assuming `pricing-xml-csv-loader/` itself is the pipeline's configured repo root - if it ends up wired up against a repo root further up instead, every path in that file needs the extra prefix (a comment at its top spells this out).

**This does not close the loop with the rest of the solution.** `scenarios.csv` is not loaded into the `XML_Requests` table that `RequestDataReader` reads from - the source project was explicit that it only covers CSV authoring, not database loading ("a deliberately trimmed slice of a larger framework that also loads scenarios... into a test database"). Populating `XML_Requests` from `scenarios.csv` is still a manual step (or unwritten loader) until something is added to bridge them.

**Left behind, deliberately**: `create-pricing-xml-csv-loader.bat`, the source project's self-contained "recreate this project from an embedded base64 archive" generator for machines with no git access. It was an explicit stand-in for "clone the repo" in a project that wasn't under git yet ("planned but deliberately not done here") - now that this is git-tracked inside `ClientAutomationFramework`, a normal clone/checkout replaces it, and the embedded snapshot would just go stale.

## How the two checks work

**`Integration/Xml/XmlToleranceTests`** - one test per row in `XML_Requests` (via `TestCaseSource`, optionally filtered by the `ScenarioId` NUnit parameter): reads the last stored `XML_Response` for that `scenario_id`, sends the request XML through `XmlApiClient`, extracts the `techPrice/premiumPriceComponent` amount with `XmlValueExtractor`, and asserts it's within `Match:MinimumThreshold`/`MaximumThreshold` (default `±0.02`) of the baseline via `XmlToleranceMatcher`. Writes the outcome to `XML_Response` through `ResultUpdater`. If there's no baseline yet, it passes and becomes one. Unlike the JSON check below, this one still requires the real database - it hasn't been given a file-based stand-in.

A response XML carries one `techPrice` per add-on node plus one for the main cover node (e.g. Keycare, two breakdown add-ons, then Comprehensive) - `XmlValueExtractor.ExtractPremium` deliberately takes the **last** matching node in the document, not the first, since the main cover node is always last and that's the one the JSON side's `underwrittenDataPoints` entry lines up with.

**`Integration/Json/JsonComparisonTests`** - takes the `ScenarioId` NUnit parameter, requires the latest stored response to have `pass_fail`/`Status = PASS`, re-extracts its stored amount, calls the quote API through `JsonApiClient`, extracts `underwrittenDataPoints[0].technicalPrice.grossPremiumAmountIncTax` with `JsonValueExtractor` ("technicalPrice" is this API's analog of the XML's "techPrice"), and asserts it's *exactly* equal via `JsonExactMatcher`. Nothing is written back - read-only cross-check.

Both fixtures build their API client from `TestSetup.Client` - **one shared `RestClient`** for the whole test run (`TestBase/TestSetup.cs`, a `[SetUpFixture]` with no namespace so its `OneTimeTearDown` covers the whole assembly). Everything on `TestSetup` is lazily created on first use rather than in `OneTimeSetUp`, because NUnit evaluates `TestCaseSource` before any `OneTimeSetUp` runs - `Lazy<T>` sidesteps that ordering rather than fighting it.

### TEMPORARY: reading `data/xml-response.json` instead of the database

The real database isn't set up yet. `JsonComparisonTests` reads its "stored response" baseline from `data/xml-response.json` (an array of `{scenario_id, quote_ref, xml_response, version, pass_fail, updated_on}`, copied into the test output via a `<None Include>` in the `.csproj`) through `TestBase/JsonFileResponseSource.cs`, instead of `TestSetup.ResponseReader.GetLastResponseAsync` (the real DB path `Core` will use). This is scoped to the `Tests` project only, deliberately: `Core.Configuration.AppConfiguration` was **not** given a `StoredResponses` setting, since that's a test-only stopgap, not something the reusable library should know about - the path is instead read directly off `appsettings.json`'s `StoredResponses:FilePath` inside `TestSetup`.

`XmlToleranceTests` was **not** changed to use this file - it still calls `TestSetup.ResponseReader`/`RequestDataReader`/`ResultUpdater` (the real DB path) and will fail without a connection string, same as before. Only the JSON comparison side got a stand-in, since that's what was asked for.

To go back to the real database once it's ready: delete `TestBase/JsonFileResponseSource.cs`, `data/xml-response.json`, the `StoredResponses` section of `appsettings.json`, the `<None Include>` line in the `.csproj`, and `TestSetup.StoredResponsesPath`; then swap `JsonComparisonTests`' `JsonFileResponseSource.GetLast(...)` call back to `await TestSetup.ResponseReader.GetLastResponseAsync(scenarioId)`.

## Running

```bash
dotnet test src/ClientAutomationFramework.Tests --filter Category=Integration -- TestRunParameters.Parameter\(name=\"ScenarioId\",\ value=\"SCN-001\"\)
```

Omit `ScenarioId` for `XmlToleranceTests` to run every row in `XML_Requests`; `JsonComparisonTests` requires it.

## Configuration

`src/ClientAutomationFramework.Tests/appsettings.json` holds the shape. **`XmlScheme.BaseUrl`/`.ResourceTemplate` are placeholders** - no endpoint was given for the pricing API, so fill them in via `appsettings.Local.json` (gitignored; copy `appsettings.Local.example.json`) along with `Database.ConnectionString` before running for real. `JsonScheme.BaseUrl` is pre-filled with the quote API's UAT host.

**`Validation/JsonResponseValidator` and the placeholder `TestAssets/Xsd/TechPriceResponse.xsd`** are still best-effort/unwired: the real response XSD was never available (the UAT host resets connections probed from this environment), and `JsonResponseValidator` isn't called from either fixture. The JSON *shape* itself is no longer a guess, though - `JsonValueExtractor.ExtractPricingComponentAmount` now navigates the real `underwrittenDataPoints[].technicalPrice.grossPremiumAmountIncTax.amount` path instead of searching for a made-up `pricingComponent` field.

## Database

`sql/001_create_tables.sql` creates both tables. `XML_Requests` has a composite `(Scenario_id, Quote_ref)` primary key; `XML_Response` is append-only (surrogate `Id`, `Created_date`) since a re-run can produce a new `Quote_ref` for the same `Scenario_id` - "the last response" is looked up by `Scenario_id` alone.

## Notes

- This has not been run against a real database or a real endpoint; do that before trusting the Pass/Fail output.
- `Extraction/XmlValueExtractor` and `Extraction/JsonValueExtractor` expose general-purpose methods (`ExtractDecimalAttribute`, `ExtractTechnicalPriceAmount`) plus convenience wrappers (`ExtractPremium`, `ExtractPricingComponentAmount`) for the two concrete fields used here - reuse the general methods for other fields without duplicating the XML/JSON walking logic. `ExtractDecimalAttribute` takes an optional `useLastMatch` (default `false`, i.e. first match) - `ExtractPremium` is the only caller that passes `true`.
