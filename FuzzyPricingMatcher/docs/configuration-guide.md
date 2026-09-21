# Configuration Guide

Committed configuration is intentionally inert. All route and endpoint placeholders are disabled, and placeholder credentials and connection strings must remain unapproved.

The authoritative CSV is `TestAsset/baseline-scenarios.csv` with the exact headings `Scenario_id,XML_request,Test_tags`. Keep JSON keys and environment-variable names unchanged when supplying approved private settings.

Do not set `FUZZY_RUN_INTEGRATION=true` or `FUZZY_RUN_SYSTEM_TESTS=true` during local naming or structure verification.

See [project-todo.md](project-todo.md) for the authoritative first-scheme and integration checklist.
