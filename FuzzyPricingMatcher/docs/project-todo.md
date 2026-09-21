# Project TODO

This is the authoritative TODO list for FuzzyPricingMatcher. It describes work that remains without enabling integration or changing implementation behavior.

No item on this page blocks local development; that column is omitted below because it is "No" for every row.

## Current verified state

- [x] Release build succeeds.
- [x] Unit baseline is 86 discovered, 86 passed, 0 failed, and 0 skipped.
- [x] No live SQL or external HTTP calls are made by local Unit verification.
- [x] No real SchemeCode is configured.
- [x] All placeholder routes and endpoints remain disabled.
- [x] No credentials or live connection strings are documented.
- [x] Coverage tooling remains available through `coverlet.collector`; no threshold is enforced.

## Before configuring the first real scheme

Complete these in order for one approved scheme.

| Item | Blocks | Action |
|---|---|---|
| Obtain the approved SchemeCode | First scheme, Live integration | Record the approved non-secret identifier. |
| Confirm whether the scheme uses EndpointA, EndpointB, or EndpointC | First scheme, Live integration | Record the endpoint name without credentials. |
| Obtain the approved API BaseUrl and resource | First scheme, Live integration, Azure DevOps | Supply them through protected private configuration. |
| Confirm API date placement and format | First scheme, Live integration | Confirm query or path placement, parameter name, and accepted format. |
| Obtain the approved response XSD and any local dependencies | First scheme, Live integration | Add trusted local schema files and reject external imports. |
| Confirm the response namespace | First scheme, Live integration | Document the namespace in the route test fixtures. |
| Confirm the comparison amount property path | First scheme, Live integration | Obtain the exact XML element path and decimal rules. |
| Implement the real comparison amount reader | First scheme, Live integration | Replace only the placeholder reader for the approved scheme. |
| Add XSD and amount-reader tests | First scheme, Live integration | Cover valid, malformed, wrong-namespace, missing-value, and invalid-value responses. |
| Confirm SQL table and column compatibility | First scheme, Live integration, Azure DevOps | Verify the existing schema against the approved integration contract; do not add DDL here. |
| Confirm SQL permissions | First scheme, Live integration, Azure DevOps | Obtain least-privilege access for the required reads and mutations. |
| Add one approved non-sensitive CSV scenario | First scheme, Live integration | Add one data-bearing scenario only after XML and privacy review. |

## Before guarded live integration

| Item | Action |
|---|---|
| Confirm POST retry safety with the API owner | Obtain explicit approval because no idempotency header is assumed. |
| Complete an independent readiness review | Review configuration, schema validation, amount extraction, SQL effects, evidence, retries, rate limits, and rollback handling. |
| Obtain approval for the guarded integration run | Approve a bounded run with protected settings and an evidence destination. |

## Before Azure DevOps use

| Item | Action |
|---|---|
| Configure Azure DevOps service connections, variable groups, secret storage, permissions, and environments for `pipelines/baseline-loader.yml` and `pipelines/comparison-tests.yml` | Keep credentials and connection strings in secret variables; never commit them. |
| Review pipeline approvals, triggers, test filters, artifact retention, and failure policy | Perform a non-live validation first, then separately approve guarded integration use. |

## Controlled system testing (optional)

Create a controlled comparison SystemIntegration suite using a synthetic SchemeCode, a synthetic response XSD, synthetic request and response XML, a disposable SQL test database, a localhost API stub, the real Dapper repository, the real external XML client, the real route resolver, the real XSD validator, the real comparison amount reader, the real comparison workflow, and real evidence writing. Implement and run it locally before live integration; it is not a live external-system test and does not block first-scheme configuration. Do not recreate it from historical test counts.

## Operational ownership

| Item | Action |
|---|---|
| Confirm owners and escalation contacts for the external API, SQL database, credentials, schema changes, evidence retention, and production approvals | Record role or team ownership, never secrets. |
| Define the approved run window, evidence retention, rollback procedure, and database change process | Add approved operational details to the runbook. |

Both items block live integration and Azure DevOps use.

## Optional improvements

- [ ] Consider enforcing a coverage percentage threshold once required behavior and ownership are stable; coverage is already collectable through `coverlet.collector`.
- [ ] Perform further simplification only when a specific readability or maintenance problem is identified; do not schedule another broad refactor without one.

## Historical information

Earlier documentation reported 93 Unit tests; the current source reliably discovers 86, and all 86 pass with 0 failed and 0 skipped. That is the current verified baseline. The identities of the historical missing tests could not be confirmed and were not recreated from counts alone — future tests should be based on current documented requirements. This does not block first-scheme configuration.

## Completed work

- [x] Placeholder routes and endpoint settings are disabled and rejected before integration access.
- [x] Unit verification uses fakes and does not contact SQL Server or external HTTP services.
- [x] The authoritative CSV remains a safe headings-only template.
- [x] No production TODO, FIXME, HACK, TEMP, `NotImplementedException`, or incomplete method body was found.
- [x] Intentional test doubles were reviewed: empty evidence callbacks in `Tests/Unit/MockWorkflowOrchestrationTests.cs` and `NotSupportedException` mutation methods are not production TODOs.
- [x] The empty `Credentials` setter in `Validation/SchemaRegistry.cs` is an intentional resolver security boundary, not an incomplete production method.
- [x] A missing `Evidence` namespace (`SafeFileName`, `EvidencePathBuilder`, `ScenarioEvidenceWriter`, `LoaderEvidenceWriter`) referenced by the composition root and tests but never committed was implemented, fixing a build that previously failed to compile.
- [x] A namespace inconsistency (`BaselineScenarioCsvRow` and `PreparedBaselineScenario` declared in `FuzzyPricingMatcher.Tests.Models` instead of `...Loader` like every other file in `Loader/Models/`) was corrected.
- [x] Dead code (`RequestChangeDetector`/`IRequestChangeDetector`, superseded by inline logic in `LoaderSynchronizationService`) was removed.
- [x] Ceremonial interfaces with exactly one production implementation and no test fake substituting them (normalizers, readers, the schema registry/validator, the retry/URL-builder/client-factory trio, the threshold evaluator, and the loader/comparison/CSV entry points) were removed in favor of the concrete class. Interfaces that back a real test seam (repository, route resolver, API client, rate limiter, clock, evidence writer, logger, response amount reader, SQL mutation session) were kept.
- [x] Trivial one-record-per-file `Models/` folders under `Database/`, `Loader/`, `Comparison/`, and `ExternalAPIAccess/` were consolidated into one `Models.cs` per folder.

## TODO inventory and classification

The complete workspace scan found these marker groups. Generated `bin/` copies mirror source configuration and are not separate work items.

| Location | Lines | Exact text or pattern | Category | Blocks | Recommended action |
|---|---:|---|---|---|---|
| `src/FuzzyPricingMatcher.Tests/appsettings.json` | 14-30 | `"TODO_SCHEME_01"` through `"TODO_SCHEME_17"`, each with `"Enabled": false` and `"PlaceholderResponseProcessor"` | First real scheme | First scheme, Live integration | Replace only the approved scheme's placeholder mapping after contract approval; leave the other placeholders disabled. |
| `src/FuzzyPricingMatcher.Tests/appsettings.Local.example.json` | 11-27 | `"TODO_SCHEME_01"` through `"TODO_SCHEME_17"`, each with `"Enabled": false` | First real scheme | First scheme, Live integration | Update the example only when the approved private configuration shape is known; never add secrets. |
| `src/FuzzyPricingMatcher.Tests/Tests/Unit/MockWorkflowOrchestrationTests.cs` | 134-136 | `public void Save(...) { }`, `public void Outcome(...) { }`, `public void QuoteMismatch(...) { }`, `public void Save(...) { }` | Obsolete TODO | None (optional) | Keep as no-op test doubles; do not add behavior. |
| `src/FuzzyPricingMatcher.Tests/Tests/Unit/MockWorkflowOrchestrationTests.cs` | 141-143 | `throw new NotSupportedException()` in `Update`, `UpdateTags`, and `DeleteObsolete` | Obsolete TODO | None (optional) | Keep as deliberate guards proving those mutation paths are not called by the tested workflow. |
| `src/FuzzyPricingMatcher.Tests/Validation/SchemaRegistry.cs` | ~65 | `public override System.Net.ICredentials? Credentials { set { } }` | Obsolete TODO | None (optional) | Keep the empty setter; it prevents credential use during trusted local schema resolution. |
| `.gitignore` | 19 | `# Temporary files` and `*.temp` | Obsolete TODO | None (optional) | Keep the ignore rule; it is repository hygiene, not project work. |
| `pipelines/baseline-loader.yml` | 1 | `# Future template only. Azure DevOps connection is intentionally not configured here.` | Azure DevOps setup | Azure DevOps | Configure only after service connections, secret variables, approvals, and non-live validation are ready. |
| `pipelines/comparison-tests.yml` | 1 | `# Future template only. Azure DevOps connection is intentionally not configured here.` | Azure DevOps setup | Azure DevOps | Configure only after service connections, secret variables, approvals, and non-live validation are ready. |

No `FIXME`, `HACK`, `NotImplementedException`, or empty production method body requiring implementation was found. `TEMP` appears only in the `.gitignore` hygiene rule. Coverage threshold enforcement remains optional because coverage collection is still configured.

## Testing and traceability

The current local verification baseline is 86 discovered, 86 passed, 0 failed, and 0 skipped. Controlled SystemIntegration testing remains outstanding and is recommended before live integration, but it is not a live external-system test and is not a first-scheme blocker.
