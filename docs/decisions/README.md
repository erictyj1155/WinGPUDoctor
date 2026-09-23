# Architectural decision records

Accepted on 2026-09-10 through 2026-09-15. Revisit only with new requirements or evidence; record a superseding ADR rather than erasing the earlier rationale.

| Record | Decision |
|---|---|
| [0001](0001-stack-and-boundaries.md) | C#/.NET 10; small core, Windows collector, CLI; no GUI |
| [0002](0002-inventory-before-topology.md) | WMI inventory now, DisplayConfig/DXGI topology spike next |
| [0003](0003-report-and-privacy-boundary.md) | Explicit states and typed privacy projection before every export |
| [0004](0004-open-source-foundation.md) | MIT; local repository first; no publishing or telemetry |
| [0005](0005-active-topology-and-instance-correlation.md) | Active DisplayConfig paths, exact SetupAPI bridge, no DXGI dependency, schema 0.2 |
| [0006](0006-optional-monitor-name-severity.md) | Empty optional monitor friendly name stays diagnostic-only; M3 keeps substantive failures incomplete |
| [0007](0007-bounded-supervisor-and-worker-protocol.md) | Gate 1 and integrated corrections approved; final timing/checkpoint review failed, targeted atomic-handoff/evidence corrections implemented; pending final checkpoint-readiness re-review |
