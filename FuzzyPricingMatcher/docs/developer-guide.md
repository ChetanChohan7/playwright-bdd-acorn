# Developer Guide

The solution contains one .NET 10 NUnit project. `ExternalAPIAccess/` owns calls to configured external pricing endpoints. `Database/` owns SQL Server access and baseline mutations. `Loader/` synchronises the authoritative CSV, while `Comparison/` evaluates current external amounts against stored baseline amounts.

Keep external contracts unchanged: `Scenario_id`, `XML_request`, `Test_tags`, database column names, XML element names, and environment-variable keys are compatibility boundaries.

Run `dotnet build --configuration Release` and the Unit test filter before submitting changes. Unit tests use fakes and must not require SQL Server or live HTTP services.

See [project-todo.md](project-todo.md) for the authoritative TODO list. Historical test-count differences do not block first-scheme configuration.
