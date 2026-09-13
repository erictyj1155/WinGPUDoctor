# Architecture

Status: M1 foundation extended by M2 active topology and M3 single-laptop hardening, 2026-09-13. Core, Windows, and CLI boundaries are preserved.

## Stack

C# / .NET 10 LTS, `System.Management` isolated in a Windows-specific assembly, `System.Text.Json` and a small Markdown renderer in the core, xUnit for deterministic tests. No GUI framework, dependency-injection container, plugin loader, database, or background host is needed for this milestone. .NET 10 is selected for its remaining LTS runway; SDK/package versions are pinned and lock files are retained. See the [official lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

C# consumes WMI and an isolated Unicode P/Invoke surface for DisplayConfig/SetupAPI. No dependency was added in M2. Definitions were checked against Microsoft's SDK documentation and verified with size/offset tests and a binary union fixture. SetupAPI provides a direct device-instance bridge, so DXGI was not introduced. C++ or Rust is unnecessary for this scope.

## Dependency direction

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
6. **Rules:** pure functions over sanitized facts. M1 explains multiple or zero reported controllers only. Findings contain evidence paths and do not infer mode, health, activity, or causes.
7. **Reports:** one `ShareableReport` feeds JSON and Markdown. No raw export escape hatch. A future ZIP writer can package the same two outputs plus a sanitized manifest; arbitrary log/file inclusion is not permitted by this design.
8. **CLI:** validates arguments before collecting, previews without writing by default, asks for explicit export acceptance, and never overwrites an existing file. File I/O belongs to the CLI, not the collector/core.

## Failure and resource behavior

Each WMI query reports expected permission/provider/COM failures independently. Missing values, ambiguous driver matches, unrecognized PCI identity, and invalid dates remain explicit. A failed query discards any partially enumerated rows; successful independent collectors survive. `CollectorRun` summarizes completeness, while individual fields carry specific reasons.

Managed WMI objects are disposed. Queries use 10-second connection/enumeration timeout settings and cap output at 128 rows per query. These settings are **not a hard overall deadline**: synchronous COM/provider calls can still hang, and the four queries do not form an atomic hardware snapshot. Ctrl+C can stop the CLI. A future hardened collector may use a bounded worker process, only if failure testing justifies it; no persistent service is proposed.

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
