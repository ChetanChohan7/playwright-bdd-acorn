# Naming And Style Guide

Name folders after the business responsibility they contain. Use `ExternalAPIAccess` for external pricing communication and `Database` for storage concerns.

Prefer names that identify the source and stage of data, such as `rawRequestXml`, `currentExternalResponseXml`, and `storedBaselineResponseXml`. Use action-oriented method names such as `SendExternalAPIAccessRequestAsync`, `BuildRequestUri`, and `ReadComparisonAmount`.

Do not rename physical CSV, database, XML, or environment-variable contracts. Avoid blanket renames of `Service`, `Result`, `Manager`, or `Processor`; replace a name only when the responsibility is clear.
