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

M3 remains a single-laptop validation result. It does not claim external-display, hot-plug, AMD, ARM64, RDP, or broad compatibility validation. Final read-only review and fresh checkpoint validation passed; M3 is locally checkpointed with no publication or release. M4 is separately bounded below.

Do not change graphics modes, MUX/BIOS state, drivers, or power settings. The existing external-display/remote-session manual checklist remains optional future coverage when hardware/session access becomes available, not a completion gate for this single-laptop milestone.

Do not add new data sources, DXGI, classification, vendor APIs, monitoring, GUI, or remediation in this validation milestone unless a specific failure requires a separately reviewed scope decision.

## Milestone 4 — bounded collection supervisor (complete locally)

The final GPT checkpoint-readiness review passed. The bounded supervisor, production worker collection path, controlled cancellation, and calibrated internal timing are checkpointed locally in commit `57abbec81ac8db83e04a2a588d39fe18c1650070` (`Milestone 4: bounded collection and controlled cancellation`). No push, tag, or release occurred; physical validation remains limited to the documented one-laptop scope.

- [x] Added `WinGPUDoctor.Protocol`, `WinGPUDoctor.Supervisor`, and framework-dependent `wingpudoctor-worker` projects.
- [x] Added strict protocol v1 framing, fixed operations, concrete payloads, bounds, duplicate/order/state validation, and `ResourceLimit` mapping.
- [x] Added the monotonic supervisor budget/state machine with exactly one terminal outcome, deadline equality timeout, late-result/late-timer protection, cleanup allowance, reservations, and poisoned admission.
- [x] Added embedded worker deployment manifest generation, hash/length/identity validation, and `<CLI output>/worker/` deployment.
- [x] Added creation-time Job Object containment with `JOB_LIST`/`HANDLE_LIST`, kill-on-close, active-process limit one, no breakaway, and explicit non-elevation checks.
- [x] Added a private current-user first-instance named pipe with remote-client rejection, explicit pipe-client/NUL inheritance, absolute asynchronous I/O deadlines, and retained ownership with process-wide poisoning on unconfirmed cleanup.
- [x] Added deterministic fake-clock tests, protocol/parser tests, native layout tests, and synthetic child-process tests.
- [x] GPT Review Gate 1: native/transport correctness passed after corrections.
- [x] Gate 2: real Worker dispatch for the five fixed operations and production supervisor-backed collection composition.
- [x] Parent assembly of the existing `CollectionSnapshot`, conditional driver operation, exact correlation, M3 optional-name behavior, privacy projection, schema and CLI exit preservation.
- [x] Targeted integrated corrections implemented and tested locally; this is not a re-review pass.
- [x] Integrated semantics/privacy review passed after those corrections.
- [x] Controlled Ctrl+C: the first interrupt closes result acceptance, prevents later operations, cleans up within the existing bounded contract, skips preview/export and returns `3`; a second interrupt keeps default forced termination. Covered by deterministic tests and a scoped live console-signal check on this laptop.
- [x] Internal timing calibration on one available laptop and selection of `CollectionTimingPolicy.CalibratedProduction`; fake-clock tests remain the authoritative boundary proof and no public timeout option was added.
- [x] Three corrected fresh-process healthy collections with explicit per-run before/after fingerprints and tracked-worker liveness checks; same-host admission reuse remains a deterministic-test claim. Historical six-run evidence is qualified in `docs/VALIDATION.md`.
- [x] Final GPT checkpoint-readiness review passed and M4 checkpointed locally in commit `57abbec81ac8db83e04a2a588d39fe18c1650070`.

At the time of the M4 completion record, no later milestone had started. M5 is separately authorized for Gate 2 implementation below.

Gate 2 keeps schema, privacy projection, production collector normalization, exact adapter correlation, report semantics, and CLI exit codes unchanged. `WindowsCollector` remains a reference/test aggregate; the Worker reuses its mapping helpers and the production CLI uses the supervised path. Controlled cancellation is out-of-band and deliberately produces no report fixture.

## Milestone 5 — Explainable Active-Display Associations (complete; checkpointed locally)

Checkpoint commit: `43d3bc39a6310a48ca7f819bf307650f3afaa6a1` (tree `e5187c757c29a2f492832dee11730ee9c1b8b404`). No push, tag, or release occurred.

The owner accepted the M5 scope and froze its rule, message and evidence contract before implementation. The four informational findings are topology.endpoint-adapter-association, topology.correlation-unresolved, topology.active-path-target-unavailable, and topology.no-active-paths. They use already projected active-path facts, preserve both endpoints and unavailable states, and reference report-local evidence. They do not infer rendering ownership, power, MUX state, health or a hardware cause.

Gate progression: Gate 0 scope accepted; Gate 1 contract frozen; Gate 2 implementation completed; Gate 3 independent semantics/privacy review passed, including correction and independent closure of the test-only F1; Gate 4 scoped physical validation passed on the current laptop; final checkpoint-readiness review passed and the M5 checkpoint was created. The physical result is limited to the current one-active-path configuration; synthetic tests cover unobserved states. Schema 0.2.0, collection, worker/supervisor behavior and CLI exits remain unchanged.

M5 is complete and checkpointed, and is now the latest committed technical milestone. M6 has not started and requires separate scoping and owner authorization. Git staging and commit permissions remain task-specific.

## Later, separately authorized

Potential work includes SetupAPI/WMI inventory comparison, privacy-reviewed ZIP packaging, and a separate GUI decision. Vendor interfaces and classification need their own source, privacy, license, and hardware investigation. No capability or timeline is promised.

BIOS/MUX/driver/service/registry/power-plan changes, telemetry, AI features, automatic fixes, and generic optimization remain outside current scope.
