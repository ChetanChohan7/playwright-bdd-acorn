# Radar JSON Assets

This folder is the permanent integration point for client-provided Radar JSON contracts and representative payloads.

`RadarJsonResponse` represents the current envelope containing a top-level `response` XML string. `RadarApiClient` deserializes this envelope and normalizes the XML before XSD validation and pricing comparison. Raw XML responses remain supported. Place approved contracts and sample payloads here; changes to the envelope must preserve the XML extraction behavior.

`placeholder-response.json` is an empty placeholder only. It does not define a Radar JSON contract and must not be treated as a production payload.

This directory is intentional. Cleanup activities must not remove this folder.
