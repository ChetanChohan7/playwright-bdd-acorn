# Future Radar JSON Assets

This folder is the permanent integration point for future client-provided Radar JSON contracts and representative payloads.

When a JSON contract is received, place the approved contract and sample payloads here, populate the `RadarJsonResponse` typed model from the actual contract, and deserialize the payload into that model. Validation and reporting should then read the typed model rather than repeat XML/JSON lookups.

`placeholder-response.json` is an empty placeholder only. It does not define a Radar JSON contract and must not be treated as a production payload.

This directory is intentional. Cleanup activities must not remove this folder.
