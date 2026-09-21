# Operations Runbook

The normal local verification is restore, Release build, and the Unit test filter. The Unit suite uses fakes and makes no live SQL or external HTTP calls.

Integration execution is guarded by `FUZZY_RUN_INTEGRATION=true` and requires approved private configuration. Do not enable it for routine refactor verification. Placeholder routes, endpoints, credentials, schemas, and connection strings must remain disabled or rejected.

Loader deletion is safe only after every data-bearing CSV scenario succeeds. Failed comparisons preserve the stored baseline.

See [project-todo.md](project-todo.md) for the authoritative integration, Azure DevOps, and operational-readiness checklist.
