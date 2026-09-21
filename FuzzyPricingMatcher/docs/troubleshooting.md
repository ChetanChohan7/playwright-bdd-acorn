# Troubleshooting

If configuration validation fails, check that placeholder endpoints, routes, credentials, response schemas, and connection strings have not been used. The committed configuration is intentionally inert.

If the loader reports a malformed CSV or request, preserve the exact headings and inspect the raw XML row. Do not edit XML element names to satisfy the test harness.

If a comparison fails, inspect the evidence path reported by the test. A failed comparison preserves the stored baseline; do not update the database manually without an approved investigation.
