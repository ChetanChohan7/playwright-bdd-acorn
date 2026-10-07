# Radar Pricing XSD Assets

This folder is the permanent home for Radar pricing XML schemas. Each route selects one XSD for both the stored baseline Radar response and the newly returned Radar response. Approximately 16 route/schema combinations are expected.

Schema selection is configured in `RadarSettings:ResponseXsdMappings`, keyed by route ID. Add the approved XSD here and map that route to its filename. All schemes on the route share this schema. Split routes when contracts differ, even if their endpoint and RouteKey are the same.

Schemas are compiled once during setup and cached in memory. Decimal elements and attributes are discovered from schema annotations while validating each scenario's XML. Full paths become comparison and report keys; no field mappings are configured. Repeated elements are paired by zero-based index and require stable ordering between baseline and response.

`PricingComparisonFixture.xsd` is a test-only fixture for XSD-driven extraction and the 20,000-scenario regression workload. It is not a production schema or a substitute for client-provided contracts. Supply each approved production XSD as it arrives; missing or invalid mapped schemas fail setup rather than bypassing validation.

This directory and its placeholder files are intentional integration points. Cleanup activities must not remove this folder.
