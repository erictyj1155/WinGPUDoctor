# Roadmap

## Milestone 1 — foundation and feasibility

- [x] Document Windows API choices and limitations.
- [x] Separate core, Windows collector, and CLI.
- [x] Model facts, interpretations, warnings, unavailable states, and collection outcomes.
- [x] Privacy projection, JSON/Markdown, preview and explicit export.
- [x] Small WMI inventory proof of concept and synthetic tests.
- [x] Record local validation evidence and remaining gaps.

## Recommended Milestone 2 — active display topology spike only

Implement a read-only `GetDisplayConfigBufferSizes` / `QueryDisplayConfig` / `DisplayConfigGetDeviceInfo` collector, plus the minimum DXGI enumeration necessary to associate adapters by LUID. Do not add a GUI, vendor modules, activity monitoring, MUX detection claims, or fixes.

Acceptance criteria:

1. Enumerate active desktop display paths; preserve clone relationships and source/target distinctions.
2. Report pixel dimensions and rational configured refresh with explicit virtual/physical mode semantics; label unsupported cases.
3. Map native adapter identities in memory to report-local IDs. Never export monitor device paths, EDID serials, or raw LUIDs. Do not join different adapters solely on PCI vendor/device IDs.
4. Bound retries after hot-plug buffer changes, validate native struct sizes/mode indexes, and preserve partial API failures.
5. Return explicit unavailable states for inaccessible console sessions, unsupported drivers, and disappearing adapters. Do not suggest elevation as a universal workaround for RDP.
6. Test deterministic interop parsing and race/failure paths. Conduct opt-in standard-user hardware checks on at least an internal-panel hybrid laptop and an external display setup; log which cases remain untested.
7. Explain **display scan-out routing** separately from the adapter rendering an application. No claims about electrical MUX position or dGPU sleep from topology alone.

## Later, evidence-gated

- SetupAPI present-device inventory and driver-property comparison, including disabled/disconnected adapters.
- Classification spike using documented D3DKMT hybrid flags and/or D3D12 UMA evidence; conflicting and absent evidence remains unknown.
- Explicit user-triggered before/after snapshots; no background monitor by default.
- Vendor-specific read-only capabilities only when standard APIs leave a concrete diagnostic gap; assess redistribution terms and wake-up effects first.
- Harden collection cancellation if provider-hang evidence requires a worker process.
- Privacy-reviewed ZIP support bundle, then a separate GUI decision.

Automatic remediation, BIOS/MUX/driver/service/registry/power-plan changes, telemetry, AI features, and generic optimization are outside this roadmap's current authorization.
