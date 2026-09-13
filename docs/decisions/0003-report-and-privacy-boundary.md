# ADR 0003: Explicit evidence states and privacy-first export

Status: accepted, 2026-09-10.

Decision: version the report envelope; separate facts, derived findings, warnings, collection outcomes, and privacy metadata. Every observation has state/value/source/reason. Unknown, unsupported, failed, and redacted values have null data and a reason. Available empty inventory is distinct from a failed query. Findings reference evidence paths.

Privacy projection is required for both writers. No raw provider objects, durable machine IDs, full PnP instance paths, or exception text in reports. Regenerate sequential GPU labels within each report. Allowlist structured values, filter suspicious free text, and warn that uncommon customized descriptions can still identify a system.

Preview is default. Explicit file export saves the same snapshot after user acceptance or deliberate `--yes`. Existing files are never overwritten; no upload is implemented. Future ZIP bundles contain these generated formats and a reviewed manifest, not arbitrary logs.

Consequences: filtering can remove useful text and is not perfect anonymization. Dates omit exact collection time; labels do not support cross-run identity. Adding new fields or vendor facts requires schema and privacy tests. M1 is pre-1.0 and does not implement import or migrate arbitrary external reports.
