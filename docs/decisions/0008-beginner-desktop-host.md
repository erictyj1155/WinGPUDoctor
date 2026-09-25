# ADR 0008: Beginner desktop host over the existing engine

Status: **accepted 2026-09-25 (Gate 1 frozen by the owner).** On 2026-09-25 the owner accepted Gate 0 scope and the recommended options below. The same day, an independent read-only architecture review (a different agent from the author) found 0 blockers, 5 should-fix items and 1 nit. All were accepted and are addressed in this revision and `docs/GUI_PLAN.md`. The reviewer ran no tests. After the should-fix revision, the owner froze this ADR. It supersedes the "no GUI" part of ADR 0001. Acceptance does not authorize implementation steps, staging, committing, packaging or release; each needs its own owner authorization.

## Context

v0.1.0 is a console tool, and people who do not use a console cannot run it or read its output. The engine is already separated from the CLI:
- `SupervisedWindowsCollector.CollectAsync` returns a typed `CollectionSnapshot`;
- `HostCancellationController` owns the cancel-versus-output race;
- `PrivacyPolicy.Prepare` produces the only `ShareableReport`;
- `ReportWriter` is the only exporter.

Some host logic is CLI-only today: the collection/output handoff (`CollectionOutput`) and the export destination and write rules in `Program.cs`. There are also constraints a GUI must respect:
- `ShareableReport.Report` is internal.
- Development progress (`CollectionProgress`) is internal and carries PIDs.
- `HostAdmission.Process` is a process-lifetime latch with no reset.
- Worker deployment requires a framework-dependent host on a shared `Microsoft.NETCore.App` layout, with loaded Core/Protocol/Windows identities matching the `worker/` deployment.

## Decision

1. **Framework.** WPF on .NET 10, framework-dependent, using the built-in Fluent theme. No MVVM, DI or other new NuGet dependency in the first release. If the theme API is still flagged experimental, use a scoped and documented suppression rather than disabling warnings-as-errors.
2. **Shared host layer.** Add a UI-free `WinGPUDoctor.Host` project containing the collection/output handoff and the export destination/`CreateNew` write rules. The CLI moves onto it first, with byte-identical output, messages and exit codes.
   - **Scan outcome.** Host returns a host-neutral outcome: `Completed` (with the prepared report and an incomplete flag), `Cancelled` (controlled stop, no report) or `CollectionFailed` (any other collection failure, no report). This keeps the distinction that `SupervisedWindowsCollector` already has and that the CLI collapses into exit `3`. The CLI maps these back to its existing notices and exit codes. Admission and poison state stay internal, so the GUI treats `CollectionFailed` conservatively as restart-required.
   - **Per-scan lifetime.** Each scan creates a fresh `HostCancellationController`, because its committed and closed states are terminal. Report preparation stays behind the existing cancel-versus-output commitment.
   - **Existing seams.** The collector remains injected, so the CLI keeps its private `M4CollectionProbe` via the Supervisor's `InternalsVisibleTo("wingpudoctor")`; no friend access is added for Host or the GUI. The test project's linked `CollectionOutput.cs` is replaced by a Host reference without losing any race test.
3. **Report access.** The GUI displays only data obtained by deserializing `ReportWriter.Json(shareable)` with `ReportWriter.JsonOptions`. The public Core API does not change. A deterministic round-trip test is required. The GUI never renders from a raw `CollectionSnapshot`.
   - Host keeps exactly one `ShareableReport` per completed scan. Result display, every preview and the save all derive from it.
   - Save writes exactly the previewed writer output. A format change requires a new preview, and a new scan discards the old report.
4. **Progress.** The first release shows an indeterminate progress indicator and a fixed description of what is being read. A public, sanitized per-operation progress contract is separate future scope and needs review.
5. **Repeated scans.**
   - Scan is disabled while a scan runs.
   - A host-fatal outcome shows a restart-required message and is never retried automatically.
   - Cancel is single-use and never escalates to a forced interrupt.
   - Closing the window during a scan requests controlled cancellation first; job kill-on-close remains the backstop.
6. **Distribution.**
   - The GUI ships in the same package root as the CLI and `worker/`, framework-dependent. Single-file and self-contained builds are not used.
   - The executable gets a distinct name (`wingpudoctor-gui.exe`), because `WinGPUDoctor.exe` would collide with `wingpudoctor.exe` on case-insensitive file systems.
   - Users need the .NET 10 Desktop Runtime x64. The package allowlist and path guard are extended.
   - The GUI needs no API from `WinGPUDoctor.Windows`, but `WorkerDeployment.Resolve` loads Core, Protocol and Windows into the host process and checks their identities against the `worker/` manifest. The GUI build must therefore deploy those exact assemblies, as the CLI's project reference does today.
7. **Language.** English first. All UI text lives in resources, and the explanation catalog is keyed by stable finding IDs and enum values, so Simplified Chinese can follow as separate scope.

Unchanged:
- schema 0.2.0, privacy projection, rules and findings, supervisor/worker lifecycle, timing policy, CLI arguments and exit codes;
- no elevation, telemetry, network client, background service, automatic fixes, auto-save or report history.

UI copy may explain existing facts and limits. It may not add claims that Core does not make, such as health, driver freshness, rendering GPU, a physical connection, a vendor inferred from labels or IDs, or a cause.
- Display copy describes active display **paths**, with separate source and target associations.
- Local-save copy says only that WinGPUDoctor does not upload. It advises choosing a folder that is not cloud-synchronized, because destination validation cannot detect sync (`PRIVACY.md`).

## Alternatives

- **WinUI 3:** Windows App SDK runtime and packaging dependency, and harder deterministic packaging.
- **Avalonia:** third-party dependency tree for a cross-platform benefit this tool does not need.
- **WinForms:** dated appearance.
- **Re-implementing host rules inside the GUI:** two copies of privacy-relevant export rules.
- **A public `ShareableReport` accessor:** simpler, but widens the privacy boundary.
- **Self-contained GUI:** needs a reviewed change to worker runtime resolution and identity checks.

## Consequences

- A new host, and the first real repeated-scan usage in one process. Same-host admission reuse has so far been proven only by deterministic tests, so live validation must now cover it.
- Beginners still need to install a runtime, and binaries remain unsigned until code signing is scoped.
- UI copy becomes a reviewed privacy/semantics surface.
- Planned screens, the explanation catalog, delivery steps and validation additions are in `docs/GUI_PLAN.md`.
