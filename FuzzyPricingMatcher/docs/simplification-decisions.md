# Simplification Decisions

This task is limited to approved folder and transport naming corrections. No class internals, interfaces, workflow steps, models, exceptions, or behavioural tests were simplified or removed.

`ExternalAPIAccess` is the approved location for RestSharp client creation, raw XML transport, URL/date construction, HTTP response handling, retries, and rate limiting. `Database` is the approved location for SQL Server connections, Dapper queries, mutation plans, transactions, retries, mappings, and database exceptions.

No generic helper, manager, repository base, unit-of-work wrapper, or utility abstraction was introduced. Perform further simplification only when a specific readability or maintenance problem is identified.
