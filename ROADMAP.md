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

## Milestone 3 scope — single-laptop validation and hardening (not started)

Do not begin automatically; see `STATUS.md` for the current checkpoint. The owner has only the laptop/internal display and no external monitor, TV, or second display. Do not wait for unavailable hardware or require physical Extend/Duplicate/clone/hot-plug tests to complete this milestone.

When authorized to execute M3:

- Repeatedly collect on the current unchanged internal-display configuration. Check stable active-path count, adapter correlation, resolution/refresh, absence of stale state, privacy, and non-administrator operation.
- Review the severity of missing optional monitor friendly names. Current M2 behavior is partial/exit `3`; do not describe the review as a completed correction. Preserve explicit unknown values and distinguish optional metadata absence from substantive collection failures.
- Harden the existing model/collector where evidence supports a change. Inspect current fixtures before adding missing deterministic synthetic cases for multiple displays, clone/extend, unmatched/ambiguous adapters, topology changes/retries, and partial failures.
- Record physical observations separately from synthetic compatibility tests in `docs/VALIDATION.md`. Do not claim real hardware validation from mocked responses.

Do not change graphics modes, MUX/BIOS state, drivers, or power settings. The existing external-display/remote-session manual checklist remains optional future coverage when hardware/session access becomes available, not a completion gate for this single-laptop milestone.

Do not add new data sources, DXGI, classification, vendor APIs, monitoring, GUI, or remediation in this validation milestone unless a specific failure requires a separately reviewed scope decision.

## Later, separately authorized

Potential work includes SetupAPI/WMI inventory comparison, hard collection cancellation, privacy-reviewed ZIP packaging, and a separate GUI decision. Vendor interfaces and classification need their own source, privacy, license, and hardware investigation. No capability or timeline is promised.

BIOS/MUX/driver/service/registry/power-plan changes, telemetry, AI features, automatic fixes, and generic optimization remain outside current scope.
