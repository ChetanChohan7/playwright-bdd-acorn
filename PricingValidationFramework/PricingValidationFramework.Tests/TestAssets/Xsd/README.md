# Radar Response XSD Assets

This folder is the permanent home for Radar response XML schemas. Approximately 16 response schemas are expected from the client.

Schema selection is configuration-driven by the existing Radar route identifier in `RadarSettings:ResponseXsdMappings`. To register a production schema, add the XSD file here and map its filename to the applicable route identifier in the active appsettings configuration. Schema names do not need to be derived from ProductCode or SchemeCode, and no code registration is required.

`placeholder-response-schema.xsd` documents the currently evidenced `Response/TotalAmount` shape only. It is not a production schema, is not selected by the current route mappings, and is not a substitute for client-provided contracts. Register each approved response schema in configuration when received.

This directory and its placeholder files are intentional integration points. Cleanup activities must not remove this folder.
