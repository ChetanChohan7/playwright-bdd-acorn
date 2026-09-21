# Project TODO

This is the authoritative TODO list for FuzzyPricingMatcher. It describes work that remains without enabling integration or changing implementation behavior.

## Current verified state

- [x] Release build succeeds.
- [x] Unit baseline is 86 discovered, 86 passed, 0 failed, and 0 skipped.
- [x] No live SQL or external HTTP calls are made by local Unit verification.
- [x] No real SchemeCode is configured.
- [x] All placeholder routes and endpoints remain disabled.
- [x] No credentials or live connection strings are documented.
- [x] Coverage tooling remains available through `coverlet.collector`; no threshold is enforced.

## Before configuring the first real scheme

Complete these in order for one approved scheme. These items block first-scheme configuration unless explicitly marked otherwise.

- [ ] Obtain the approved SchemeCode. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: record the approved non-secret identifier.
- [ ] Confirm whether the scheme uses EndpointA, EndpointB, or EndpointC. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: record the endpoint name without credentials.
- [ ] Obtain the approved API BaseUrl and resource. Category: Database and API access. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: yes. Optional: no. Action: supply them through protected private configuration.
- [ ] Confirm API date placement and format. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: confirm query or path placement, parameter name, and accepted format.
- [ ] Obtain the approved response XSD and any local dependencies. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: add trusted local schema files and reject external imports.
- [ ] Confirm the response namespace. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: document the namespace in the route test fixtures.
- [ ] Confirm the comparison amount property path. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: obtain the exact XML element path and decimal rules.
- [ ] Implement the real comparison amount reader. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: replace only the placeholder reader for the approved scheme.
- [ ] Add XSD and amount-reader tests. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: cover valid, malformed, wrong-namespace, missing-value, and invalid-value responses.
- [ ] Confirm SQL table and column compatibility. Category: Database and API access. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: yes. Optional: no. Action: verify the existing schema against the approved integration contract; do not add DDL here.
- [ ] Confirm SQL permissions. Category: Database and API access. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: yes. Optional: no. Action: obtain least-privilege access for the required reads and mutations.
- [ ] Add one approved non-sensitive CSV scenario. Category: First real scheme. Blocks local development: no. Blocks first-scheme configuration: yes. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: add one data-bearing scenario only after XML and privacy review.

## Before guarded live integration

- [ ] Confirm POST retry safety with the API owner. Category: Live integration approval. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: obtain explicit approval because no idempotency header is assumed.
- [ ] Complete an independent readiness review. Category: Live integration approval. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: review configuration, schema validation, amount extraction, SQL effects, evidence, retries, rate limits, and rollback handling.
- [ ] Obtain approval for the guarded integration run. Category: Live integration approval. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: yes. Blocks Azure DevOps: no. Optional: no. Action: approve a bounded run with protected settings and an evidence destination.

## Before Azure DevOps use

- [ ] Configure Azure DevOps service connections, variable groups, secret storage, permissions, and environments for `pipelines/baseline-loader.yml` and `pipelines/comparison-tests.yml`. Category: Azure DevOps setup. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: yes. Optional: no. Action: keep credentials and connection strings in secret variables; never commit them.
- [ ] Review pipeline approvals, triggers, test filters, artifact retention, and failure policy. Category: Azure DevOps setup. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: yes. Optional: no. Action: perform a non-live validation first, then separately approve guarded integration use.

## Controlled system testing

- [ ] Create a controlled comparison SystemIntegration suite using a synthetic SchemeCode, a synthetic response XSD, synthetic request and response XML, a disposable SQL test database, a localhost API stub, the real Dapper repository, the real external XML client, the real route resolver, the real XSD validator, the real comparison amount reader, the real comparison workflow, and real evidence writing. Category: Optional quality improvement. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: no. Action: implement and run locally before live integration; this is not a live external-system test. Do not recreate it from historical test counts.

## Operational ownership

- [ ] Confirm owners and escalation contacts for the external API, SQL database, credentials, schema changes, evidence retention, and production approvals. Category: Operational ownership. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: yes. Blocks Azure DevOps: yes. Optional: no. Action: record role or team ownership, never secrets.
- [ ] Define the approved run window, evidence retention, rollback procedure, and database change process. Category: Operational ownership. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: yes. Blocks Azure DevOps: yes. Optional: no. Action: add approved operational details to the runbook.

## Optional improvements

- [ ] Consider enforcing a coverage percentage threshold after the required behavior and ownership are stable. Category: Optional quality improvement. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: yes. Action: choose a threshold and CI policy; coverage is already collectable through `coverlet.collector`.
- [ ] Perform further simplification only when a specific readability or maintenance problem is identified. Category: Optional quality improvement. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: yes. Action: make a narrowly justified change; do not schedule another broad refactor without a specific problem.

## Historical information

- [ ] Earlier documentation reported 93 Unit tests. Category: Historical information. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: yes. Action: retain as historical context only.
- [ ] The current source reliably discovers 86 Unit tests, and all 86 current tests pass with 0 failed and 0 skipped. Category: Historical information. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: no. Action: use this as the current verified baseline.
- [ ] The identities of the historical missing tests cannot be confirmed. Tests must not be recreated from counts alone. Future tests should be based on current documented requirements. This does not currently block first-scheme configuration. Category: Historical information. Blocks local development: no. Blocks first-scheme configuration: no. Blocks live integration: no. Blocks Azure DevOps: no. Optional: yes. Action: review historical tests separately only if source-control history becomes available.

## Completed work

- [x] Placeholder routes and endpoint settings are disabled and rejected before integration access.
- [x] Unit verification uses fakes and does not contact SQL Server or external HTTP services.
- [x] The authoritative CSV remains a safe headings-only template.
- [x] No production TODO, FIXME, HACK, TEMP, `NotImplementedException`, or incomplete method body was found.
- [x] Intentional test doubles were reviewed: empty evidence callbacks in `Tests/Unit/MockWorkflowOrchestrationTests.cs` and `NotSupportedException` mutation methods are not production TODOs.
- [x] The empty `Credentials` setter in `Validation/SchemaRegistry.cs` is an intentional resolver security boundary, not an incomplete production method.
- [x] The old simplification dependency on historical test-count reconciliation was removed from current guidance.

## TODO inventory and classification

The complete workspace scan found these marker groups. Generated `bin/` copies mirror source configuration and are not separate work items.

| Location | Lines | Exact text or pattern | Category | Blocks local development | Blocks first scheme | Blocks live integration | Blocks Azure DevOps | Optional | Recommended action |
|---|---:|---|---|---|---|---|---|---|---|
| `src/FuzzyPricingMatcher.Tests/appsettings.json` | 14-30 | `"TODO_SCHEME_01"` through `"TODO_SCHEME_17"`, each with `"Enabled": false` and `"PlaceholderResponseProcessor"` | First real scheme | No | Yes | Yes | No | No | Replace only the approved scheme's placeholder mapping after contract approval; leave the other placeholders disabled. |
| `src/FuzzyPricingMatcher.Tests/appsettings.Local.example.json` | 11-27 | `"TODO_SCHEME_01"` through `"TODO_SCHEME_17"`, each with `"Enabled": false` | First real scheme | No | Yes | Yes | No | No | Update the example only when the approved private configuration shape is known; never add secrets. |
| `src/FuzzyPricingMatcher.Tests/Tests/Unit/MockWorkflowOrchestrationTests.cs` | 134-136 | `public void Save(...) { }`, `public void Outcome(...) { }`, `public void QuoteMismatch(...) { }`, `public void Save(...) { }` | Obsolete TODO | No | No | No | No | Yes | Keep as no-op test doubles; do not add behavior. |
| `src/FuzzyPricingMatcher.Tests/Tests/Unit/MockWorkflowOrchestrationTests.cs` | 141-143 | `throw new NotSupportedException()` in `Update`, `UpdateTags`, and `DeleteObsolete` | Obsolete TODO | No | No | No | No | Yes | Keep as deliberate guards proving those mutation paths are not called by the tested workflow. |
| `src/FuzzyPricingMatcher.Tests/Validation/SchemaRegistry.cs` | 65 | `public override System.Net.ICredentials? Credentials { set { } }` | Obsolete TODO | No | No | No | No | Yes | Keep the empty setter; it prevents credential use during trusted local schema resolution. |
| `.gitignore` | 19 | `# Temporary files` and `*.temp` | Obsolete TODO | No | No | No | No | Yes | Keep the ignore rule; it is repository hygiene, not project work. |
| `pipelines/baseline-loader.yml` | 1 | `# Future template only. Azure DevOps connection is intentionally not configured here.` | Azure DevOps setup | No | No | No | Yes | No | Configure only after service connections, secret variables, approvals, and non-live validation are ready. |
| `pipelines/comparison-tests.yml` | 1 | `# Future template only. Azure DevOps connection is intentionally not configured here.` | Azure DevOps setup | No | No | No | Yes | No | Configure only after service connections, secret variables, approvals, and non-live validation are ready. |
| `docs/test-count-reconciliation.md` | historical sections | Historical 93-versus-86 reconciliation | Historical information | No | No | No | No | Yes | Retain as historical information; do not treat it as a blocker or recreate tests from counts. |

No `FIXME`, `HACK`, `NotImplementedException`, or empty production method body requiring implementation was found. `TEMP` appears only in the `.gitignore` hygiene rule. Coverage threshold enforcement remains optional because coverage collection is still configured.

## Testing and traceability

The current local verification baseline is 86 discovered, 86 passed, 0 failed, and 0 skipped. Controlled SystemIntegration testing remains outstanding and is recommended before live integration, but it is not a live external-system test and is not a first-scheme blocker.
