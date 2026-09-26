# M7 beginner desktop host plan

Status: **M7 plan; Gate 0 accepted by the owner on 2026-09-25 with the recommended options.** Boundary decisions live in [ADR 0008](decisions/0008-beginner-desktop-host.md) (accepted 2026-09-25), and gates are tracked in `ROADMAP.md`. This file holds UX, copy rules, delivery steps and validation details. Each implementation step still needs separate owner authorization.

Record language: English.

## 1. Goal and non-goals

Goal: a beginner-friendly Windows desktop application that lets someone who does not use a console:

1. scan the PC with one button;
2. see and understand the GPU, driver and active-display facts in plain language;
3. preview exactly what would be shared;
4. save that exact snapshot as a local report.

The GUI is a **second host** over the existing engine. The CLI, schema 0.2.0, privacy projection, diagnostic rules, supervisor/worker lifecycle and exit-code semantics stay unchanged.

Non-goals (unchanged project boundaries): no health score or "PC is OK" verdict, no fixes/setters, no elevation, no telemetry or crash upload, no network client, no background service or tray agent, no auto-save, no report history, no driver-update advice framed as a diagnosis.

### Primary users

- **"Someone asked me for my GPU/display info."** A friend, forum or support person asks for details. The user needs to run a scan, check what will be shared and save a file. This is the most important flow.
- **"I'm curious what is in my PC."** The user wants to know which GPU(s) Windows reports, which driver version is installed and what resolution/refresh rate the display path is using.

## 2. Assessment of the current code

Overall: **the engine is already well separated from the CLI.** Most of the work is new host code; almost no engine change is needed.

| Area | Current state | GUI impact |
|---|---|---|
| Collection | `SupervisedWindowsCollector.CreateLocal().CollectAsync(CancellationToken)` is public and returns a typed `CollectionSnapshot`. | Usable directly from a GUI host. No output parsing needed. |
| Cancellation | `HostCancellationController` (public) implements the atomic cancel-vs-output race; `Interrupt()` returns `Controlled`/`Forced`. | A Cancel button can call `Interrupt()` once. |
| Privacy | `PrivacyPolicy.Prepare(snapshot, date)` → `ShareableReport`; `ReportWriter.Json/Markdown` are the only public exporters. | The GUI must render from the `ShareableReport`, never from the raw snapshot, so the screen matches the export. |
| Report contents | `ShareableReport.Report` is **internal** to Core. A host can only get the JSON or Markdown text. | **Gap.** See decision D3. |
| Findings | 6 stable finding IDs, all `information`; 7 `WarningCode`s; `DataState`/`ReasonCode`/`CollectorStatus` enums; messages are English strings in Core. | Plain-language text can be keyed on stable IDs/enums without changing Core. |
| Progress | `CollectionProgress` is `internal` and documented as an opt-in development observation that includes worker PIDs. | **Gap.** The GUI cannot show real per-step progress without a new public, sanitized progress contract (D4). |
| Collection/output handoff | `CollectionOutput.RunAsync` (CLI, `internal`) owns commit-vs-cancel and the "no report" notices. | Needs an equivalent in the GUI, or to be extracted into a shared host layer (D2). |
| Export | Destination checks (full path, reject UNC/device/ADS, local fixed drive) and `FileMode.CreateNew` writing are inline in CLI `Program.cs`. | Should be extracted and shared so the GUI cannot drift from the CLI's no-overwrite and local-only rules (D2). |
| Host admission | `HostAdmission.Process` is a process-lifetime latch: `Busy` while a scan runs, and permanently `HostPoisoned` after unconfirmed cleanup, with no reset. | CLI is one scan per process; a GUI will scan **repeatedly in one process**. Same-host reuse is so far proven only by deterministic tests (STATUS). Needs UI handling and live validation (§6). |
| Deployment | The worker is framework-dependent. `WorkerDeployment.ResolveRuntime` requires the host to run on a shared `Microsoft.NETCore.App` layout. `ValidateLoadedIdentity` hashes `Assembly.Location` of the loaded Core/Protocol/Windows. Deployment is resolved as `AppContext.BaseDirectory/worker`. | The GUI must be **framework-dependent, not single-file, not self-contained**, and live next to the same `worker/` directory, referencing the same Core/Protocol/Windows builds. |
| Runtime prerequisite | Users must install the .NET 10 runtime before running. | A WPF host needs the **.NET 10 Desktop Runtime x64** (which includes the base runtime). This is a real beginner barrier; see D6. |
| Naming | CLI assembly is `wingpudoctor.exe`. | A GUI exe named `WinGPUDoctor.exe` in the same folder would **collide on a case-insensitive file system**. Use a distinct name, e.g. `wingpudoctor-gui.exe`. |

## 3. Proposed architecture

```
WinGPUDoctor.Desktop (WPF, new) ──┐
                                  ├──> WinGPUDoctor.Host (new, UI-free)
WinGPUDoctor.Cli (existing) ──────┘      - collection session (commit vs cancel)
                                         - local export destination + CreateNew write
                                              │
                                              ├──> WinGPUDoctor.Supervisor ──> worker/
                                              └──> WinGPUDoctor.Core (privacy, rules, writers)
```

- `WinGPUDoctor.Host` holds host-neutral logic that is currently CLI-only: the collection/output handoff and the export destination rules. The CLI keeps identical behavior, messages and exit codes. Moving this code needs the existing CLI tests to pass unchanged, and the extraction should come first as its own small step.
- `WinGPUDoctor.Desktop` contains views, view models and the explanation catalog. View models and the catalog should not depend on WPF types, so the existing xUnit project can test them.
- No new data sources, no Core schema change, and no rule change are needed for the first GUI release.

## 4. Decisions

The decisions D1–D7 are recorded in [ADR 0008](decisions/0008-beginner-desktop-host.md). The owner accepted the recommended options:
- D1: WPF with the Fluent theme, no new dependencies.
- D2: a shared `WinGPUDoctor.Host` layer.
- D3: display via the sanitized-JSON round-trip.
- D4: indeterminate progress.
- D5: single-use cancel, and restart required after a host-fatal outcome.
- D6: same-package, framework-dependent distribution with the executable `wingpudoctor-gui.exe`.
- D7: English first, with resources ready for Simplified Chinese.

## 5. User experience

### Screens

1. **Welcome.** In one sentence: what the app reads (Windows' GPU, driver and active-display information). Three visible promises: *reads only, changes nothing*, *WinGPUDoctor never uploads anything*, *you review before saving*. A single primary "Scan this PC" button.
2. **Scanning.** An indeterminate progress indicator, the list of what is being read (system, graphics adapters, drivers, displays) and a Cancel button.
3. **Results.**
   - A top summary without health language, for example: "Scan complete. Windows reports 2 graphics adapters and 1 active display path. 1 item could not be read." If collection was incomplete, it says so plainly; this is the equivalent of CLI exit `3` with a report.
   - Cards: **This PC** (Windows version/build, manufacturer/model), **Graphics adapters** (name, PCI vendor ID, driver provider/version/date), **Active display paths**, one card per path: resolution, refresh rate, output technology, and **both** endpoint associations, the display source adapter and the display target adapter. These can differ, and either can be unresolved; unresolved associations get their own friendly wording. The card never infers a physical connection or a vendor from the report-local GPU label or the PCI vendor ID. Include a **What this can't tell you** card.
   - Each card expands to details and has a "Show technical details" toggle that shows field state, source and reason.
4. **Save report.** A format choice (Markdown/JSON), then a read-only preview of the **exact text** that will be written.
   - Host keeps **one** privacy-projected `ShareableReport` per completed scan. The result cards (via the JSON round-trip) and every preview come from that same report.
   - The preview is the selected writer's exact output, and Save writes exactly the string being previewed. Changing the format shows a new preview. A new scan discards the old report and preview, so the user can never save content they have not seen.
   - A short "What this report contains" summary sits above the exact text; the exact text stays visible because seeing it before saving is the core promise.
   - Redacted fields are highlighted ("Hidden by the privacy filter: 1 field").
   - Shows the `ReviewBeforeSharing` warning.
   - Save uses a Save dialog, but writing always uses `CreateNew`: an existing file is never overwritten, even if the dialog offers it.
   - Only local fixed drives are accepted; there are no network locations.
   - Next to the destination, a short hint that stays visible: "Keep it local: choose a folder that OneDrive, Dropbox or similar apps don't sync." Validation cannot detect cloud-synchronized folders (see `PRIVACY.md`).
   - Success message: "Report saved. WinGPUDoctor did not upload anything."

### Explanation catalog (the part that makes the GUI useful rather than shallow)

A table keyed by stable identifiers: the 6 finding IDs, 7 `WarningCode`s, 5 `DataState`s, the `ReasonCode`s, and `CollectorStatus`. Each entry has:

| Field | Purpose |
|---|---|
| Plain title | e.g. "Display path linked to graphics adapter gpu-1", never a vendor or physical-connection claim |
| What this means | One or two sentences |
| What this does **not** mean | Carries the project's existing limits, e.g. "This does not tell you which GPU a game uses." |
| Optional next step | Manual, informational only, e.g. "If someone is helping you, save a report and send it to them." Never an instruction to change settings. |
| Glossary links | Only terms that actually appear in the report: driver version, refresh rate, resolution, output technology, PCI vendor ID. Do not explain facts that are not collected, such as VRAM or HDR. |

Rules for the copy:
- It must not add claims that Core does not make. Examples: no "driver is outdated" (the warnings already state that driver dates do not establish freshness); no "your display is running at the wrong rate"; no inference of which GPU renders applications.
- States are shown with an icon plus text, never color alone.
- Secondary copy ("What this does not mean", the optional next step, field glossary text, unavailable-state meanings, how hidden values appear) sits behind an (i) tip. Each tip is a Tab stop. It shows its text on hover, on Enter/Space or click, and on a focus change caused by keyboard input from another element in the window (Tab, Shift+Tab or other keyboard navigation; the key itself is not identified), but not when focus returns on window reactivation such as Alt+Tab. It closes with Escape and exposes `AutomationProperties.Name` and `HelpText`. The "Review before sharing" warning and the cloud-sync hint stay on screen in short form.
- `Unknown`/`Failed`/`Redacted` get friendly wording. Example: "Windows didn't provide this" rather than "Unknown (MissingValue)".
- Refresh rate is shown rounded for people (e.g. "165 Hz"), with the exact rational value in technical details.

### Visual design (adopted 2026-09-26)

The owner compared three directions in a local mockup (`artifacts/design/gui-mockup-v1.html`; ignored because it contains real hardware names, and never staged) and chose:
- **Welcome, Scanning and Save: direction B, "diagnostic instrument panel".** Two columns: the primary content and actions on the left, a panel on the right (an idle or reading "Readout" on Welcome and Scanning, the exact report text on Save). Small monospace amber captions sit above the titles; values are cyan monospace.
- **Results: direction C, "guided", in direction B's colors.** A centered heading ("Here's what Windows reported") and one summary sentence; three main cards (Your PC, Graphics adapters, and one card per active display path), each with an icon badge, one plain-language sentence, small details and (i); one driver card per adapter; the "What this scan can't tell you" note; and one "Show technical details" switch.

Not adopted from the mockup: direction A; C's step bar and illustration; B's display-path diagram and large number tiles; and mockup wording that goes beyond Core, such as a Windows edition inferred from the build number or "All information was read".

Visual system:
- **Colors.** Every color is a resource in `Themes/Palette.Dark.xaml`, `Palette.Light.xaml` or `Palette.HighContrast.xaml`, which define the same keys; other XAML and code refer to the keys only, and a test rejects color literals elsewhere. Dark uses the direction B colors: background `#171C22`, panel `#1E252D`, lines `#2E3844`, text `#E4EAF0`, secondary text `#8795A3`, amber accent `#F2A541`, cyan data `#7CC4E0`. Light darkens amber to `#8A5300` and cyan to `#0B6283`. A test checks WCAG AA contrast for every text pair (4.5:1) and for focus rings and badge icons (3:1) in both palettes.
- **Theme.** The palette follows Windows: a high-contrast theme first (mapped to the user's system colors, with text only on the surface Windows pairs it with: window text on window, highlight text on highlight, info text on info), otherwise the Windows app mode (light or dark). The app only reads these settings and switches when Windows announces a change. The built-in Fluent theme (ADR 0008 D1) still supplies the scroll bars.
- **Fonts and icons.** Windows built-ins only, nothing bundled: Segoe UI Variable for text, Cascadia Mono (falling back to Consolas) for values and the report text, and Segoe Fluent Icons (falling back to Segoe MDL2 Assets) for icons. No GPU vendor logos.
- **Layout.** The window opens at 1040 × 720, reduced to the primary work area if needed; its minimum is 520 × 460, which fits 1366 × 768 at 150% scaling. Below 720 px the right panel of a two-column page moves below the content; result cards flow into as many columns as fit (up to three) and the result actions stay visible at the bottom.
- **Motion.** Only the scanning screen animates: an amber sweep and pulsing placeholder lines, both only while visible and only when the Windows "Animation effects" setting is on; otherwise they are steady.
- **Wording.** After a `Forced` cancel result the heading reads "Almost done…" with "It's too late to stop this scan. Please wait while it finishes.", and the readout status says FINISHING. Result copy states counts and what Windows reported; it never says that everything was read, because a completed step can still leave values missing.

### Accessibility and polish

- Keyboard-only operation.
- `AutomationProperties` names for screen readers.
- High-contrast and dark/light theme support.
- Per-monitor DPI awareness.
- The window stays usable at 1366×768 and at 150% scaling.
- The manifest keeps `asInvoker`.

### Error handling

Error dialogs never show exception text or file paths, consistent with AGENTS privacy rules. They use generic messages mapped from the Host scan outcome defined in ADR 0008:
- `Completed`: a report, possibly incomplete.
- `Cancelled`: "Scan stopped. No report was created."
- `CollectionFailed`: "The scan could not complete. Restart WinGPUDoctor before scanning again."

Admission and poison state are not public, so every non-cancel collection failure is treated conservatively as restart-required.

## 6. Delivery plan (gated, like M1–M6)

Each step needs the owner's explicit authorization; none of them includes commit, tag or release authority.

- **Gate 0 — scope acceptance.** Done 2026-09-25.
- **Gate 1 — ADR 0008 + ROADMAP entry.** Independent review on 2026-09-25: PASS WITH SHOULD-FIX ITEMS (0 blockers, 5 should-fix, 1 nit), all addressed; the owner froze ADR 0008 on 2026-09-25.
- **Step 1 — host extraction.** Create `WinGPUDoctor.Host` with the host-neutral scan outcome, the per-scan session and the export rules.
  - The CLI maps the outcomes back to its current notices and exit `3`, keeping identical stdout/stderr content, prompts, exit codes, schema and cancellation behavior.
  - The collector stays injectable, so the CLI keeps constructing it with its private `M4CollectionProbe` (Supervisor friend access `InternalsVisibleTo("wingpudoctor")`); Host does not compose the collector itself.
  - The test project's linked `CollectionOutput.cs` source is migrated to a Host project reference, with every existing race test kept.
  - Verification: all existing tests, new Host unit tests, and a before/after comparison of CLI `--help`, preview, export prompt/decline paths and exit codes.
- **Step 2 — vertical slice.** Welcome → Scan → summary cards → Cancel. Includes the D3 round-trip test. No export yet.
- **Step 3 — explanation catalog and detail views.** Unit tests: every finding ID/`WarningCode`/`DataState`/`ReasonCode` has an entry (a new enum value without copy fails the build), and no entry contains prohibited claims from a reviewed list.
- **Step 4 — save/export flow** on the shared host export code.
- **Step 5 — packaging.** GUI exe (distinct name) in the same package root. Extend the file allowlist and path guard, keep deterministic Release builds, confirm the missing-runtime experience. On 2026-09-26 a local test package (`package-v0.1.ps1 -DevGui`, `-dev` ZIP, not a release asset) and its live check were done; the missing-runtime experience and a release package with the GUI remain open.
- **Step 6 — validation and review.** Validation per §7, a privacy/semantics review of all UI copy, and a usability check.

## 7. Validation additions (for `docs/VALIDATION.md`)

Deterministic:
- The JSON round-trip reproduces the model.
- The explanation catalog is complete.
- Export never overwrites an existing file and rejects UNC/device/ADS paths and non-fixed drives.
- The view model state machine covers Idle → Scanning → Cancelling → Result/Stopped/NeedsRestart, and the second Cancel is a no-op.
- The GUI never receives a raw `CollectionSnapshot` outside the host session.
- Each scan uses a fresh `HostCancellationController`. The outcomes `Completed`, `Cancelled` and `CollectionFailed` are distinct, and report preparation happens only after the output commitment wins.
- Preview and save come from one retained report: the saved bytes equal the previewed string, and a format change or new scan invalidates the preview.

Live, separately authorized, non-administrator:
- **Several scans in one GUI process** (first live evidence for same-host admission reuse).
- Cancel during each stage, with no surviving tracked worker.
- Closing the window during a scan.
- Export in both formats, with schema and privacy checks.
- No network activity.
- No application-created report or log file other than the user-chosen report. OS and runtime activity is outside the application's guarantee.
- Behavior when the Desktop Runtime is missing.

Usability: 2–3 people who do not use the console complete "scan, find your GPU name and display refresh rate, save a report" without help. Record where they hesitated. Findings feed back into the copy, not the engine.

## 8. Explicitly out of scope for the first GUI release

- Live monitoring and utilization graphs.
- GPU classification (iGPU/dGPU).
- Vendor APIs, DXGI.
- Report ZIP bundles.
- Clipboard copy, because cloud clipboard sync is a privacy risk; this can be reconsidered later.
- Report history and auto-update.
- Installer/MSIX.
- Code signing, which is a separate post-v0.1 item. The GUI still ships unsigned, so SmartScreen will warn, and the README must explain SHA-256 verification in beginner terms.
