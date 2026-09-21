# User Guide

The Baseline Loader reads `TestAsset/baseline-scenarios.csv`. The file headings are exactly `Scenario_id,XML_request,Test_tags`. It calls the external pricing service only for new or changed request XML, validates the response, and updates the stored baseline.

The Comparison Test Runner reads stored scenarios, calls the external pricing service, validates current and stored responses, compares their amounts using inclusive thresholds, and persists the outcome before asserting the test.

The committed CSV is a headings-only safe template. A real run requires approved configuration and guarded integration execution.
