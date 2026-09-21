# Naming Refactor Decisions

## External pricing folder

The former `Api` folder contained only the client, request construction, retry, rate limiting, date/resource construction, and exception types used to call configured external pricing endpoints with XML. `ExternalAPIAccess` is therefore more accurate than `Api`: it describes why the code exists rather than the HTTP transport.

The folder contains external pricing request/response boundaries and their supporting resilience and URI-building code. It does not contain database persistence, comparison workflow orchestration, or generic HTTP infrastructure for unrelated services.

## Database folder

The former `Data` folder contains SQL Server connection, repository, mutation, retry, identifier validation, and persistence model code. `Database` is more accurate than `Data` because these types store and retrieve baseline and comparison records; they are not general-purpose data-processing utilities.

## Naming scope

Only names with a clear responsibility-based replacement are changed. Generic words are not removed mechanically. External contracts remain unchanged, including CSV headings, XML element names, environment-variable keys, database table and column names, NUnit categories, and disabled placeholder configuration.

No interface is removed. Interfaces remain where they protect an external side effect, support test substitution, represent retry/rate-limit boundaries, or have multiple implementations.

The comparison amount remains a neutral term pending confirmation of the business vocabulary. No `Premium` terminology is introduced.
