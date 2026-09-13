# Roadmap

## Milestone 1 — complete

Read-only WMI inventory, core/Windows/CLI boundaries, JSON/Markdown, privacy projection and preview/export, MIT foundation, schema, tests, and decision records. The verified baseline is preserved before M2; see `docs/VALIDATION.md`.

## Milestone 2 — active topology

- [x] DisplayConfig active paths with distinct source/target and adapter relationships.
- [x] Exact SetupAPI instance-ID bridge to WMI inventory; unmatched/ambiguous states.
- [x] Source dimensions, separate rational path/signal rates, rotation, connector and availability.
- [x] Bounded query races, validated native unions/layouts/indexes, per-field failures.
- [x] Neutral labels and privacy checks excluding raw correlation IDs from both exports.
- [x] Retained M1 tests plus deterministic topology cases and non-admin local validation.
- [x] Schema 0.2.0, updated documentation and durable interop/correlation decision.

M2 does not determine application rendering GPU, utilization/power, MUX position, hybrid mode, or Optimus. Local hardware validation covers this machine in its unchanged current configuration only. Synthetic clone/multi-monitor tests are not physical compatibility evidence.

## Milestone 3 — single-laptop validation and hardening (complete locally)

- [x] Six fresh non-administrator CLI collections on the unchanged internal-display configuration, split into two batches with a pause between them.
- [x] Stable one-path, exact-correlation, 2560 × 1600, identity/progressive, rational-rate, privacy, schema, and exit-status checks; reports and anomalies retained under ignored `artifacts/`.
- [x] Empty optional monitor friendly name kept as explicit `unknown/missingValue` with a diagnostic, without independently making collection partial or changing CLI exit to `3`.
- [x] Substantive source/target API, mode, adapter, provider, and retry failures remain incomplete (exit `3`); export refusal/failure remain exits `4`/`5`, and privacy redaction retains its existing warning/count semantics.
- [x] Tightened deterministic coverage for affected severity combinations, retry growth/shrink, count guards, repeated collector use, path isolation, and multi-path privacy references.
- [x] Updated the validation record, schema guidance, README, and ADR 0006; schema remains 0.2.0.

M3 remains a single-laptop validation result. It does not claim external-display, hot-plug, AMD, ARM64, RDP, or broad compatibility validation. Final read-only review and fresh checkpoint validation passed; M3 is locally checkpointed with no publication or release. M4 has not started.

Do not change graphics modes, MUX/BIOS state, drivers, or power settings. The existing external-display/remote-session manual checklist remains optional future coverage when hardware/session access becomes available, not a completion gate for this single-laptop milestone.

Do not add new data sources, DXGI, classification, vendor APIs, monitoring, GUI, or remediation in this validation milestone unless a specific failure requires a separately reviewed scope decision.

## Later, separately authorized

Potential work includes SetupAPI/WMI inventory comparison, hard collection cancellation, privacy-reviewed ZIP packaging, and a separate GUI decision. Vendor interfaces and classification need their own source, privacy, license, and hardware investigation. No capability or timeline is promised.

BIOS/MUX/driver/service/registry/power-plan changes, telemetry, AI features, automatic fixes, and generic optimization remain outside current scope.
