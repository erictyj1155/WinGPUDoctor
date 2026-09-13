# WinGPUDoctor

**Read → Explain → Report.** An early, read-only Windows GPU/display diagnostics project, separate from any university or research work.

Milestones 1–3 provide a small CLI for inventory and **active display paths**, not a finished troubleshooting application. It reports Windows observations; it does not certify GPU health.

## Implemented

- Windows numeric version/build and firmware-reported computer manufacturer/model.
- WMI-reported video controllers, PCI vendor/device type IDs when recognizable, and matched driver provider/version/date.
- Active DisplayConfig paths with separate source/target adapters, source resolution, rational path/signal refresh, connector type, rotation, and availability.
- Exact DisplayConfig → SetupAPI device-instance → WMI inventory correlation; unmatched/ambiguous cases remain explicit. Clone/extended relationships use per-report source/target labels.
- Explicit available, unknown, unsupported, failed, and redacted field states with provenance.
- Sanitized JSON and Markdown, console preview, and opt-in local file export without overwriting existing files.
- Deterministic tests using synthetic data; separate manual hardware checks.

Topology does not establish application GPU use, GPU utilization/power, MUX state, or Optimus/Advanced Optimus. iGPU/dGPU classification, inactive-display enumeration, vendor APIs, and a GUI remain unimplemented. No automatic fixes, telemetry, network client, background service, configuration writes, or elevation requests are implemented.

The local M2 check found one internal path at 2560 × 1600 and approximately 165 Hz mapped to Intel Graphics. Windows returned no monitor friendly name; that historical run remained unknown with partial-collection exit `3`. M3 keeps the name explicitly unknown but treats absence of that optional metadata as non-blocking, so six repeat collections completed with exit `0` while all path and correlation evidence stayed consistent. This describes this laptop only, not a persistent mode or broad compatibility claim.

## Build and run

Initial validation target: Windows 11 x64. Other Windows versions, Windows on ARM, virtual machines, and remote sessions have not been validated. Use the .NET SDK selected by `global.json` (10.0.401 with later patches allowed). Dependency lock files are included. SDK/package restore needs internet; the built diagnostic program does not.

```powershell
# Process-local SDK settings: avoid SDK telemetry and first-run certificate creation.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet restore --locked-mode
dotnet build --no-restore -c Release -m:1 -p:UseSharedCompilation=false
dotnet test --no-build --no-restore -c Release
dotnet run --project src/WinGPUDoctor.Cli -c Release --no-build -- --help
dotnet run --project src/WinGPUDoctor.Cli -c Release --no-build
dotnet run --project src/WinGPUDoctor.Cli -c Release --no-build -- --format json
```

No arguments prints a sanitized Markdown preview; it does not create a file. JSON on stdout is also sanitized. Redirecting stdout saves it at your discretion. The SDK may print its own build/runtime messages; run the built app directly when a clean JSON stream is required:

```powershell
dotnet src/WinGPUDoctor.Cli/bin/Release/net10.0-windows/wingpudoctor.dll --format json
dotnet src/WinGPUDoctor.Cli/bin/Release/net10.0-windows/wingpudoctor.dll --format json --output report.json
```

`--output` shows a Markdown preview on stderr and asks you to type `EXPORT`. It saves that same snapshot without recollecting. In deliberate automation, `--yes` with `--output` accepts export without the prompt; preview still goes to stderr. Existing files are never overwritten. The parent directory must exist. Direct UNC/device paths, alternate data streams, and non-fixed drives are rejected. See [privacy](PRIVACY.md) for indirect/cloud folder limitations.

Exit codes: `0` collection completed; `2` invalid arguments/platform/destination; `3` incomplete collection; `4` export declined; `5` export failed. A partial report can still be previewed or saved with exit code `3`. An empty optional monitor friendly name remains explicit `unknown/missingValue` without independently making collection partial; target-name API failures, missing required identity, invalid modes, unavailable targets, correlation failures, provider failures, and retry exhaustion remain incomplete. Attempted but unsupported topology is also incomplete; intentionally deferred features do not cause failure by themselves.

For this initial workspace, a SHA512-verified portable SDK is in ignored `.tools/dotnet`. The supplied scripts require PowerShell 7. `scripts/dev.ps1` uses the portable SDK when present, otherwise the installed SDK; it keeps SDK state/package cache under `.tools` and disables telemetry and certificate generation. This folder is a local development convenience, not part of the distributed app.

```powershell
./scripts/dev.ps1 -Action test
./scripts/dev.ps1 -Action preview
# Opt-in repeatability check for the current laptop build:
./scripts/validate-m3.ps1
```

## Project map

For any new contributor or coding agent, start with [AGENTS.md](AGENTS.md) → [STATUS.md](STATUS.md) → actual Git status/diff/history → relevant code/tests. These repository records support handoff across models and providers without conversation memory. After authorized work and verification, update the current status if it changed; keep dated evidence in the existing validation record.

```text
src/
  WinGPUDoctor.Core/       Facts, states, rules, privacy boundary, JSON/Markdown
  WinGPUDoctor.Windows/    Local WMI, isolated DisplayConfig/SetupAPI, matching
  WinGPUDoctor.Cli/        Arguments, preview, explicit local export
tests/WinGPUDoctor.Tests/  Synthetic unit and collector-contract tests
docs/                     Feasibility, schema guide, decisions, validation
schemas/                  Versioned JSON Schema
examples/                 Synthetic reports only
scripts/                  Development and opt-in hardware checks
.github/workflows/        Deterministic Windows CI; no uploaded system reports
```

Start with [API feasibility](docs/API-FEASIBILITY.md), [architecture](ARCHITECTURE.md), [report schema](docs/REPORT-SCHEMA.md), and [validation evidence](docs/VALIDATION.md). Decisions are recorded in [ADRs](docs/decisions/README.md). See [roadmap](ROADMAP.md), [contributing](CONTRIBUTING.md), and [security](SECURITY.md).

MIT licensed. See [STATUS.md](STATUS.md) for the current Git/commit state and handoff, and [validation evidence](docs/VALIDATION.md) for the preserved M1 baseline and review commands. Follow the owner's existing commit policy; do not invent Git identity or publish automatically.
