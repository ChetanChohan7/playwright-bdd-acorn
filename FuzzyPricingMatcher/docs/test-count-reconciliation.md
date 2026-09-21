# Test Count Reconciliation

Date: 2026-09-21

## Current verified state

- Unit discovery: 86 test cases.
- Unit execution: 86 passed, 0 failed, 0 skipped.
- ComponentIntegration discovery: 0 test cases.
- ComponentIntegration execution: 0 passed, 0 failed, 0 skipped, with the NUnit no-match warning.
- No source file contains `ComponentIntegration` or `[Category("ComponentIntegration")]`.

## Historical test information

Earlier documentation reported 93 Unit tests. The current source reliably discovers 86 Unit tests, and all 86 current tests pass with 0 failed and 0 skipped. The identities of the historical missing tests cannot be confirmed. Tests must not be recreated from counts alone; future tests should be based on current documented requirements. This does not currently block first-scheme configuration.

The current repository does not contain the historical source-control metadata or the historical review files `solution-review.md` and `post-remediation-review.md`, so a complete one-to-one 93-name comparison is not available.

The prior session does provide exact evidence for seven guarded `SystemIntegration` tests in `ComparisonSystemIntegrationTests.cs`, supported by synthetic XML/XSD assets and local SQL/HTTP infrastructure. Those tests were not ComponentIntegration tests and were not included in the Unit filter. Their source files are absent from the current workspace. They must not be recreated from a test count alone; restoration requires the complete historical source and assets to avoid inventing coverage or enabling live resources.

| Missing count item | Previous evidence | Current equivalent | Category | Assertions preserved | Restore decision |
|---|---|---|---|---|---|
| 1-7 | Seven `SystemIntegration` tests in the prior session's `ComparisonSystemIntegrationTests.cs` | None in the current tree | SystemIntegration | No | Do not restore from partial log fragments; preserve the evidence and require the complete historical source/assets before restoration |
| 8-11 | Earlier summary reports 93 Unit tests but does not provide the 11 missing test names | Four current structure tests were added, but no exact historical equivalents can be proven | Previously reported Unit | Unknown | Do not invent or duplicate tests; investigate through source control or a supplied historical snapshot |

The current Unit total of 86 is therefore the verified executable count, not evidence that historical behaviours were intentionally removed. No test was removed during this task.

## ComponentIntegration finding

ComponentIntegration tests were never found in the current source, session file inventory, or available test output. The earlier session explicitly reported `ComponentIntegration: 0 tests found`. There is no evidence that a ComponentIntegration fixture was renamed, lost an attribute, or moved to another category. No replacement is invented.

## Historical follow-up evidence

If source-control history later becomes available, review the prior source snapshot and test names separately. Do not restore the seven SystemIntegration tests or reconcile the remaining historical Unit difference from partial logs alone. The current workspace has no `.git` metadata and the available session records contain only partial generated-file content. This is historical information, not an active blocker.
