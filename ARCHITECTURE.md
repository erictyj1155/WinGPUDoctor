# Architecture

Status: M1 foundation extended by M2 active topology, M3 single-laptop hardening, and M4 supervisor-backed collection. The production CLI uses short-lived workers for the five fixed operations. M4 remains a historical checkpoint at commit `57abbec81ac8db83e04a2a588d39fe18c1650070`; M5 — Explainable Active-Display Associations — passed independent semantics/privacy review and scoped physical integration validation on the current laptop, then was checkpointed at commit `43d3bc39a6310a48ca7f819bf307650f3afaa6a1` (tree `e5187c757c29a2f492832dee11730ee9c1b8b404`). Schema 0.2.0 and M3/M4 collection and lifecycle boundaries remain unchanged. Physical validation remains limited to one laptop. M6 — v0.1 Release Hardening and OSS Readiness — was completed and checkpointed at commit `f966e1b602561044c19eaca6a84cf4baa146b62a` without changing these architecture boundaries; see `STATUS.md` for rolling milestone and release state.

## Stack

C# / .NET 10 LTS, `System.Management` isolated in a Windows-specific assembly, `System.Text.Json` and a small Markdown renderer in the core, xUnit for deterministic tests. No GUI framework, dependency-injection container, plugin loader, database, or background host is needed for this milestone. .NET 10 is selected for its remaining LTS runway; SDK/package versions are pinned and lock files are retained. See the [official lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

C# consumes WMI and an isolated Unicode P/Invoke surface for DisplayConfig/SetupAPI. No dependency was added in M2. Definitions were checked against Microsoft's SDK documentation and verified with size/offset tests and a binary union fixture. SetupAPI provides a direct device-instance bridge, so DXGI was not introduced. C++ or Rust is unnecessary for this scope.

## M1–M3 dependency direction (historical)

```text
CLI ──────────────> Windows collector ───────> Core contracts
 │                                             ↑
 └───────────────> Core privacy/rules/writers ───┘
Future GUI ──────> same collector and core interfaces
```

1. **System collection:** two fixed local WMI projections for numeric OS version/build and firmware manufacturer/model.
2. **GPU collection:** WMI video-controller inventory; match display-class signed drivers by full case-insensitive instance ID in temporary memory. Never match by name, vendor, list order, or approximate PCI ID.
3. **Display collection:** `DisplayTopologyCollector` queries active paths through injectable native interfaces. Each domain entry preserves source/target keys and adapters, source dimensions, rational path/signal rates, rotation, and target availability. Clone/extended relationships remain representable. `WindowsCollector.CreateLocal()` composes WMI and topology; the WMI-only injectable constructor remains for M1 tests.
4. **Normalization/model:** `Observation<T>` enforces state/value/reason consistency. Collectors return projected facts and structured collection outcomes, with no conclusions or raw provider dumps.
5. **Privacy:** `PrivacyPolicy.Prepare` copies permitted facts. `TopologyPrivacy` regenerates labels, remaps unique GPU references, validates GDI/enum text, and filters names. Native LUIDs, paths, instance IDs, and EDID fields never enter domain facts. Public writers cannot accept raw collector objects. This is a code boundary, not a sandbox against malicious extensions or reflection.
6. **Rules:** pure functions over privacy-projected facts. M1 explains multiple or zero reported controllers. M5 appends bounded informational interpretations of active endpoint associations, unresolved correlation, unavailable targets and available empty topology. Findings contain report-local evidence paths and do not infer mode, health, activity, or causes.
7. **Reports:** one `ShareableReport` feeds JSON and Markdown. No raw export escape hatch. A future ZIP writer can package the same two outputs plus a sanitized manifest; arbitrary log/file inclusion is not permitted by this design.
8. **CLI:** validates arguments before collecting, previews without writing by default, asks for explicit export acceptance, and never overwrites an existing file. File I/O belongs to the CLI, not the collector/core.

## Failure and resource behavior

Each WMI query reports expected permission/provider/COM failures independently. Missing values, ambiguous driver matches, unrecognized PCI identity, and invalid dates remain explicit. A failed query discards any partially enumerated rows; successful independent collectors survive. `CollectorRun` summarizes completeness, while individual fields carry specific reasons.

Managed WMI objects are disposed. Queries use 10-second connection/enumeration timeout settings and cap output at 128 rows per query. These settings are **not a hard overall deadline**: synchronous COM/provider calls can still hang, and the four queries do not form an atomic hardware snapshot. M4 now places production queries behind the bounded supervisor described below. A first Ctrl+C during supervised collection requests controlled cancellation and returns `3` without preview or export; a second interrupt falls back to default forced termination, and the Job Object kill-on-close remains the containment backstop. No persistent service is proposed.

## M4 worker supervisor and production collection

`WinGPUDoctor.Protocol` defines concrete, bounded protocol v1 messages. `WinGPUDoctor.Supervisor` owns monotonic deadlines, deployment validation, creation-time Job containment, pipe/session lifetimes and parent snapshot assembly. `WinGPUDoctor.Worker` is framework-dependent and deployed under `<CLI output>/worker/`. The production CLI composes `SupervisedWindowsCollector`; the pre-M4 aggregate `WindowsCollector` remains a reference/test path and supplies shared mapping helpers used by the real worker dispatcher.

Gate 1 passed after the targeted corrections. Gate 2 now dispatches the frozen real operations:

- `wmi.operatingSystem`
- `wmi.computerSystem`
- `wmi.videoControllers`
- conditional `wmi.displayDrivers`
- `display.activeTopology`

The parent runs them sequentially under one supervisor, omits the driver worker when video inventory is unavailable or empty, retains completed independent facts when a later operation fails/times out, and assembles the existing `CollectionSnapshot`. The topology worker performs the existing DisplayConfig → source/target metadata → SetupAPI resolution → exact WMI instance correlation path; only report-local labels and transient instance IDs cross the protocol boundary.

The targeted integrated corrections normalize only blank WMI identities to null at dispatch, preserving their rows. WMI announces its attempt immediately before the provider read; topology announces each attempt immediately before its existing sizing/query iteration. Omitted drivers release their reservation before topology starts. A fixed, content-free protocol failure carries worker output `ResourceLimit` without fabricating native issues. The targeted integrated semantics/privacy re-review passed on those corrections, and the final pre-checkpoint phase then added controlled cancellation, calibrated internal timing and six-run healthy-machine validation on one laptop.

### Controlled cancellation and calibrated timing

`HostCancellationController` uses one atomic state transition from active collection to either cancellation or output committed. Cancellation is published before token signaling; second/reentrant interrupts permit default termination. `CollectionOutput` is the actual CLI boundary: it atomically commits output before closing collection and removing the handler, and invokes all report preparation/preview/export only if that commitment won. A callback captured before event removal but entering after commitment cannot set cancellation and permits ordinary/default termination. A cancellation winner returns `3` with no output even while token signaling is still in flight. The callback performs no worker cleanup, I/O or process waits. Disposal closes the lifecycle and defers cancellation-source disposal until active callbacks finish.

The supervisor closes result acceptance, blocks later operations and uses the existing absolute cleanup deadline to terminate and confirm the worker. Accepted independent results remain internal when host cancellation wins. Default forced termination promises no cleanup, exit code or report. Deterministic gate tests compile the actual CLI boundary and force both handoff outcomes; event removal alone is not the synchronization mechanism.

`CollectionTimingPolicy` holds the unchanged bounded values: overall 60 s, per-operation 10 s, cleanup allowance 2 s, later-operation reservation 10 s, final bookkeeping reserve 500 ms, connect 2 s, frame 8 s. Every value stays finite, maps to no `INFINITE` wait, and preserves separate current/later cleanup, later-active and bookkeeping reservations. The measured inclusive operation contains result wait and cleanup; stages must not be added as independent costs. Connect margin is engineering judgment supported by component measurements, and final bookkeeping is an unmeasured conservative reserve. Report preparation/export are outside collection. See the corrected segment definitions and margins in [VALIDATION.md](docs/VALIDATION.md); no population claim or public timeout option is provided.

An internal opt-in lifecycle probe (`WINGPUDOCTOR_M4_PROCESS_PROBE=1`) supplies fixed operation/attempt markers, CLI/worker PID plus birth identity, and monotonic points to the development harness only. Normal execution has no progress sink. The harness captures identities before signaling, timestamps the actual signal call, and checks those identities after CLI exit without rediscovering descendants through an exited root. This instrumented one-laptop evidence is separate from synthetic admission reuse/poison tests.

[ADR 0007](docs/decisions/0007-bounded-supervisor-and-worker-protocol.md) defines the corrected contract, including:

- Absolute deadlines shared by all partial frame reads, and one clipped cleanup deadline with no extra disposal wait.
- Managed pipe I/O whose task, cancellation-completion and handle owners remain retained until completion is confirmed. Unconfirmed cleanup roots the session in a process-lifetime quarantine and permanently poisons default worker admission.
- Separate current/later cleanup, later active and final bookkeeping reservations; skipped-operation continuation; first-interrupt controlled cancellation, second-interrupt forced termination, and collection-wide cancellation while retaining accepted independent facts.
- Read-only parent elevation/integrity checks; creation-time `JOB_LIST` and `HANDLE_LIST`; initialization-aware attribute cleanup with strong handle references.
- Exactly two inherited handles: the connected pipe client for standard input/output and write-only NUL for standard error. A current-user DACL excludes other users and remote clients; it does not authenticate against hostile same-user processes.
- One dependency-derived recursive deployment closure, including nested runtime assets; PDBs excluded, other unexpected files rejected. Shared loaded/deployed MVIDs and hashes must match, and runtime selection is pinned to the recognized parent runtime.
- Request/Ready/Start sequencing: build identity is verified before topology input is sent. Complete topology run/issues/attempts and unavailable inventory identities are representable; successful/partial results undergo semantic and reference validation.

Schema 0.2.0, privacy projection, report writers and CLI exit meanings are unchanged. Cancellation is out-of-band and creates no report state or fixture, and `host-cancelled` remains an internal supervisor code. The final checkpoint-readiness review passed, and the M4 checkpoint was created locally before any publication. Fresh evidence and limits are in [VALIDATION.md](docs/VALIDATION.md).

## M7 hosts (in progress)

[ADR 0008](docs/decisions/0008-beginner-desktop-host.md) adds a second host over the unchanged engine. `WinGPUDoctor.Host` is UI-free: `HostScanSession` owns one scan's cancel-versus-output commitment and returns `Completed` (with the `ShareableReport`), `Cancelled` or `CollectionFailed`; `HostExport` holds the local-destination and `CreateNew` write rules. The CLI and the WPF `WinGPUDoctor.Desktop` (`wingpudoctor-gui`) both inject `SupervisedWindowsCollector` into it. The GUI displays only the report read back from `ReportWriter.Json` (never a raw snapshot); its preview is the exact writer output of that one retained report, and Save writes that string through `HostExport`. Its view models, explanation catalog and resource-based copy stay free of WPF types so the xUnit project tests them. The GUI deploys the same Core/Protocol/Windows builds and `worker/` as the CLI. Evidence is in [VALIDATION.md](docs/VALIDATION.md).
## M2 topology semantics

For each distinct source/target LUID, `DISPLAYCONFIG_ADAPTER_NAME` returns an interface path. `SetupDiOpenDeviceInterfaceW` and the documented device-info-only `SetupDiGetDeviceInterfaceDetailW` query resolve its devnode; `SetupDiGetDeviceInstanceIdW` reads its instance ID. Unique ordinal case-insensitive equality with WMI controller instance IDs gives `exactSetupApiInstanceId` evidence and `exact` confidence. Zero/multiple matches remain unmatched/ambiguous. Confidence describes this identity join only, not health, chronology, or rendering. No path parsing, PCI-only/name/order join, or DXGI fallback is used. A SafeHandle disposes SetupAPI information sets.

Domain/export references use per-report GPU/path/source/target/adapter/clone labels. Source and target adapters can differ. Inventory retains adapters without active paths; this does not establish their workload or power state.

Queries allow at most three sizing/query attempts, cap counts at 128 paths/512 modes, validate returned counts, and record recovered races. Mode lookups require matching type, LUID, and endpoint ID. Name/bridge failures preserve other path facts. A successful target-name query with an empty optional friendly name keeps an explicit `unknown/missingValue` diagnostic without independently making the run partial; actual target-name API failures remain substantive. Availability is separate from path-active state. Generic errors stay generic. `CollectionIssue` records operation/reason and an actual numeric error when available, with no raw strings. Display failures do not erase WMI results.

Windows 10 enables virtual-mode awareness; Windows 11 build 22000+ also enables virtual-refresh awareness. Earlier API contexts select active paths only; older OS/.NET support is not claimed. Invalid-parameter errors are reported rather than hidden by fallback. Virtual-aware paths use packed 16-bit indexes; others use full 32-bit indexes. Path refresh and signal VSync remain separate rationals, accompanied by query mode and the Windows 11 boost flag. No FPS, VRR activity, or GPU workload is inferred. The desktop-image union is laid out but not exported.

Paths/modes come from one successful query. Names and SetupAPI/WMI identity data are separate reads that can race with hot-plug; the report is not globally atomic. Inactive/HMD-specific paths, proprietary mode state, and setters are outside M2.

## Vendor extension seam

Keep vendor-specific read-only collectors in separate assemblies with explicit capabilities and provenance. Add them through code/composition rather than dynamic DLL discovery. They must return typed observations, use the same privacy boundary, report missing runtime/library/device support, and avoid requiring a vendor module for standard inventory. Shared DTOs must not expose NVIDIA/AMD/Intel/OEM native types. Each new vendor fact requires schema and rule review; conflicting sources stay separate until reconciled with evidence. Vendor extension loading is not implemented in M1.

## Trust and boundaries

All local/provider strings are untrusted input. No free-form exception text enters reports. Query definitions are fixed; no arbitrary WQL or remote host argument. The app has an `asInvoker` manifest, does not enable extra privileges, contains no network calls, and does not call configuration-changing Windows APIs. A read-only query may still invoke a driver/provider and affect transient activity; this is not a guarantee of zero GPU wake-up or zero OS-internal side effects.

Architecture and schema remain pre-1.0. Changes need migration/version notes, privacy tests, and an ADR when they change the accepted boundary.
