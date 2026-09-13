# Architecture

Status: accepted M1 foundation, 2026-09-10. Implementation and future design are distinguished below.

## Stack

C# / .NET 10 LTS, `System.Management` isolated in a Windows-specific assembly, `System.Text.Json` and a small Markdown renderer in the core, xUnit for deterministic tests. No GUI framework, dependency-injection container, plugin loader, database, or background host is needed for this milestone. .NET 10 is selected for its remaining LTS runway; SDK/package versions are pinned and lock files are retained. See the [official lifecycle policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

C# can consume WMI directly and documented Win32/COM interfaces through interop. A later DisplayConfig/DXGI spike should evaluate generated bindings such as CsWin32 against a small reviewed interop surface. Do not add bindings to M1 just to reserve a dependency. C++ gives direct Windows SDK access but increases resource/lifetime and build complexity; Rust offers memory safety but adds a second ecosystem without a demonstrated requirement. Neither is necessary for the current four WMI queries.

## Dependency direction

```text
CLI ──────────────> Windows collector ───────> Core contracts
 │                                             ↑
 └───────────────> Core privacy/rules/writers ───┘
Future GUI ──────> same collector and core interfaces
```

1. **System collection:** two fixed local WMI projections for numeric OS version/build and firmware manufacturer/model.
2. **GPU collection:** WMI video-controller inventory; match display-class signed drivers by full case-insensitive instance ID in temporary memory. Never match by name, vendor, list order, or approximate PCI ID.
3. **Display collection:** explicit unsupported result in M1. Next spike will use DisplayConfig active paths and DXGI adapter identity. The reserved display model supports local adapter references and rational refresh rates; it is not an implemented collector.
4. **Normalization/model:** `Observation<T>` enforces state/value/reason consistency. Collectors return projected facts and structured collection outcomes, with no conclusions or raw provider dumps.
5. **Privacy:** `PrivacyPolicy.Prepare` copies the permitted fields, validates structured formats, removes suspicious free text, regenerates report-local GPU IDs, and drops reserved display data. Raw objects cannot be passed to public report writers. This is a deliberate code boundary, not a sandbox against malicious extensions or reflection.
6. **Rules:** pure functions over sanitized facts. M1 explains multiple or zero reported controllers only. Findings contain evidence paths and do not infer mode, health, activity, or causes.
7. **Reports:** one `ShareableReport` feeds JSON and Markdown. No raw export escape hatch. A future ZIP writer can package the same two outputs plus a sanitized manifest; arbitrary log/file inclusion is not permitted by this design.
8. **CLI:** validates arguments before collecting, previews without writing by default, asks for explicit export acceptance, and never overwrites an existing file. File I/O belongs to the CLI, not the collector/core.

## Failure and resource behavior

Each WMI query reports expected permission/provider/COM failures independently. Missing values, ambiguous driver matches, unrecognized PCI identity, and invalid dates remain explicit. A failed query discards any partially enumerated rows; successful independent collectors survive. `CollectorRun` summarizes completeness, while individual fields carry specific reasons.

Managed WMI objects are disposed. Queries use 10-second connection/enumeration timeout settings and cap output at 128 rows per query. These settings are **not a hard overall deadline**: synchronous COM/provider calls can still hang, and the four queries do not form an atomic hardware snapshot. Ctrl+C can stop the CLI. A future hardened collector may use a bounded worker process, only if failure testing justifies it; no persistent service is proposed.

## Vendor extension seam

Keep vendor-specific read-only collectors in separate assemblies with explicit capabilities and provenance. Add them through code/composition rather than dynamic DLL discovery. They must return typed observations, use the same privacy boundary, report missing runtime/library/device support, and avoid requiring a vendor module for standard inventory. Shared DTOs must not expose NVIDIA/AMD/Intel/OEM native types. Each new vendor fact requires schema and rule review; conflicting sources stay separate until reconciled with evidence. Vendor extension loading is not implemented in M1.

## Trust and boundaries

All local/provider strings are untrusted input. No free-form exception text enters reports. Query definitions are fixed; no arbitrary WQL or remote host argument. The app has an `asInvoker` manifest, does not enable extra privileges, contains no network calls, and does not call configuration-changing Windows APIs. A read-only query may still invoke a driver/provider and affect transient activity; this is not a guarantee of zero GPU wake-up or zero OS-internal side effects.

Architecture and schema remain pre-1.0. Changes need migration/version notes, privacy tests, and an ADR when they change the accepted boundary.
