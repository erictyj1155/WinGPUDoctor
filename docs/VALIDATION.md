# Validation evidence

Use [STATUS.md](../STATUS.md) for the current checkpoint and [AGENTS.md](../AGENTS.md) for the operating workflow. Dated sections below describe their own checked snapshot; historical next-step proposals do not override current scope or accepted superseding ADRs.

## Current reading guide — 2026-09-26

M1–M6 technical milestones are complete locally. The M6 checkpoint is `f966e1b602561044c19eaca6a84cf4baa146b62a`; the focused Gate 3 re-review passed with F1/F2 closed, as recorded in `STATUS.md` and `ROADMAP.md`. The refreshed local candidate ZIP with SHA-256 `cdefbb10d1f401b3574119255924facece084c16f878081bfcc1183786cdcc8d` passed its own packaged validation on one Windows 11 x64 laptop before Phase B documentation changes. Because README and SECURITY are included in the package, this candidate is historical evidence rather than the final publication artifact; P1.3 requires a fresh package and checksum. Dated sections below retain their original wording and describe the state when each check was made, including then-pending M6 steps. Consult `STATUS.md` and live Git for current state. The repository is Public with Private Vulnerability Reporting enabled and verified; v0.1.0 was published on 2026-09-25. P1.2 Phase F closed when its documentation reconciliation was committed and pushed as `5354cce40217acb6bd867bc7e408474d37fb9493` and hosted CI passed 304/304 tests and schema verification. P1.3 is complete: P1.3A pre-freeze remediation is complete (its commit `a9a83c271fa268d836d11f432e1411c403f30b67` passed hosted CI), and its changes to the packaged `README.md` and CLI help also differ from the historical candidate. The first release source, `bdefbac75dba0e5f65af59c67677e69ad4c7fb27`, was superseded because its package candidate embedded the local build path. The P1.3B remediation commit `62e25b7b12d04499f7b39bae417418d33cf297ee` is release source S2, and its release candidate ZIP (SHA-256 `65162553242aea18e5b1cebe963b769de76ebd7f7729d360ae151fa984ce63a8`) passed the final audit, path guard, non-live and live validation. The two sections after the publication record describe this; the P1.3A pre-freeze plan describes its own snapshot. The v0.1.0 publication section records the annotated tag and two public assets. The M7 Step 1 section records the locally implemented host extraction and its bounded validation, and the section after it records its independent review and the authorized follow-up. Newer M7 step sections appear above them, newest first. The Codex follow-up re-review of parts A and B, and its fixes, are recorded as a subsection directly under the Steps 1-4 Codex review. The M7 Step 5 local test package is the newest section, with its Codex review as a subsection, followed by the M7 visual redesign (owner batch of 2026-09-26) and its Codex review.

## M7 Step 5 local test package and live check — 2026-09-26

**Scope.** Owner-authorized in the same batch as the fixes for the Codex review of the visual redesign (next section): a local test package only, no publication, no version change. Committed as `c26c0ff83a64c043c1dbc20c7cf383b3267e7961`. `scripts/package-v0.1.ps1 -DevGui` packages the CLI, `wingpudoctor-gui.exe` and one `worker/` in the same folder as `WinGPUDoctor-<version>-dev-win-x64.zip` (with a `.sha256`) under ignored `artifacts/`; without the switch the script keeps the release layout and name and still refuses to overwrite an existing artifact. The file lists moved to `scripts/package-layout.ps1`. The release (CLI) layout now includes `WinGPUDoctor.Host.dll`, which the CLI has needed since M7 Step 1 and which the v0.1 list lacked, and packaging now fails if any runtime file that an executable's `deps.json` declares is missing from its layout. The dev mode also checks that the GUI executable is an x64 PE with product version 0.1.0, that its runtime configuration names .NET 10 `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`, and that the seven files both builds produce are byte-identical (they are packaged once). `scripts/package-path-guard.ps1` covers the GUI files like every other packaged file and now also rejects any PE whose PDB path is under a Windows user profile. No Host, CLI, Core, schema, privacy-rule or NuGet change.

**Deterministic validation.** The new `scripts/test-package-layout.ps1` runs in `./scripts/dev.ps1 -Action test` after the build: **26 checks** covering both layouts (release = CLI with Host and no GUI file; test package = release plus exactly the four GUI files; no PDB or duplicate; 6 and 7 first-party DLLs), both names (only the test package is marked `-dev`), deps coverage for the CLI and the GUI, byte identity of the shared files, the guard over the four built GUI files (no local path; the GUI DLL's PDB path is `/_/src/WinGPUDoctor.Desktop/obj/Release/net10.0-windows/wingpudoctor-gui.pdb`), and that the guard reports a profile path in GUI JSON (UTF-8 and UTF-16) and GUI DLL and executable PDB paths rewritten in place to a profile path; a mutation removing the new guard rule failed it. Full run outside the sandbox: Release build 0 warnings and 0 errors, xUnit **368/368**, schema and all helper checks, package layout 26/26.

**Package.** `artifacts/WinGPUDoctor-0.1.0-dev-win-x64.zip`, 871730 bytes, SHA-256 `1f17c99ce24a1204d21ddaf918811b03e4db67bc135cd3d9006f9b24265c3c42`, built from `c26c0ff` with a clean tree: 30 entries in one `WinGPUDoctor-0.1.0-dev-win-x64/` folder (16 application files, 5 documents, 9 worker files); the path guard found nothing in the staging folder or the ZIP, and the five packaged documents are byte-identical to the checkout. An earlier ZIP built before the README usage line was committed was deleted and rebuilt. The published `WinGPUDoctor-0.1.0-win-x64.zip` in `artifacts/` is unchanged (SHA-256 `65162553…`). The release mode was not run: the v0.1.0 ZIP already exists there, and a second file with the release name was not created.

**Live check (non-administrator, this laptop).** The ZIP was extracted to the ignored `artifacts/m7-step5-live-a7ffd813c51643d7803c5e47c462f736/package/`. For each pass, Explorer opened that folder with `wingpudoctor-gui.exe` selected and the file was opened through Explorer's own open action (UI Automation invoke on the selected item, as a double-click does); the GUI process ran from the extracted folder with `explorer.exe` as its parent. Each pass: Welcome, scan, result with technical details and scrolled, a second scan cancelled while reading, scan again, save through the real Save dialog, close.
- **Dark mode:** background pixels `#171C22`/`#1E252D`; stopped 301 ms after Cancel; the JSON report (9312 bytes) was byte-identical to the preview without a BOM, passed `Test-Json` against the 0.2.0 schema and `Get-M3PrivacyFailures` with 0 findings; the window closed, no worker survived, and the extracted folder was unchanged (same file list and hashes).
- **Active high-contrast theme** (switched by the owner in Windows Settings; the agent only read the state: `HighContrast` true, window `#000000`, window text `#FFFFFF`, highlight `#D6B4FD`, highlight text `#2B2B2B`): background pixels were the system window color; captions, text, icons and borders were window text on window; the primary buttons were the highlight color with highlight text; stopped 326 ms after Cancel; the Markdown report (4459 bytes) was byte-identical to the preview without a BOM; clean close, no surviving worker, extracted folder unchanged.

Screenshots and saved reports are in the ignored `artifacts/m7-step5-live-a7ffd813c51643d7803c5e47c462f736/shots/`; they contain real hardware names and are not committed.

**Limits.** No physical mouse double-click: Explorer's open action was invoked through UI Automation. The ZIP was created locally, so it had no Mark of the Web and SmartScreen was not exercised; the missing-Desktop-Runtime experience, other machines, the release-mode package, a `Forced` cancel result, "Animation effects" off, the minimum window size and a screen reader were not tested live. For Explorer-launched processes the exit code is not observable from the driver; the close was confirmed by process exit. One laptop and one display configuration only.
### Independent review (Codex) of `eff5e23`..`0671a37` — 2026-09-26

**Result.** 1 blocker, 2 should-fix items, 1 nit. The reviewer confirmed that the readout reaches FINISHING on both `Forced` paths, that the default package list includes `WinGPUDoctor.Host.dll` and excludes the GUI, that the `-dev` ZIP has the expected 30 entries and scans clean, and that the Release build and 368 tests passed with a clean tree; the release-mode ZIP was not built. The owner authorized fixing all items in one batch, without a live GUI check.

| Item | Finding | Disposition |
|---|---|---|
| B1 | The guard required `/_/` PDB paths only for first-party DLLs, so an executable whose PDB path was rewritten to `D:\Dev\GPU\...` passed with zero findings. | Fixed in `27810794e454903c4f8d95bfbb7248a3ba62cd3b`: every first-party PE, DLL or EXE, needs a `/_/` CodeView path. The only exception is an SDK apphost, recognized by content: its CodeView record (GUID, age, path) and its `.text` code must equal those of an SDK apphost template under the dotnet root (`sdk/*/AppHostTemplate`, the Windows x64 host pack) or the NuGet host packs. Here both templates share one signature, and both built executables match it (the build only adds a resource section). Packaging fails if no template is found, and without one every executable is rejected. Checks: the built CLI and GUI executables pass; an executable is rejected without a template, with its PDB path rewritten to `D:\Dev\GPU\app\...`, and with one code byte changed under the template's PDB record. The pre-fix guard (from `0671a37`) reported nothing for the `D:\Dev` case and the fixed guard reports it; a mutation that accepted every executable failed the checks. |
| S1 | The preview set `SelectionBrush` but not the selected-text brush, so selected text was not a guaranteed pair in high contrast. | Fixed in `5f186243500b1512abc6053df6ca7d396bbe2834`: `SelectionTextBrush` is OnAccent and `SelectionOpacity` 1 over the AccentFill `SelectionBrush` (highlight text on highlight in high contrast), and the GUI sets `Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering` to false, because WPF's default adorner selection draws a translucent overlay that ignores `SelectionTextBrush`. Tests read the brushes a `TextBox` gets from the real styles with each palette (set by the style; the palette pair; system highlight pair in high contrast; AA in dark and light) and check that the project and the built runtimeconfig set WPF's own switch name to false. A mutation removing the text brush failed all three palettes. |
| S2 | `Get-DepsRuntimeFiles` skipped `native` assets, which the worker closure reads. | Fixed in `99dca1ef9f5f7114935d12fa2e59bca5b80e7315`: native assets are read by file name like the worker closure, and packaging and its tests share one unpackaged-file check. A synthetic deps.json with runtime, native, runtimeTargets and resources assets shows that an unpackaged native library is reported, which fails packaging; a mutation skipping native assets failed it. The current CLI and GUI deps declare no native asset. |
| N1 | The `STATUS.md` next action still said Steps 5-6 needed authorization after Step 5 was done. | Corrected: Step 6 needs separate authorization. |

**Rescan and repackage.** With the fixed guard and the SDK apphost signature, the earlier `artifacts/WinGPUDoctor-0.1.0-dev-win-x64.zip` (SHA-256 `1f17c99c…`, built before these fixes and left in place) has 0 findings; without a template the guard reports both executables, as intended. `package-v0.1.ps1` gained `-OutputDirectory`, limited to folders inside `artifacts/` (`af72598f6679a5a06d48f68c9ba34f1213261d0e`), so a new package did not need to replace the earlier one. From `af72598` it built `artifacts/m7-step5-review-62f61ce24b164575bbd74c3870a12701/WinGPUDoctor-0.1.0-dev-win-x64.zip`: 871802 bytes, SHA-256 `0063304bf77faae75dbabcfff8c50407711ee5f71185d968c410f3e66428d210`, 30 entries, fixed guard clean in staging and ZIP, and the packaged GUI runtimeconfig carries the selection switch as false. The published v0.1.0 ZIP is unchanged.

**Validation and limits.** `./scripts/dev.ps1 -Action test` outside the sandbox after each fix: Release build 0 warnings and 0 errors, xUnit **372/372**, schema and helper checks, package layout **34/34**. No live GUI check was run: the non-adorner selection rendering and the new package were not exercised on screen, and the release-mode ZIP was again not built.
## M7 visual redesign, parts A-F — 2026-09-26

**Scope.** One owner-authorized batch: a visual redesign of the GUI following the owner's mockup (`artifacts/design/gui-mockup-v1.html`, ignored and never staged because it contains real hardware names), one local commit per part, then a live check in the dark and light Windows themes and a push of the backup branch `m7-desktop-gui` only. The adopted direction is recorded in `docs/GUI_PLAN.md` ("Visual design"). Only the Desktop project and its tests changed: no Host, CLI, Core, schema, privacy-rule or worker change, and no NuGet package.

| Part | Commit | Change |
|---|---|---|
| A | `b3ca3adba0fcedf52589b09e6d1a6f6845579e56` | Dark, light and high-contrast palettes with the same keys, chosen from the Windows settings and switched when Windows announces a change; shared styles; Windows fonts and Segoe Fluent Icons only |
| B | `a61ca07b9c55be4ccf4de60989363f227f9750de` | Two-column Welcome (direction B) with an idle Readout; `SplitPanel` stacks below 720 px; fixes a Part A key clash (see below) |
| C | `5b4b3e174557d60d803fa2ce077d9a34f6587547` | Scanning in the same layout: amber sweep, "usually a few seconds", Readout placeholders; animations only when visible and when Windows "Animation effects" is on |
| D | `31c543a21d13697bdcb4b3ed25a2efa75175c00f` | Results in direction C's layout with direction B's colors: summary sentence, main cards, one card per display path, one driver card per adapter, limits note, one technical-details switch |
| E | `4083922abcdae6039270a2190146ede270422897` | Two-column review-and-save screen: warning and cloud-sync hint always visible, field summary, exact-text panel with "N hidden (i)" |
| F | `d5563f5f73bc5a699a716fbb1c3cfdb3cce9088b` | "Almost done…" plus a plain reason line after a `Forced` cancel result; window 1040 × 720 fitted to the work area, minimum 520 × 460 |

**Unchanged behavior.** The scan state machine, single-use Cancel, the Requesting stop / Stopping / Stopping-to-close states and the bounded close are unchanged; F changes only the heading text after `Forced` and adds a detail line that promises neither a result nor a stop. The preview is still the exact writer output of the one retained report, and Save still writes that string through the Host rules. The (i) tip control keeps its keyboard and UI Automation behavior; only its look and tooltip style changed.

**Key-clash finding.** Part A keyed both the preview box style and a palette brush `Wgd.Preview`. Because the palette is added before the window is created, `StaticResource` returned the brush for the style and window creation failed. No deterministic test constructed the window, so Part A's commit passed the suite; the offscreen render check found it. Part B renamed the style `Wgd.PreviewBox` and added a test that keeps palette keys separate from all other keys and requires palette keys to be referenced only through `DynamicResource`.

**Deterministic validation.** After each part, `./scripts/dev.ps1 -Action test` outside the sandbox: Release build 0 warnings and 0 errors, all schema and helper checks passed. xUnit went from 352 to **360** (A), 362 (B), 363 (C), 364 (D, E) and **365/365** (F). New or changed tests:
- palettes define the same keys; dark uses the owner's colors; light darkens amber and cyan; WCAG AA for every text color on every surface (4.5:1) and for focus rings and badge icons (3:1) in dark and light; no color literals outside the palettes; only built-in Windows fonts and no bundled font file; palette choice (high contrast first, then the app mode, light when the value is missing); palette keys separate and dynamic-only;
- the two-column panel side by side at 1000 px and stacked at 600 px; animation only when visible and enabled; window size fitted to the work area, the minimum fitting 1366 × 768 at 150% scaling and stacking two-column pages;
- the new result structure, summary sentence, hidden and unavailable values, and a report with three display paths, source and target on different adapters, and an unresolved link that keeps the neutral wording;
- the save field summary as descriptions, the "N hidden" header and its tip; the busy detail while reading, after `Controlled` (none) and after `Forced` (no "result" or "report").

**Visual check.** Each screen was rendered offscreen from the Release build with the synthetic test fixtures (no live collection, no focus change) in the dark, light and high-contrast palettes, at 1040 px, 560-600 px and the 520 × 460 minimum. The renders stay in the session scratchpad and are not evidence of physical behavior.

**Limits.** The runtime reaction to a Windows "Animation effects" change relies on WPF refreshing `SystemParameters.ClientAreaAnimation`; only the rule and the startup value are tested. Whether the Fluent theme's own scroll-bar transitions follow that setting was not checked. High contrast was rendered only with the non-contrast system colors of this session. No screen reader or usability session was run, and the copy changes are the implementer's, not the independent Step 6 review. The live check follows.

**Live check (non-administrator, this laptop, 2026-09-26).** A UI Automation driver ran the Release `wingpudoctor-gui.exe` once per Windows app mode; the owner switched the mode in Windows Settings between the passes (the agent only read `AppsUseLightTheme`, which was `0` and then `1`; high contrast was off and "Animation effects" on). Each pass: Welcome, a scan to the result (also with technical details on and scrolled to the end), a second scan cancelled while reading, "Scan again", then the save screen and a save through the real Save dialog, and finally closing the window. Screenshots and the saved reports are in the ignored `artifacts/m7-visual-live-ea26bea87c5f4602bf6f120a1fa3e553/`; they contain real hardware names and are not committed.
- **Dark** (build of `daac37e`, code identical to F): background pixels `#171C22` (left column) and `#1E252D` (right panel), matching the dark palette; the cancelled scan showed "Scan stopped. No report was created." 295 ms after Cancel; the Markdown report (4459 bytes) was byte-identical to the preview without a BOM and showed "Report saved. WinGPUDoctor did not upload anything."; the window closed and no worker survived.
- **Light** (build of `1a76ffab4aa51a57aff785d6733d8c166d35f4af`): background pixels `#F3F5F8` and `#FFFFFF`, matching the light palette; stopped 313 ms after Cancel; the JSON report (9312 bytes) was byte-identical to the preview without a BOM, passed `Test-Json` against `schemas/report-0.2.0.schema.json` and `Get-M3PrivacyFailures` with 0 findings; exit `0` and no surviving worker.

**Findings during the live check.**
- The first light attempt could not find the JSON option by name: Part E had given both format radio buttons `AutomationProperties.LabeledBy` the "Format" heading, which replaces a button's own text, so both were exposed as "Format". Fixed in `1a76ffab4aa51a57aff785d6733d8c166d35f4af` (the options use their own text again; a test keeps every choice control named by its content, 366/366 tests), and the light pass was rerun in full on that build. The dark pass ran before the fix; the fix changes only those two accessibility names.
- In the dark pass the driver first tried to fill the Save dialog through UI Automation patterns, which this PowerShell client does not get for the dialog's controls, so the dialog stayed open. While it waited, the owner pressed Save manually, which saved `wingpudoctor-report.md` into the dialog's default folder (the ignored `artifacts/m7-review-fix-gui-export-0e0fe3c36edf461e8e265a387bbac67a/`); that file is byte-identical to the preview and was left in place. The driver then set the file name and pressed Save through the dialog's native controls, which produced the dark result above, and the light pass used the same method.

**Live limits.** The cancel stopped within about 0.3 s, so the intermediate "Stopping" screen was not captured live (it is covered deterministically). Not exercised live: a `Forced` cancel result, "Animation effects" turned off, a high-contrast theme, the minimum window size, several display paths or adapters on different paths, and a screen reader. One laptop and one display configuration only. Windows was left in light mode by the owner's change.

### Independent review (Codex) of `8854bde`..`ea95545` — 2026-09-26

**Result.** 0 blockers, 2 should-fix items, 0 nits. The reviewer confirmed that Host, CLI, Core, supervisor and worker code are unchanged; that the cancellation, bounded close and preview-to-save binding remain intact; that the result tests cover several display paths, different source and target adapters and an unresolved link; that no new health, driver-freshness, vendor or physical-port claim was added; and that no hardware name, absolute local path or `artifacts/` file was committed (documentation only names ignored artifact folders). The reviewer's full workflow outside the restricted environment passed 366 tests and all schema and helper checks; inside it, 10 worker-pipe tests failed because private pipe creation was denied, an environment limitation rather than a code finding. The owner authorized fixing both items in the same batch as Step 5.

| Item | Finding | Disposition |
|---|---|---|
| S1 | After a `Forced` cancel result the scan stays in `Scanning`, so the readout still said READING beside "Almost done…", although output commitment had won or collection had already closed after a failure. | Fixed in `eff5e235670896d4d384f9fea2b84bd7009326b1`: the readout status comes from `MainViewModel.ReadoutStatus` and says FINISHING after `Forced`, READING while a stop is only being requested, STOPPING for a controlled stop and IDLE otherwise. Tests assert every status on both `Forced` paths; a mutation that kept READING after `Forced` failed both. |
| S2 | The high-contrast palette used `HighlightColor` as accent text on `WindowColor` surfaces, which Windows does not guarantee as a contrast pair, and the contrast tests covered dark and light only. | Fixed in `8e0c779d1a8c6b5e2e16ed62b394d54510ae69a9`: `Wgd.Accent` (text and icons) and a new `Wgd.AccentFill` (primary buttons, checked boxes, text selection, progress glow) are separate; in high contrast the accent is `WindowTextColor` on `WindowColor` and the fill is `HighlightColor` under `HighlightTextColor`. A test maps every foreground/surface pair the UI uses to a Windows system pair (window, highlight, info) and fails otherwise (a mutation back to highlight text failed it); another keeps the accent keys in their text or fill role in XAML. Dark and light are visually unchanged. |

After both fixes `./scripts/dev.ps1 -Action test` passed outside the sandbox: Release build 0 warnings and 0 errors, xUnit **368/368**, schema and all helper checks. An active high-contrast theme was checked live in the Step 5 section above.

## M7 Steps 1-4 review follow-up, part B: copy trimming with (i) tips — 2026-09-26

**Scope.** Owner-authorized in the same batch as part A (committed locally as `6ccaf2c7cb767fb9dff905acc6577b84b4b2145a`). Secondary copy moves behind a new `InfoTip` control, an (i) button next to a field label or explanation title: "What this does not mean" and the optional next step for findings and warnings, the field glossary, the meaning of an unavailable state, the collector-status meanings, and how hidden values appear in the preview. The title and meaning of each finding and warning, the "What this can't tell you" card and all values stay on screen. The "More about this card" expander is gone; the "Show technical details" checkbox now sits directly on each card. On the save screen, "Review before sharing" and the cloud-sync hint stay visible in shorter form, a "What this report contains" summary (system, adapter and display-path counts, findings, report notes, reading steps and hidden fields, from the same retained report) sits above the exact text, and the exact-text box is smaller (200 instead of 300 pixels high) but still shows exactly what Save writes. `docs/GUI_PLAN.md` records the tip rule and the new hint wording. No NuGet package, CLI, Host, Core, schema or privacy change.

**Accessibility of the tips.** Each tip is a Tab stop. Its text appears on mouse hover, as soon as it receives keyboard focus, and on Enter, Space or click; Escape or leaving the tip closes it. `AutomationProperties.Name` is "About {field}" or "More about {title}", and `AutomationProperties.HelpText` is the tip text.

**Validation.** Tests were updated for the new structure: finding tips carry both labelled parts, warning explanations all have a tip, collector lines carry the status meaning, every display field has a glossary tip with an "About" name, unavailable values explain their state, and the save summary counts come from the retained report with the hidden-value hint in its tip. A new STA test checks that the tip is focusable and a Tab stop, exposes Name and HelpText, shows on keyboard focus and hosts the text in its tooltip. The copy-review, completeness and XAML-key tests cover the new strings. `./scripts/dev.ps1 -Action test` outside the sandbox: Release build 0 warnings and 0 errors; xUnit **348/348**, up from 347; schema and all helper checks passed. Live, non-administrator, on this laptop: UI Automation found 23 tips after a scan, all keyboard-focusable with a Name and HelpText; real Tab presses from the results area reached a tip on the second press and its tooltip was showing; Escape closed it and Enter reopened it; the save screen showed the summary and the hidden-fields tip. The live export regression passed again (Markdown and JSON equal to the preview, schema and privacy helper checks, existing-name refusal with the file unchanged).

**Limits.** No screen reader was run; announcement of Name and HelpText is inferred from the UIA properties. Many tips add many Tab stops on a long result page. Moving "What this does not mean" behind a tip makes those limits less prominent than before; the always-visible "What this can't tell you" card is unchanged. The copy change is the implementer's; the independent Step 6 copy review has not been done.

## M7 Steps 1-4 review follow-up, part A: lifecycle and copy fixes — 2026-09-26

**Review.** The Codex review of Steps 1-4 is recorded in the next section. The owner authorized fixing its findings, then a copy-trimming pass (part B), with one local commit per part. Part A groups the findings as:
- **A1** (Codex CS1). Closing the window during a scan waited for the controlled stop with no upper bound, so a scan that never ended would keep the window open indefinitely.
- **A2** (Codex CS2). `Cancel()` switched to "Stopping" before the request was sent and sent it later on the thread pool, so the screen could claim a stop that output commitment had already made impossible, and the close path did not guarantee the request was sent first.
- **A3** (Codex CS3-CS5 and CN1). Four strings over-reached: the unresolved-association value, `Reason.QueryFailed.Meaning` (blamed Windows), `Glossary.RefreshRate` (could be read as frames shown) and the Welcome read-only promise.

**Fixes.**
- **A1.** `RequestClose()` sends the cancellation request, then waits at most `MainViewModel.DefaultCloseWait` (75 s: the supervisor's 60 s overall budget in `CollectionTimingPolicy` plus margin). If the scan has not ended by then, the window may close and the process exits, leaving the worker Job's kill-on-close as the backstop. The busy text says the scan is stopping before WinGPUDoctor closes. The GUI has no friend access to Supervisor, so the bound is a GUI constant pinned by a test that reads the policy through the test project's existing access.
- **A2.** `Cancel()` calls `RequestCancellation()` synchronously on the calling (UI) thread. Only `Controlled` switches to "Stopping"; `Forced` (output already committed) keeps the normal path and shows the result. Cancel stays single-use in both cases. `RequestClose()` calls the same method before anything else.
- **A3.** New copy: "A link to a graphics adapter could not be established in this report"; "This information could not be read during the scan."; "The refresh rate Windows reports as configured for this display path. It does not measure frames shown or rendered."; "Scanning only reads information. It does not change settings, drivers or hardware." The copy-review pattern now also covers "rendered".

**Validation.** New or tightened tests: the cancellation token is already cancelled when `Cancel()` and `RequestClose()` return; a cancel after output commitment (report preparation held by a gated collector-run list) stays on the scanning screen, stays single-use and ends with the result; closing with a collector that ignores cancellation raises the close exactly at the bound on a manual clock (not 1 ms earlier), and the late snapshot is still discarded as cancelled; a close that stops normally closes once and is not repeated when the bound later elapses; the bound is at least the policy's overall budget plus 10 s. The fake collector now holds its controlled stop behind a gate so intermediate states are asserted without a race. `./scripts/dev.ps1 -Action test` outside the sandbox: Release build 0 warnings and 0 errors; xUnit **347/347**, up from 344; schema and all helper checks passed. The Desktop, Host and cancellation-handoff tests (48) then passed 15 consecutive repeated runs. A live non-administrator GUI smoke on this laptop again completed two scans in one process, one Cancel ending in "Scan stopped" (31 ms after the click) and one close during a scan (exit `0` after 127 ms), with no surviving worker.

**Limits.** The bounded-close fallback was exercised only deterministically; no live scan was forced to exceed the bound. Synchronous cancellation runs the token callbacks on the UI thread, as the CLI does on its console-handler thread; no UI stall was observed in the live smoke. These lifecycle changes were made by the implementer and have not been independently re-reviewed.

## M7 Steps 1-4 independent review (Codex) — 2026-09-26

**Reviewer and scope.** Codex reviewed the local M7 Steps 1-4 commits, `ce4d15bf0ecfee674e661e819b8b96eac6228a5a` (Host extraction) through `105c05caed014caf129c41a8d804cba32227f6bc` (save/export). The owner provided the review text; its file and line references are to the Step 4 commit. As the owner confirmed, the review also covered the Step 1 S1 disposal/cancellation fix from the earlier Step 1 review follow-up; it raised no finding against that fix. The review text does not report any test run.

**Result: 0 blockers, 5 should-fix, 1 nit.** All six were fixed in part A, commit `6ccaf2c7cb767fb9dff905acc6577b84b4b2145a`. The labels below are prefixed with "C" to keep them apart from the earlier Step 1 review's S1/S2 and N1-N3.

| ID | Finding | Disposition |
|---|---|---|
| CS1 (should-fix) | Window close had no bounded fallback. `MainWindow` cancelled the close and `MainViewModel` allowed it only after the scan task finished, so a collection or cleanup that never returned kept the window open and the Job's kill-on-close backstop was never reached. The close test used a collector that responds to cancellation. | Fixed in `6ccaf2c` (A1). Cancellation is requested first; the window may close after at most 75 s (supervisor overall budget plus margin). A test with a collector that ignores cancellation checks the bound on a manual clock. |
| CS2 (should-fix) | Cancel was queued after the UI said "Stopping". The state changed before `RequestCancellation()` was scheduled on the thread pool, so collection could win `HostScanSession`'s output commitment first, and a close could proceed without cancellation having been requested, weakening ADR 0008's close-during-scan contract. | Fixed in `6ccaf2c` (A2). The request is sent synchronously; only `Controlled` shows "Stopping", and `Forced` shows the result. Tests check that the token is cancelled when `Cancel()` and `RequestClose()` return, and cover a cancel after output commitment. |
| CS3 (should-fix) | An unresolved match was presented as a negative match: "Not linked to a graphics adapter listed in this report" was shown for every unavailable association, including ambiguous ones, while Core says only that an association was not established. | Fixed in `6ccaf2c` (A3): "A link to a graphics adapter could not be established in this report". |
| CS4 (should-fix) | The query-failure explanation blamed Windows, but the supervised collector also maps protocol failures and other caught failures to `QueryFailed`. | Fixed in `6ccaf2c` (A3): "This information could not be read during the scan." |
| CS5 (should-fix) | The refresh-rate help described actual image updates ("sets up a new image"), while Core reports configured path timing, not frames shown or rendered. | Fixed in `6ccaf2c` (A3): "The refresh rate Windows reports as configured for this display path. It does not measure frames shown or rendered." The copy-review pattern now also covers "rendered". |
| CN1 (nit) | The Welcome promise "changes nothing on this PC" was too broad because the app writes a user-chosen report; narrow it to the scan and graphics settings. | Fixed in `6ccaf2c` (A3) with the owner's wording: "Scanning only reads information. It does not change settings, drivers or hardware." |

**After the review.** Part B, commit `ca7f6e88aeb7790e54776e24d2af635571b9a4b9`, is an owner-requested copy-trimming pass, not a review finding. Codex then re-reviewed parts A and B; that re-review and its fixes follow directly below.

### Follow-up re-review (Codex) — 2026-09-26

**Reviewer and scope.** Codex re-reviewed the two follow-up commits, `6ccaf2c7cb767fb9dff905acc6577b84b4b2145a` (part A) and `ca7f6e88aeb7790e54776e24d2af635571b9a4b9` (part B), read-only; the review text states that it reran no tests and changed no files. The owner provided the text. **Result: 1 blocker, 1 should-fix, 0 nits.** Labels are prefixed "CR" to keep them apart from the review above.

| ID | Finding | Disposition |
|---|---|---|
| CR-B1 (blocker) | The 75 s close fallback was not bounded if cancellation itself blocked. `RequestClose()` called `Cancel()` before starting the timer, and `RequestCancellation()` reaches the synchronous `CancellationTokenSource.Cancel()` in `HostCancellationController`, so a token callback could hold the UI thread on Cancel or window close; the fallback could neither start nor fire. The close test covered a collector that ignores cancellation after the request returns, not a blocking callback. Requested: keep cancellation off the UI thread and start the close deadline independently, preserving the controlled-versus-committed result. | Fixed in `22a1a31d65b7754f1e4025ab424049b9b331de49`. `CancelAsync()` sends `RequestCancellation()` on the thread pool; the screen shows a neutral "Requesting stop…" and disables Cancel at once, then shows "Stopping" for `Controlled`, or "The scan had already finished reading. Showing the results…" for `Forced`, which then shows the result. `RequestClose()` starts the bounded deadline first and only then sends the request. This supersedes part A's synchronous request (A2). |
| CR-S1 (should-fix) | The new "What this report contains" summary claimed values that Core may mark unavailable or redacted ("each with name, PCI IDs and driver details", "adapter links"); the redacted fixture shows an adapter whose name is hidden. Describe these as report fields whose values may be unavailable, redacted or unresolved. The shortened review warning and cloud-sync hint were confirmed to remain visible. | Fixed in `2f15e341031f68eae61ba02d95360c02581d5e33`. The summary now names fields ("{n} entries, with fields for name, PCI IDs and driver details", and so on) under a note that any value may be unavailable, hidden by the privacy filter or unresolved, with the exact text showing each one. |

**Also fixed in this round (owner request, not a review finding).** Commit `a0d892e2e282887168a96d0ef438f7a9aecdf484`: an (i) tip opened whenever keyboard focus arrived after keyboard input, so focus restored by Alt+Tab back into the window popped it open. It now opens on focus only when Tab moved focus from another element in the window, or on Enter, Space or click. WPF's own keyboard-focus tooltip opening is turned off for the tip. This supersedes part B's "shows on keyboard focus" behavior.

**Validation.**
- **CR-B1.** A new deterministic test registers a token callback that blocks the cancelling thread: `RequestClose()` returns while the callback is still blocked, the screen shows "Requesting stop…" with Cancel disabled, the close deadline fires on a manual clock although the request has not returned, and after the callback is released the `Controlled` result is still shown as a controlled stop ending in Stopped, with one close. The existing cancel-after-output-commitment test now checks the `Forced` text and the result. A temporary mutation restoring the synchronous request before the deadline made the new test fail at its 10 s wait; the fix was then restored and matched its saved SHA-256. xUnit **349/349**; the Desktop, Host and cancellation-handoff test classes passed 15 consecutive repeated runs; a live non-administrator GUI smoke passed (two scans in one process, one Cancel ending in "Scan stopped" 37 ms after the click, one close during a scan exiting `0` after 142 ms, no surviving worker).
- **CR-S1.** A new test uses the redacted fixture (adapter name hidden) to check that the summary names fields, repeats no value and shows the note. xUnit **350/350**.
- **Tip reactivation.** A test covers the focus rule (Tab opens; focus from no element, as after Alt+Tab, and mouse focus do not), and the STA test checks that WPF's keyboard-focus opening is off. xUnit **351/351**. A live check with real key presses on this laptop (Tab onto a tip, Alt+Tab away and back, Enter) passed twice on the final build: the tip opened on Tab, closed while away, stayed closed when focus returned to it, and opened on Enter. On a temporary build restoring the previous rule, one run reproduced the popup on reactivation; in the other, Alt+Tab did not return focus to the tip, so that run was inconclusive. The fix was restored and matched its saved SHA-256.
- Every commit in this round followed a full `./scripts/dev.ps1 -Action test` outside the sandbox (Release build 0 warnings and 0 errors; schema and all helper checks passed). One combined verification run for the tip fix was interrupted by the owner before completing and was repeated in full.

**Limits.** No live scan was made to block a cancellation callback or exceed the close bound; both remain deterministic-only. The Alt+Tab check depends on the desktop's window order. No screen reader was run. These fixes were made by the implementer; Codex re-reviewed them next.

### Second follow-up re-review (Codex) — 2026-09-26

**Reviewer and scope.** Codex re-reviewed `22a1a31`, `2f15e34`, `a0d892e` and `d7b1366` read-only; it inspected the recorded validation but reran no tests. The owner provided the text. **Result: 0 blockers, 1 should-fix, 1 nit.** Codex confirmed that `RequestClose()` starts the 75 s deadline before `CancelAsync()` sends cancellation off the UI thread, so a blocked token callback cannot prevent the close fallback (CR-B1 closed), and that the save summary now describes fields whose values may be unavailable, hidden or unresolved (CR-S1 closed).

| ID | Finding | Disposition |
|---|---|---|
| CR2-S1 (should-fix) | `Forced` does not always mean a report is coming: `HostCancellationController` also returns `Forced` when collection has closed after a failure, yet `MainViewModel` then showed "The scan had already finished reading. Showing the results…" before it could enter NeedsRestart. Use neutral finishing text until a completed report exists. The Controlled path still suppresses the report, and the output-committed path retains it. | Fixed in the local commit that records this re-review. After `Forced`, the busy text is the neutral "Finishing up…"; only the scan's own outcome decides between Result and NeedsRestart, so a `CollectionFailed` outcome (or a failure after output commitment) goes straight to NeedsRestart. |
| CR2-N1 (nit) | The (i) rule checks for keyboard input and a previously focused element; it does not check that Tab caused the focus change, so the code and its test support a broader rule than "Tab only", and the GUI plan wording was broader still. | Clarified without a behavior change: the `InfoTip` comments, the test name (`InfoTipOpensOnKeyboardFocusChangeFromAnotherElement`) and `docs/GUI_PLAN.md` now describe a focus change caused by keyboard input from another element in the window (Tab, Shift+Tab or other keyboard navigation; the key is not identified), excluding focus restored on window reactivation. |

**Validation.** A new test holds report preparation after output commitment, cancels (the result is `Forced`), checks that the busy text is the neutral "Finishing up…" and mentions no result, then fails the preparation: the view model ends in NeedsRestart with no report. The existing test for the output-committed path still ends with the result, and `CancellationHandoffTests.FailedCollectionClosesLifecycleWithoutOutput` already shows that a request after a failed collection returns `Forced`. The exact interleaving in which a failed collection closes between the request and the outcome is not forced deterministically; the view model's handling of any `Forced` result is the same. `./scripts/dev.ps1 -Action test` outside the sandbox: Release build 0 warnings and 0 errors; xUnit **352/352**; schema and all helper checks passed. No live GUI key-press check was run for this round, as agreed with the owner.

## M7 Step 4 save and export — 2026-09-26

**Scope and state.** Authorized in the same batch; Step 3 was committed locally as `570a3c421842fcd7f3132b9484a0b609d35f7118`. Step 4 adds a WPF-free `SaveViewModel` and a "Review and save the report" panel reached from the result screen. The preview is the exact `ReportWriter.Markdown` or `ReportWriter.Json` output of the scan's one retained `ShareableReport`; changing the format regenerates the preview and clears any earlier save message. Save writes exactly the previewed string through `HostExport.ResolveDestination` and `HostExport.TryWriteNew` (local fixed drives only, UNC/device/ADS rejected, UTF-8 without BOM, `CreateNew`). The WPF Save dialog only picks a path and does not offer to overwrite; an existing file is reported as "already exists" and left unchanged. The panel shows the `ReviewBeforeSharing` explanation, the count of fields hidden by the privacy filter with how hidden values appear, the cloud-sync folder hint, and on success "Report saved. WinGPUDoctor did not upload anything." Messages never include the path or exception text. A new scan discards the report and its preview before collection starts. Copying or dragging text out of the preview is blocked, following the plan's exclusion of clipboard copy. `PRIVACY.md` describes this behavior. No NuGet package, CLI, Host, schema, privacy or rule change: `git diff ce4d15b -- src/WinGPUDoctor.Cli src/WinGPUDoctor.Host` is empty.

**Deterministic validation.** 4 new tests: the preview equals the writer output of the retained report in both formats, with the redacted count and review warning; saved bytes equal the previewed string (UTF-8 without BOM) for Markdown and, after a format change, for JSON, matching the writer output; an existing file is not overwritten, and UNC, alternate-stream and invalid names are rejected without writing or showing the path; a new scan discards the report and preview and disables Save until the next result. The copy review, catalog and XAML key tests cover the new strings. `./scripts/dev.ps1 -Action test` was run outside the sandbox: Release build 0 warnings and 0 errors; xUnit **344/344** (0 failed, 0 skipped), up from 340; schema and all helper checks passed.

**Live validation (non-administrator, this laptop).** A UI Automation driver scanned, opened the save panel and saved through the real Windows Save dialog into an ignored `artifacts/` folder. The Markdown and JSON files were each byte-for-byte equal to the on-screen preview, without a BOM, and each showed the saved message; switching to JSON changed the preview and cleared the earlier message. The saved JSON passed `Test-Json` against `schemas/report-0.2.0.schema.json` and the existing `Get-M3PrivacyFailures` helper with 0 findings. Saving again to the existing JSON name showed the "already exists" message and left the file hash unchanged. Only the two chosen files were created in that folder. On the final build, the Step 2 smoke was repeated with the same results (two scans, one cancel ending in "Scan stopped", close during a scan with exit `0`, no surviving worker), and the non-live CLI captures (`--help` and four argument errors) were still byte-identical to the pre-follow-up baseline. The private reports and captures remain in ignored or temporary locations.

**Limits.** One laptop, one display configuration, dark theme; the missing-runtime experience, packaging, a cloud-synced or removable destination through the dialog, no-network and no-other-file observation at the system level, accessibility walkthroughs and usability checks are Step 5-6 scope and have not been done. The independent reviews required by `AGENTS.md` for Steps 2-4 (lifecycle, privacy/export boundary and UI copy) have not been performed.

## M7 Step 3 explanation catalog and detail views — 2026-09-26

**Scope and state.** Authorized in the same batch; Step 2 was committed locally as `f90da1fa4d8eb23389072c97c99a40e9f49d1dcb`. Step 3 adds the WPF-free `ExplanationCatalog`, keyed by the 6 finding IDs and by every `WarningCode`, `DataState`, `ReasonCode`, `CollectorStatus` and `DataSource` value, plus a glossary for the 9 report fields a beginner meets on the cards. All copy is in `Resources/Strings.resx`. Findings, warnings and states carry a title, a meaning and a "What this does not mean" line; reasons and collector statuses carry a title and meaning; a few entries have an informational next step (save a report and send it to a helper, or read the preview), never a settings or driver instruction. The result screen gains three cards (findings with the display path they refer to, report notes for the warnings, and "How the scan went" for the collector runs), and every card has an expandable "More about this card" section with the glossary text and a "Show technical details" toggle. Technical details show exact values (for example the rational path and signal rates), the field state, source and reason, Core's own finding messages with evidence paths, Core's warning text and the collector run metadata. An unknown future finding ID would fall back to Core's own message rather than invented copy. No NuGet package, CLI, schema, privacy or rule change.

**Deterministic validation.** 8 new tests. Completeness: every enum value above has its required text; the finding IDs compiled into Core (read from the Core assembly's string literals) equal the catalog's list, and fixtures covering all six rule branches produce only catalogued IDs; the connector tokens in Core's output-technology allowlist (also read from Core's literals, 20 tokens) all have friendly text; every `views:Text` key in `MainWindow.xaml` exists. Copy review: a reviewed pattern list rejects vendor names, Windows edition names, install/download/driver-update and settings wording in any resource string, and allows diagnosis, rendering, hybrid, power-state and physical-connection terms only in a sentence with a negation or in the "What this can't tell you" items. Behavior: findings show catalog titles with the display path they refer to, plus warning explanations, collector-run wording with icons, the no-findings note, glossary help, exact rates and reason text in provenance. The earlier "Something went wrong" error copy was reworded because the review rule flagged it. `./scripts/dev.ps1 -Action test` was run outside the sandbox: Release build 0 warnings and 0 errors; xUnit **340/340** (0 failed, 0 skipped), up from 332; schema and all helper checks passed.

**Live check.** On the same laptop, as a non-administrator, a UI Automation driver ran one scan, expanded all 7 detail sections, turned on the technical details and captured the window at several scroll positions for a local visual check. No report content was recorded in the repository.

**Limits.** The copy review is a pattern list and a self-review by the implementer, not the independent privacy/semantics copy review planned for Step 6. No screen-reader, keyboard-only, high-contrast or scaling walkthrough was done.

## M7 Step 2 vertical slice — 2026-09-26

**Scope and state.** The owner authorized Steps 1-4 in one batch, with one local commit per step after the full test workflow passes; push, tag and release are not authorized. Step 1 was committed locally as `ce4d15bf0ecfee674e661e819b8b96eac6228a5a`. Step 2 adds the WPF project `src/WinGPUDoctor.Desktop` (assembly `wingpudoctor-gui`, framework-dependent `net10.0-windows`, `asInvoker` with per-monitor DPI awareness): Welcome, Scan, summary cards and a single-use Cancel. It references Host, Supervisor and Windows, and copies `worker/` with the CLI's existing `copy-worker-deployment.ps1`. The built-in Fluent theme is applied with `ThemeMode.System`. SDK 10.0.401 still flags this API as experimental (`WPF0001`; confirmed by building without the suppression), so one scoped `#pragma` is used, as ADR 0008 D1 allows. No NuGet package was added: the new lock file lists project references and the existing transitive `System.Management` 10.0.12 only, and the test lock file adds only the Desktop project. All UI copy is in `Resources/Strings.resx`. The CLI, schema, privacy projection and rules are unchanged; there is no export yet.

**Design.**
- **D3.** `ReportDocument` retains the one `ShareableReport` of a completed scan. The cards display only the `DiagnosticReport` deserialized from `ReportWriter.Json` with `ReportWriter.JsonOptions`.
- **Lifecycle.** The WPF-free `MainViewModel` has the states Welcome, Scanning, Cancelling, Result, Stopped and NeedsRestart. Each scan runs a fresh `HostScanSession` on the thread pool, and a new scan discards the previous result before it starts. Scan is disabled while busy. Cancel calls `RequestCancellation()` once and never escalates. `CollectionFailed`, and any exception escaping `RunAsync` (review N2), leads to a fixed restart-required message without exception text and with no retry. Closing the window during a scan requests controlled cancellation and closes after the scan has stopped; the worker Job remains the backstop.
- **Copy.** Cards show reported facts only: PCI vendor ID as reported hex, display source/target adapters named only for exact matches (otherwise "Not linked to a graphics adapter listed in this report"), unavailable states as icon plus friendly text, and a "What this can't tell you" card. The summary uses neutral counts and no health wording.

**Deterministic validation.** 15 new tests: the D3 round-trip (re-serializing the displayed model reproduces the exact writer output) for 7 fixtures covering single, multiple and empty inventory, topology with exact and unmatched associations, redaction and incomplete collection; no Desktop type holds a `CollectionSnapshot`; completed-scan cards and summary values; friendly unavailable, unresolved and incomplete wording; single-use Cancel ending in Stopped with no report; restart-required without retry; outermost handling of a report-preparation exception; a fresh session per scan with the previous report discarded; and close-during-scan requesting controlled cancellation first. `./scripts/dev.ps1 -Action test` was run outside the sandbox: locked restore and Release build passed with 0 warnings and 0 errors; xUnit passed **332/332** (0 failed, 0 skipped), up from 317; schema checks and the M3 helper (50), M4 integrated schema (8), admission (1), deployment (10), execution-fingerprint (8) and process-evidence (6) checks passed.

**Live GUI smoke.** An ad-hoc UI Automation driver (kept outside the repository; a tracked GUI check belongs to Step 6) ran the Release `wingpudoctor-gui.exe` as a non-administrator on the same laptop and display configuration. In one GUI process, two consecutive scans completed (about 4.0 s and 2.5 s to the result screen), showing the headline, "This PC", two adapter cards, one display-path card and the limits card. This is the first live evidence of same-host admission reuse across scans in one process. A third scan was cancelled with Cancel and showed "Scan stopped. No report was created." A fourth was interrupted by closing the window: the process exited with code `0` about 0.2 s after the close request. No `wingpudoctor-worker` process was running after any scan or after exit. A local screen capture confirmed the Fluent layout; the capture and smoke evidence remain in ignored or temporary locations, and no report content was recorded.

**Limits.** One laptop, one display configuration, dark theme only; no high-contrast, 150% scaling, keyboard-only or screen-reader walkthrough; the cancel and close were each exercised once and not at controlled operation stages; no packaging, missing-runtime or usability check. The explanation catalog, detail views and export follow in Steps 3-4.

## M7 Step 1 independent review and follow-up — 2026-09-26

**Review.** An independent read-only review of the uncommitted Step 1 implementation (Claude in Cowork; code inspection only, no tests run) reported 0 blockers, 2 should-fix items and 3 nits:

- **S1 (should-fix).** Disposing `HostScanSession` while `RunAsync` was still collecting disposed the controller. Later cancellation requests returned `Forced` without effect, and collection continued silently until it ended.
- **S2 (should-fix).** No live cancellation check had been run after the extraction.
- **N1 (nit).** `HostScanSession.Cancellation` exposed the whole controller publicly, including `TryCommitOutput`.
- **N2 (nit).** An exception from `PrivacyPolicy.Prepare` propagates out of `RunAsync`, as it did from the original CLI output delegate. Accepted unchanged; a GUI host will need outermost error handling around the scan.
- **N3 (nit).** The Host test assertions that a field is redacted exercise the privacy rules rather than Host itself. Naming only; accepted unchanged.

The owner authorized fixing S1 and N1 and running S2. N2 and N3 are recorded without code changes.

**S1 fix.** `HostScanSession` now tracks an atomic lifecycle: idle, collecting, collection ended, dispose pending and disposed. `Dispose()` during collection requests controlled cancellation through the controller's existing `Interrupt()` path and returns without waiting. The controller and its token source are disposed only after collection has ended and the collection-ended callback has run, so collector cleanup can still observe the cancelled token. If output commitment has already won, the dispose-time request is `Forced`, the winner is unchanged and the scan completes normally. Disposal before a scan starts disposes the controller, and a later `RunAsync` throws `ObjectDisposedException` without invoking the collector. Repeated disposal is a no-op. This follows ADR 0008: closing the window during a scan requests controlled cancellation first.

**N1 fix.** The only public cancellation member is now `RequestCancellation()`, which returns `HostInterruptResult` with the existing controller semantics. The controller accessor is internal, and Host grants `InternalsVisibleTo("WinGPUDoctor.Tests")`, the same test-only pattern used by Protocol, Supervisor, Windows and Worker. No friend access was granted to Host or a GUI, so ADR 0008's seam rule is unchanged; this supersedes the Step 1 section's statement that Host had no `InternalsVisibleTo`. The CLI console handler and the race tests that model it now call `RequestCancellation()`.

**Deterministic validation.** Four Host tests were added: the public cancellation surface with `Controlled`-then-`Forced` sequencing; disposal during collection (outcome `Cancelled`, token source still usable during collector cleanup, controller released after collection ends, later request `Forced`); disposal after output commitment (`Completed`, no hidden cancellation); and disposal before a run (`ObjectDisposedException`, collector not invoked). A temporary mutation that restored the pre-fix `Dispose()` made the in-flight and before-run disposal tests fail (the in-flight case at its 10 s timeout) in a filtered Debug run of the Host tests; the fixed file was then restored and matched its saved SHA-256. `./scripts/dev.ps1 -Action test` was run outside the sandbox. The Release build passed with 0 warnings and 0 errors; xUnit passed **317/317** (0 failed, 0 skipped), up from 313. Synthetic schema checks passed, as did the M3 helper (50), M4 integrated schema (8), admission (1), deployment (10), execution-fingerprint (8) and process-evidence (6) checks.

**CLI compatibility.** Non-live Release CLI captures of stdout, stderr and exit code were taken from the Step 1 build before this follow-up and from the rebuilt CLI after it, under ignored `artifacts/m7-step1-review/`. All 15 files were byte-identical: `--help` (exit 0) and four argument errors (`--format xml`, `--yes` without `--output`, empty `--output` and an unknown option; exit 2 each). The `--help` capture also matches the pre-Step-1 capture under `artifacts/m7-step1/before/`. Live preview and export-decline captures were not repeated. The CLI change is limited to the console handler's call target, and no output text changed.

**Live cancellation (S2).** On 2026-09-26 the owner-authorized `./scripts/test-m4-cancel.ps1` (default `Cancel` mode, one run) ran outside the sandbox as a non-administrator against the rebuilt Release CLI, on the same laptop and display configuration: **PASS**. Readiness was the validated attempt 1 of `wmi.displayDrivers`, and a real `CTRL_C_EVENT` was delivered. The CLI exited `3`, observed 33.3 ms after the signal call. Exactly one cancellation was observed and no worker launched after the signal. All four started workers had confirmed cleanup and had exited, with no survivor. No report file or preview was produced, stdout was empty and the cancellation notice was present. The non-probe stderr bytes, the notice with CRLF, matched two earlier recorded live cancellation runs from 2026-09-24. Private evidence remains under ignored `artifacts/`.

**Limits.** This is one live run on one laptop and its current configuration. The second forced interrupt remains deterministic-only. No GUI was exercised. Disposal during collection is covered only deterministically, because the CLI disposes its session only after `RunAsync` returns. The S1 fix is a cancellation/lifecycle change made by the implementer (Claude Code), so a focused independent re-review is still required before any checkpoint. Nothing was staged, committed or pushed.

## M7 Step 1 host extraction — 2026-09-25

**Scope and state.** Starting from clean local `main` at `5357e1bbbcfa1a44b95d95d2d967bcc1fa7f5d9a`, the owner-authorized Step 1 added a UI-free Host project and moved the CLI's scan handoff and export destination/write rules into it. The collector remains injected by the CLI, with its existing private progress probe. The test project now references Host instead of linking `CollectionOutput.cs`, and all existing cancellation race tests remain. No GUI, new NuGet package, `InternalsVisibleTo` for Host or protocol/schema change was added. Nothing was staged, committed or pushed. The published v0.1.0 package is unchanged.

**Deterministic validation.** `./scripts/dev.ps1 -Action test` was run outside the sandbox. Locked restore and Release build passed with 0 warnings and 0 errors; xUnit passed **313/313** (0 failed, 0 skipped), up from 305 before Step 1. Synthetic schema checks passed, as did the M3 helper (50), M4 integrated schema (8), admission (1), deployment (10), execution-fingerprint (8), and process-evidence (6) checks. The new Host tests cover privacy-projected completion, distinct cancellation/failure outcomes, fresh one-use controllers, destination rejection and UTF-8 `CreateNew` no-overwrite behavior. `git diff --check` passed after code changes. The lock-file changes contain only the new Host project references.

**CLI compatibility comparison.** Release CLI output was captured before and after Step 1 under ignored `artifacts/m7-step1/` on the same laptop and display configuration. SHA-256 comparisons of stdout, stderr and exit-code files were byte-identical for each case: `--help` (exit 0), default live preview (exit 0), redirected-input export decline (exit 4), and an interactive `NO` response to the export prompt (exit 4). The declined destinations were not created. Preview and decline cases invoked live read-only collection; no report was exported. The private report content remains in ignored local evidence.

**Limits.** Physical comparison covers one laptop and its current configuration. This step did not exercise a GUI, repeated scans in one host process, a live Ctrl+C race after extraction, an authorized export, or cross-machine hardware behavior. Cancellation/outcome and write rules were checked deterministically; the independent implementation review required for cancellation/lifecycle and security boundaries remains pending.

## v0.1.0 publication — 2026-09-25

**Release batch.** The owner ran one authorized PowerShell 7.6.6 batch with GitHub CLI 2.101.0 on the same laptop. It started from a clean `main` in sync with GitHub at the P1.3 evidence commit `4d63b2b1dec122dfde897c62c646f755667124b8`, whose hosted `deterministic-tests` run #9 (run `36109727352`) passed **305/305** tests and schema verification with no uploaded artifact; the only annotation was the known Node.js 20 deprecation notice. The batch would have stopped at the first failed gate; every gate passed.

| Step | Result |
|---|---|
| Preflight | `main` clean and in sync at `4d63b2b…`, whose parent is S2 and which differs from S2 only in `STATUS.md` and `docs/VALIDATION.md`. No `v0.1.0` tag existed locally or on GitHub, and no Release or draft existed. The local ZIP and `.sha256` matched the validated values. |
| Tag | Annotated tag `v0.1.0`, tag object `0a3da645f624636dce1b5707a79abe3a0fbf987c`, created at S2 `62e25b7b12d04499f7b39bae417418d33cf297ee` from a reviewed tag message and verified locally (type `tag`, object S2, type `commit`) before pushing. Only `refs/tags/v0.1.0` was pushed. GitHub reported the same tag object, peeling to S2. |
| Draft | "WinGPUDoctor v0.1.0" was created as a draft from the existing tag (`--verify-tag`) with exactly two uploaded assets. Both were downloaded back and matched the validated ZIP (SHA-256 and size) and checksum file before publication. |
| Publication | Published at 2026-09-25 11:48:14 UTC (Release ID `396559408`): public, marked latest, not a draft, not a pre-release, and the repository's only Release. |
| Public verification | Unauthenticated downloads of both public assets matched the validated ZIP and checksum file. The public API listed exactly one Release, with `latest` resolving to `v0.1.0`. The tag still peeled to S2, `main` stayed at `4d63b2b…` with a clean tree, and the local ZIP and checksum were unchanged. |

**Published assets.**

| Uploaded asset | Size | SHA-256 |
|---|---|---|
| `WinGPUDoctor-0.1.0-win-x64.zip` | 734729 bytes | `65162553242aea18e5b1cebe963b769de76ebd7f7729d360ae151fa984ce63a8` |
| `WinGPUDoctor-0.1.0-win-x64.zip.sha256` | 97 bytes | `71bdd25767ca69caada706b4182f6aed7e623fa3f8909fba485b044dfa9bf71c` |

These are the only uploaded assets: the S2 release candidate validated in the next section and its validated checksum file. GitHub's reported asset digests match these values. The "Source code" zip and tar.gz links are archives GitHub generates automatically from the tag; they are not uploaded assets.

**Independent verification.** A separate review, outside the owner's machine, downloaded both public assets without authentication and re-hashed them; both matched the table. Through the public API it also confirmed that `v0.1.0` is the only tag and that its tag object peels to S2, that the Release is the only one, public, latest and not a draft or pre-release, and that `main` was still at `4d63b2b…`.

**Notes.**

- The v0.1.0 binaries are not code signed. The published Release notes disclose this, warn that SmartScreen may prompt, give the `Get-FileHash` command and expected SHA-256, and state that a matching checksum shows the file is identical to the published one, not who published it or that it is free of malware.
- GitHub reports the Release `target_commitish` as `main` because the Release was created from an existing tag; the tag determines the release source, S2. GitHub immutable releases were not enabled for this Release.
- The pre-fix `1a319aa6…` ZIP and the historical M6 ZIPs `cdefbb10…` and `0328525a…` were not uploaded or published and remain preserved as local evidence only. The live report and other local evidence remain ignored and unpublished.
- This publication record is a docs-only commit after S2 and is not part of the release source. The tag and the published assets were not changed after publication.

## P1.3 release-candidate and live validation from S2 — 2026-09-25

**Release source.** S2 is `62e25b7b12d04499f7b39bae417418d33cf297ee` (`fix: map Release source paths to /_/ and guard packaged paths`, parent `bdefbac75dba0e5f65af59c67677e69ad4c7fb27`). It changes exactly `Directory.Build.props`, `AGENTS.md`, `scripts/package-v0.1.ps1` and the new `scripts/package-path-guard.ps1`; the next section describes them. Hosted GitHub Actions `deterministic-tests` run #8 (run `36105656989`, job `107977473007`) checked out that exact SHA and succeeded in 1 m 27 s: locked restore; Release build with **0 warnings / 0 errors**; standard-user tests **305 passed / 0 failed / 0 skipped**; `./scripts/verify-schema.ps1` PASS; **0** uploaded artifacts. The reviewed log showed runner paths only and masked tokens, and did not print the test-account password. The only annotation was the known Node.js 20 deprecation notice. The owner froze S2 as the `v0.1.0` tag target; the tag was later created there (see the publication section above).

**Environment.** The checks were owner-run PowerShell 7.6.6 batches, kept with the ignored local evidence, on the same Windows 11 x64 laptop (build 26200) with the portable .NET SDK 10.0.401 and the checkout at S2. `HEAD`, local `origin/main` and GitHub `main` were S2, and the tracked tree and index were clean before and after each batch.

**Release-candidate validation (non-live).**

| Gate | Result |
|---|---|
| Final vulnerability audit | `./scripts/dev.ps1 -Action audit` (locked restore with NuGet audit) reported no `NU19xx` diagnostics. `dotnet list WinGPUDoctor.slnx package --vulnerable --include-transitive --format json` reported **7 projects, 0 vulnerable direct or transitive packages, 0 problems** and empty stderr. All 7 lock files matched S2 before and after. |
| Clean outputs | Only the 7 projects' 14 `bin`/`obj` directories were deleted, by explicit path. `.tools`, `artifacts` and the preserved ZIPs were untouched. |
| Package | `scripts/package-v0.1.ps1` built Release with **0 warnings / 0 errors**, and its path guard passed on the staging folder and on the final ZIP. |
| Release candidate | `WinGPUDoctor-0.1.0-win-x64.zip`, **734729 bytes**, SHA-256 `65162553242aea18e5b1cebe963b769de76ebd7f7729d360ae151fa984ce63a8`. The `.sha256` file matches exactly. The ZIP has **25** files, **0** directory entries, a single `WinGPUDoctor-0.1.0-win-x64/` root and **9** Worker files. `README.md`, `SECURITY.md`, `PRIVACY.md`, `LICENSE` and `THIRD-PARTY-NOTICES.md` are byte-identical to S2 (Git blob IDs). |
| Path guard, independent rerun | **0** findings on the staging folder and **0** on the final ZIP. All 9 first-party DLL entries (5 parent, 4 Worker) have exactly one CodeView record rooted at `/_/src/…`. No embedded PDB, local checkout path, user-profile path or `X:\Users\` path is present. |
| Comparison with the pre-fix `1a319aa6…` package | Same 25 entry names. The 16 non-first-party entries are byte-identical; exactly the 9 first-party DLLs differ. |
| First-party DLL structure (Gate 6b) | PASS under the corrected rule below. |
| Step 4a, fresh extraction outside source, build and staging folders | All 25 extracted hashes match the ZIP. `--help` exits `0`, identifies 0.1.0 and contains the clarified exit-code line. `--format xml`, unscoped `--yes` and a UNC `--output` each exit `2` before collection. `wingpudoctor.exe` and all 9 first-party DLLs have ProductVersion 0.1.0 and FileVersion 0.1.0.0. Both runtimeconfig files target `Microsoft.NETCore.App` 10.0.0 (`net10.0`). The execution fingerprint has 21 inputs (10 parent, 9 Worker, host and runtime), with hashes matching the ZIP; the runtime was 10.0.7. Authenticode is `NotSigned` for the exe and the 9 first-party DLLs (unsigned v0.1.0 build) and `Valid` for the 6 Microsoft-signed dependency DLLs. |
| Final state | ZIP hash and size, checksum and the three preserved evidence folders (contents, hashes, checksum files, last-write times) unchanged. `HEAD` and local `origin/main` S2; tracked tree and index clean. |

**Gate 6b rule correction.** The first release-candidate run stopped at Gate 6b. That rule expected the Supervisor's regenerated Worker manifest to differ only in SHA-256 and MVID strings. The manifest compiled into `WinGPUDoctor.Supervisor.dll` also pins each Worker file's length. The shorter `/_/` debug path reduced `wingpudoctor-worker.dll` by one 512-byte file-alignment unit (24576 to 24064 bytes), which changed one IL constant. The owner classified this as a validation-rule defect, not a release-candidate defect. A read-only re-check against the unchanged ZIP, with no rebuild or repackaging, applied the corrected rule and passed:

- Eight DLLs have identical IL, metadata tables, `#Strings`/`#Blob`, user strings, CLR/PE flags and Win32 resources. They differ only in debug data (CodeView `/_/…`, PDB ID and checksum), MVID, timestamp and layout offsets.
- The Supervisor differs additionally only in 1 IL byte inside the Worker file-length pin, and in 8 user strings that are the regenerated SHA-256 and MVID pins of the 4 rebuilt Worker DLLs.
- All 9 Worker pins (path, length, SHA-256, assembly name and version, MVID) match the final `worker/` files exactly, and the pre-fix pins match the pre-fix files.

No product-code change was found.

**Live validation (Batch 4).** This owner-authorized session ran on the same laptop with its single active internal display path, from a non-administrator, Medium-integrity token. It used the packaged executable from a fresh temporary extraction outside source, build and staging folders, and all 25 extracted hashes matched the ZIP. No hardware, display, driver, MUX or power setting was changed.

| Check | Result |
|---|---|
| `verify-cli.ps1 -CliExecutable <extracted exe>` | 7/7: help; invalid format, unscoped `--yes` and UNC destination rejected before collection (exit `2`); JSON preview passes schema and creates no file; redirected-input export refusal (exit `4`); existing-file refusal (exit `5`, synthetic file unchanged) |
| Direct `--help` and JSON preview | `--help` exits `0` and identifies 0.1.0; the preview exits `0`, passes schema 0.2.0 and creates no file (output not stored) |
| Explicit `--format json --output <ignored local path> --yes` | exit `0`; stdout empty; stderr has the preview and `Report saved locally. Nothing was uploaded.`; **9312-byte** report |
| Report checks | Schema 0.2.0 PASS; tool/schema/privacy 0.1.0 / 0.2.0 / 0.2; 0 redacted fields; `Get-M3PrivacyFailures` 0 and `Get-M3ReportFailures` 0. All 5 collectors succeeded. 2 GPU entries and 1 active path are available, with one non-blocking `targetName`/`missingValue` issue. Findings are `inventory.multiple-adapters` and `topology.endpoint-adapter-association`, with all 5 evidence paths resolved. Warnings are `inventoryOnly`, `providerReportedValues`, `topologyIsNotRendering` and `reviewBeforeSharing`. |
| `test-m4-cancel.ps1 -Mode Cancel -Runs 1 -CliExecutable <extracted exe>` | First Ctrl+C delivered and observed; exit `3`; no later Worker launch, report or preview; empty stdout; cancellation notice present. **4** tracked Workers cleanup-confirmed and `Exited`, **0** survived, 0 harness failures. Signal call to observed exit 33.8 ms, which includes delivery and observation overhead and is not a guarantee. |
| Final state | ZIP, checksum and preserved ZIPs unchanged; `HEAD` S2; tracked tree and index clean; report and probe records only in ignored `artifacts/` |

These results match the M6 refreshed-candidate live session. Coverage remains this laptop, session and one-active-internal-path configuration only; the limits in `STATUS.md` still apply. The live report is local-only evidence and is not committed or published.

**Artifacts and publication.** The final ZIP and checksum remain under ignored `artifacts/`. The first P1.3 candidate `1a319aa6…` is preserved as pre-fix evidence only and must not be published. The historical M6 candidates `cdefbb10…` and `0328525a…` also remain preserved. The v0.1.0 binaries are unsigned. This docs-only record follows S2 and is not part of the release source. No `v0.1.0` tag or GitHub Release existed when this record was written; the publication section above records the tag at S2 and the publication of exactly this ZIP and checksum.

## P1.3 first package candidate, local build-path finding and S2 remediation — 2026-09-25

**Superseded release source.** After its hosted CI passed (305/305, schema PASS), `bdefbac75dba0e5f65af59c67677e69ad4c7fb27` (`docs: record P1.3A hosted CI and pre-freeze plan`) was defined as release source S. From that checkout:

- The final audit passed: 7 projects, 0 vulnerable direct or transitive packages, 0 problems.
- The historical `cdefbb10…` ZIP and checksum were moved aside unchanged.
- The 14 `bin`/`obj` directories were deleted by explicit path.
- Packaging produced `WinGPUDoctor-0.1.0-win-x64.zip` with SHA-256 `1a319aa63a5f33a8da6d1ce09ed964f556870f147308ab1c34c3ff58614b4a2f`: 734887 bytes, 25 files, 9 Worker files, a matching checksum and packaged documents identical to S.

Its comparison gate against `cdefbb10…` failed. The gate expected only `README.md`, `SECURITY.md` and `wingpudoctor.dll` to differ, but all 9 first-party DLLs differed. Structural review showed no unintended code change:

- The SDK's SourceLink records the HEAD commit in the unpackaged PDB, so each commit changes the deterministic PDB ID, MVID and timestamp.
- The CLI assembly additionally carried the P1.3A help-text change.
- The Supervisor's Worker pins followed the changed Worker files.

**Local build-path finding.** The same review found that every first-party DLL recorded the builder's absolute local checkout path, including the Windows user-profile directory name, in its CodeView debug record; the Microsoft dependency DLLs use `/_/…` paths. The `cdefbb10…` candidate had the same property. Nothing had been published. The owner declined to publish binaries containing the local path and superseded `bdefbac…` for release purposes. The `1a319aa6…` ZIP and checksum were preserved as pre-fix evidence only, with their hashes verified before and after the move.

**Mechanism check (V0).** Before any tracked edit, SDK 10.0.401 built `WinGPUDoctor.Core` alone into ignored evidence with `-p:DeterministicSourcePaths=true`:

- The compiler path map sent the Git checkout to `/_/` and the NuGet package root (`.tools/packages`) to a separate `/_1/`.
- The CodeView path, all 9 PDB documents and the SourceLink key were rooted at `/_/`, and the SourceLink URL was unchanged.
- IL and metadata were identical to the unmapped build except the MVID.

**Remediation (S2).** `Directory.Build.props` sets `DeterministicSourcePaths=true` for Release builds only. This uses the SDK's Git-derived source root, so release packaging requires a Git checkout. `ContinuousIntegrationBuild` (broader CI semantics) and a manual `PathMap` (inconsistent with SourceLink) were rejected. `scripts/package-v0.1.ps1` calls the new `scripts/package-path-guard.ps1` twice: on the staging folder, before a ZIP is written, and on the final ZIP, before the checksum is written. Packaging fails if any packaged file contains:

- the current checkout or user-profile path, in UTF-8 or UTF-16LE, with `\`, `/` or JSON-escaped separators;
- any `X:\Users\` path;
- an embedded PDB;
- a first-party CodeView path not rooted at `/_/`.

First-party DLLs are matched by file name, so `worker/` copies are included, and exactly 9 are required. `AGENTS.md` notes the mapping. No product code, dependency, lock file or version changed.

**Checkpoint validation.** Before the commit, the owner ran these checks with the checkout at `bdefbac…` plus exactly the 4 reviewed files:

- `git diff --check` was clean, and both scripts parse.
- `./scripts/dev.ps1 -Action test` passed: Release build **0 warnings / 0 errors**, **305 passed / 0 failed / 0 skipped**, and all schema and helper checks.
- All 31 first-party DLL copies in the Release outputs (including 12 Worker copies) have one CodeView record rooted at `/_/`, and all 7 SourceLink maps use only `/_/*`.
- The exact 25 package inputs have 0 guard findings.
- Against the preserved `1a319aa6…` and `cdefbb10…` ZIPs, the guard reported exactly **27** findings each: checkout or user-profile path, user-profile pattern and unmapped CodeView on each of the 9 first-party DLLs. It reported 0 findings on the other 16 entries.

The four files were then committed and pushed as S2; its hosted CI is recorded above.

## P1.3A hosted CI result and pre-freeze record — 2026-09-25

The P1.3A remediation checkpoint was committed as `a9a83c271fa268d836d11f432e1411c403f30b67` (`fix: correct example provenance and pre-release docs`, parent `5354cce40217acb6bd867bc7e408474d37fb9493`) and pushed to `main`; `origin/main` and GitHub `main` matched that commit. A read-only comparison of the commit and parent trees found exactly the 14 reviewed paths changed (13 modified and 1 added, `scripts/update-examples.ps1`), with content identical to the locally validated working tree recorded below.

Hosted GitHub Actions `deterministic-tests` run #6 (run `36078756659`, job `107895765404`), triggered by the push to `main` for that exact SHA, succeeded in 1 m 34 s. The standard-user test step reported `Passed!  - Failed:     0, Passed:   305, Skipped:     0, Total:   305, Duration: 3 s - WinGPUDoctor.Tests.dll (net10.0)`, and `./scripts/verify-schema.ps1` reported `Schema checks: PASS (valid synthetic report, contradictory state rejected, undeclared field rejected).` No artifact was uploaded. The only annotation was the known non-failing Node.js 20 deprecation notice for `actions/checkout@v4` and `actions/setup-dotnet@v4`, which remains deferred maintenance. The reviewed restore, test and schema step logs contained runner paths and workflow script text only; the ephemeral test-account password was not printed. No tag or GitHub Release existed. P1.3A pre-freeze remediation is complete.

This docs-only pre-freeze record changes only `STATUS.md` and this file. It is intended to become release source S after it is pushed with owner authorization and its own hosted CI passes. That CI result and the S designation are deliberately not recorded inside S, to avoid a recursive record; they belong with the release evidence after S. Remaining P1.3 steps, each separately authorized: define S; from a clean checkout of S, rerun the direct-and-transitive NuGet vulnerability audit against the final locked dependency state; move the historical `artifacts/WinGPUDoctor-0.1.0-win-x64.zip` and its checksum aside; generate the final v0.1.0 ZIP and SHA-256; run packaged validation on that exact ZIP; then tag `v0.1.0` at S and publish the GitHub Release and its assets.

## P1.3A pre-freeze remediation — 2026-09-25

Base: `main` and `origin/main` at `5354cce40217acb6bd867bc7e408474d37fb9493`, the pushed P1.2 Phase F reconciliation commit. The owner confirmed that hosted CI passed **304/304** tests and schema verification on that commit and that `HEAD` and `origin/main` did not drift during this remediation. Release source S was not defined, and no package, ZIP archival, tag, Release, staging, commit or push was performed in this pass.

**Example provenance finding and fix.** A full-project review found that the published synthetic examples labelled Windows version/build, manufacturer/model and driver provider/version/date as `wmiVideoController`, and that their collection metadata listed only `wmiVideoController` and `displayConfig`. The production mapping labels those fields `wmiOperatingSystem`, `wmiComputerSystem` and `wmiSignedDriver`, and orders collector runs operating system, computer system, video controller, display topology, then signed driver (the last only for non-empty video inventory). Schema 0.2.0 accepts any declared source in any observation, so the schema checks could not detect the mismatch. The defect was confined to the test fixture used to generate the examples (`ModelAndPrivacyTests.Sample()`); production collection and real exports were unaffected. The fixture now uses production per-field provenance and collector order; `TopologyTests.WithTopology()` inserts the topology run before the signed-driver run; and `M5FindingTests` selects the display run by source instead of assuming it is last. The new deterministic test `SyntheticExampleFixtureUsesProductionProvenanceAndCollectorOrder` compares the fixture's privacy-projected per-field sources and collector order with the supervised production path over the existing in-memory worker wire. The previously ignored local `.tools/update-examples.ps1` generation path is now tracked as `scripts/update-examples.ps1`, with the same fixture/writer logic, explicit Release-output checks, and LF/UTF-8-without-BOM output. Regeneration changed only the source values of those seven field kinds and added the three missing collector runs; values, findings, warnings and privacy metadata are unchanged, and the examples remain wholly synthetic.

**Documentation and help.** CLI `--help` and README now state that exit `3` covers both an incomplete collection whose report is still previewed or saved and collection stopped with no report (a controlled first Ctrl+C or a fatal collection failure); that the no-report case writes nothing to stdout or the output file and prints `No report was exported.` on stderr; and that export decline/failure (`4`/`5`) takes precedence over an incomplete-collection `3`. Help also lists destination errors under `2`. Exit-code behavior is unchanged. CONTRIBUTING now points to the public repository's Issues and Pull Requests (the owner confirmed Issues are enabled), and ARCHITECTURE no longer describes M6 as beginning or the M4 checkpoint as local only.

**Owner-run validation** in the local Windows 11 x64 checkout, PowerShell 7.6.6, repository root, 2026-09-25:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action build` | PASS |
| `pwsh -NoProfile -File ./scripts/update-examples.ps1` | PASS; synthetic schema checks PASS |
| Regenerated `examples/report.example.json` SHA-256 | `C2D3A5C30486F7D00A6711E08C2E7E176871186F8A50BAEC10C20CC39AC6ED2E` |
| Regenerated `examples/report.example.md` SHA-256 | `12BB59E271D8F213DE55C0A5ED678C23E888FAEB41CCF680FC4D7BB9DF1862E6` |
| `./scripts/dev.ps1 -Action test`, xUnit | **305 passed / 0 failed / 0 skipped** |
| Synthetic schema checks | PASS |
| M3 helper deterministic checks | 49 passed / 0 failed |
| M4 integrated schema checks | 8 passed / 0 failed |
| M4 host-process admission probe | 1 passed / 0 failed |
| M4 deployment helper checks | 10 passed / 0 failed |
| Execution-fingerprint checks | 8 passed / 0 failed |
| M4 process-evidence checks | 6 passed / 0 failed |
| Release CLI `--help` (non-live) | clarified exit-code wording shown; exit `0` |
| `git diff --check` | exit `0` for tracked changes; the new untracked script was reviewed separately for whitespace |
| `git status --short` before this record update | 11 modified tracked files and 1 untracked file (`scripts/update-examples.ps1`); no unexpected files |

Both example hashes match the output predicted independently from the pre-change examples before regeneration. The ignored local TRX (`artifacts/test-results/unit-tests.trx`) records 305 executed and 305 passed, including the new provenance test, the exact example-match test and the adjusted M5 test. The C# changes were not compiled in the review environment; the owner-run build and tests above are the compile and behavior evidence. No live collection, cancellation, packaged or physical-hardware check was run; the only production-code change is CLI help text. `STATUS.md` and this section were written after these results. Hosted CI for this checkpoint must be verified after its separately authorized push.

**Package impact.** The packaged `README.md` and the CLI assembly (help text) change; examples, tests, scripts, `STATUS.md`, this record, `CONTRIBUTING.md` and `ARCHITECTURE.md` are not packaged. The historical candidate (`cdefbb10d1f401b3574119255924facece084c16f878081bfcc1183786cdcc8d`) therefore remains historical evidence only; P1.3 must build and validate a fresh package from release source S. `artifacts/WinGPUDoctor-0.1.0-win-x64.zip` and its checksum remain in place, and `scripts/package-v0.1.ps1` refuses to overwrite them, so moving them aside requires separate owner authorization before P1.3 packaging.

**Deferred after v0.1.0 (owner-accepted, non-blocking).** Export-destination existence and parent-directory failures are reported only after collection and preview (create-new export still never overwrites). `--output --yes` consumes `--yes` as the file name when the output value is omitted (export still requires typed `EXPORT`, or exits `4` with redirected input). Development hooks remain in shipped binaries: the opt-in lifecycle probe, and worker synthetic scenarios that the production parent never requests. CI Actions are not pinned to commit SHAs (read-only token, no secrets or artifacts; release packages are built locally). The local CRLF working-tree copy of `schemas/report-0.2.0.schema.json` normalizes to the tracked blob and is not packaged, and the size of ignored `.tools/` and `artifacts/` is local housekeeping. None of these blocks v0.1.0.

## P1.2 Phase F post-public security gate — 2026-09-24

GitHub `erictyj1155/WinGPUDoctor` is Public with `main` at `795779c1a0db68507309260255c4de7e17162b3f`. Phase E's publication gate passed. Private Vulnerability Reporting is enabled and its **Report a vulnerability** flow was verified. Secret scanning and repository push protection are enabled; secret scanning reports **0 alerts**. The active `main` ruleset blocks force pushes and branch deletion, with no bypass actors and no mandatory pull-request workflow. Dependency Graph and Dependabot alerts remain enabled, with **0 open Dependabot alerts**. The latest hosted CI passed **304/304** tests and schema verification. Only `main` is publicly published; no tag, GitHub Release or Actions artifact exists. No unexpected auxiliary ref or sensitive exposure was found in the focused Phase F review. GitHub's dependency graph still does not provide complete coverage of the tracked transitive NuGet lock-file set; P1.3 must repeat the local direct-and-transitive vulnerability audit against the final locked dependency state. This security gate does not complete Phase F until the documentation reconciliation is committed, pushed and verified.

## P1.2 hosted CI recovery and dependency coverage — 2026-09-24

The corrected private GitHub Actions `deterministic-tests` [run](https://github.com/erictyj1155/WinGPUDoctor/actions/runs/35998313537) completed successfully on commit `7ae583a9bd34d59b473ea62303b2267542556a9d`. Its `test` job passed locked restore, Release build, the full standard-user suite (**304 passed / 0 failed / 0 skipped**) and synthetic schema verification. No artifact was uploaded. This resolves the hosted rerun pending in the earlier P1 compatibility-correction snapshot below; the first failed run remains historical evidence. A Node.js deprecation warning for the existing checkout/.NET setup Actions was non-failing maintenance information.

GitHub Dependency Graph and Dependabot alerts were enabled while the repository remained Private. After GitHub checked dependency files, its graph listed **6** dependencies: **4 direct NuGet** packages from the Windows/test project files and **2 GitHub Actions**. Dependabot reported **0 open alerts**. The graph did not show the repository's **7 tracked `packages.lock.json` files** or their transitive NuGet entries, so zero GitHub alerts alone is not complete evidence for that transitive set. No dependency-submission or update automation was enabled.

The local `.tools/dotnet` SDK was **10.0.401**. All seven lock files were inspected; package names and resolved versions matched the existing restored package data with **0 mismatches**. With `NuGet.Config` pointing to nuget.org, the read-only command below queried vulnerability data for all seven projects and returned exit **0**, with **no known vulnerable direct or transitive NuGet packages reported**. `--no-restore` did not regenerate lock files or update packages.

```powershell
.\.tools\dotnet\dotnet.exe package list --project WinGPUDoctor.slnx --vulnerable --include-transitive --no-restore --format json --config NuGet.Config
```

For v0.1.0, the owner approved this local direct-and-transitive NuGet audit as the transitive dependency release gate alongside the enabled GitHub graph/alerts. **P1.3 must rerun the audit against the final locked dependency state before release.** Automatic Dependency Submission, an explicit Component Detection workflow and `.github/dependabot.yml` version-update automation are deferred maintenance work, not publication blockers. This dated result is a known-advisory check of the existing package state, not a guarantee against future findings.

## P1 hosted CI compatibility correction — 2026-09-24

The owner reported that the first private GitHub Actions `deterministic-tests` [run](https://github.com/erictyj1155/WinGPUDoctor/actions/runs/35973927066) on commit `b5a7dc516d532fc3a679b61a4700316c83ffbe72` passed **294** tests and failed **10**; schema verification did not run and no artifact was uploaded. The supplied log summary says the current-parent elevation assertion evaluated true and native Worker tests rejected admission. The private job logs were not retrievable through the available GitHub connector in this development pass, so the exact hosted case list was not independently confirmed. [GitHub documents](https://docs.github.com/en/actions/reference/runners/github-hosted-runners#administrative-privileges) its hosted Windows runners as administrators with UAC disabled. `ParentSecurityContext` intentionally rejects an elevated or above-medium parent before Worker launch, matching the accepted M4 security contract. Local validation previously used a non-elevated medium-integrity parent.

The CI workflow now builds with a workspace-local package cache, then runs its unchanged full test command under a temporary standard Windows account with a loaded profile and workspace access. Account-creation, access or test failure stops the job; the account is removed after the test command. No test filter, skip, production-code change, privilege relaxation, live hardware collection or artifact upload was added. The existing schema step remains after the test step.

The first focused local attempt in the restricted tool sandbox passed **5/15** and failed **10/15** at private pipe-client creation; this was a separate process-permission limitation, not the hosted elevation result. With ordinary process access and a confirmed medium-integrity Windows token, the same focused set passed **15/15**, including native Worker cases, current-parent inspection, elevated/above-medium rejection and unverifiable-parent rejection. `./scripts/dev.ps1 -Action test` then passed locked restore, Release build with **0 warnings / 0 errors**, **304 passed / 0 failed / 0 skipped** xUnit tests, synthetic positive/negative schema checks, **49** M3 helper checks, **8** M4 integrated schema checks, **1** admission probe, **10** deployment checks, **8** execution-fingerprint checks and **6** process-evidence checks. This validates the unchanged local product/security behavior; the new account-creation workflow has not yet run on GitHub. P1 remains paused until a corrected commit is pushed and a hosted CI run passes.

## M6 refreshed-candidate packaged live revalidation — 2026-09-24

The owner authorized live checks of the corrected **734747-byte** ZIP with SHA-256 `cdefbb10d1f401b3574119255924facece084c16f878081bfcc1183786cdcc8d`. Before execution, `main` remained at `367769e33ed2bbc58f877756ae3d4e559d34f54b` with 19 modified tracked files, 3 nonignored untracked files and nothing staged. The checksum, single archive root, 25 entries and 9 Worker files matched; the executable was extracted to a fresh unique system-temporary directory outside source, build and staging folders, and its hash matched the ZIP entry. All live commands used that executable with a non-administrator token and ordinary process access. No hardware, display, driver, MUX or power setting was changed.

The package-aware `verify-cli.ps1 -CliExecutable <extracted exe>` passed **7/7** checks: help; invalid format, unscoped `--yes` and UNC destination rejected before collection (exit 2); JSON preview passed schema and created no default file; redirected-input export refusal (exit 4, no report); and existing-file refusal (exit 5, synthetic sentinel content and SHA-256 preserved). Direct help identified tool 0.1.0 (exit 0). A separate direct JSON preview completed with exit **0** and passed schema. Along with the package-aware preview, refusal and overwrite paths and the explicit export below, five non-cancellation CLI invocations reached collection; the cancellation run was separate. No restricted-process attempt was made in this refreshed-candidate session; the original candidate's restricted failures remain recorded below, without a proven internal cause.

The explicit `--format json --output <ignored local path> --yes` run exited **0**, wrote a **9312-byte** report, previewed the same snapshot on stderr and wrote nothing to stdout. The report passed `schemas/report-0.2.0.schema.json`; tool/schema/privacy versions were **0.1.0 / 0.2.0 / 0.2**. `Get-M3PrivacyFailures` found **0** failures and `Get-M3ReportFailures` found **0** failures. No raw instance ID, LUID, native path, EDID identifier, SetupAPI identity, provider exception, local path or hidden correlation key was detected by these bounded checks; manual review remains necessary before sharing.

All five collectors succeeded. Projected topology was available with one active `display-1`, an available target and exact source/target associations to `gpu-2`; optional friendly name remained `unknown/missingValue` with one nonblocking `targetName` issue. The informational findings were `inventory.multiple-adapters` and `topology.endpoint-adapter-association`. Warnings were `inventoryOnly`, `providerReportedValues`, `topologyIsNotRendering` and `reviewBeforeSharing`, with no `collectionIncomplete`. **All five finding evidence paths resolved**, and their referenced facts supported both claims. This does not establish application rendering, workload, electrical routing, MUX state, health, driver correctness or cause.

Exactly one packaged `test-m4-cancel.ps1 -Mode Cancel -Runs 1 -CliExecutable <extracted exe>` run passed: first Ctrl+C delivered, cancellation observed, exit **3**, no later Worker launch, report or preview, empty stdout, and the cancellation notice present. All **4 tracked Workers** had confirmed cleanup and post-exit state `Exited`; **0 survived**, with **0 harness failures**. Signal-call start to observed CLI exit was **40.1185 ms**, including delivery and observation overhead; it is not isolated cleanup duration or a universal timing guarantee. No second-interrupt or stress run was performed.

This evidence belongs only to refreshed ZIP hash `cdefbb10d1f401b3574119255924facece084c16f878081bfcc1183786cdcc8d`. It covers this laptop, Windows session, current one-active-path configuration, installed .NET 10 x64 runtime and ordinary non-administrator execution. External displays, clone/extend/hot-plug, AMD, other Intel/NVIDIA layouts, ARM64, eGPU, RDP, virtual displays, other machines and broader runtime environments remain physically untested. The packaged application operated from the temporary extraction with its own Worker closure; PowerShell was only the validation shell. Raw report/process evidence is ignored locally. The original ZIP's earlier live session remains separate historical evidence below. M6 is still incomplete and uncheckpointed; focused Gate 3 F1/F2 re-review is next.

## M6 Gate 3 F1/F2 correction and refreshed candidate — 2026-09-24

The original 734751-byte ZIP (`0328525a466e955686d6680430e3ead6e5a7d3546f7a1d78e53d5e806e8b5295`) received the packaged live session recorded below. Gate 3 then identified two Should-fix items: the package script invoked the SDK before `dev.ps1` safeguards applied, and README/ARCHITECTURE contained stale M6 status wording. The original ZIP and matching checksum were hash-verified and preserved under ignored `artifacts/m6-historical-0328525a466e9556/` as historical evidence, not the corrected candidate.

The package script now applies the same six process environment settings as `dev.ps1` before its first SDK invocation and restores the caller's exact values in `finally`; originally absent variables are removed. Focused child-shell checks passed for six sentinel values and six originally absent variables on the existing-artifact refusal path, and for six sentinel values after successful packaging. README now points rolling candidate evidence to STATUS/VALIDATION; ARCHITECTURE records M6 release hardening without changing architecture boundaries.

The corrected script completed locked restore and Release build with **0 warnings / 0 errors**, then produced `WinGPUDoctor-0.1.0-win-x64.zip`: **734747 bytes**, SHA-256 `cdefbb10d1f401b3574119255924facece084c16f878081bfcc1183786cdcc8d`; the `.sha256` file names that ZIP and matches its hash. The refreshed archive has the same **25-entry** layout and **9 Worker files** as the original; entry-by-entry byte comparison found only `README.md` changed. All extracted entry hashes match the ZIP. No PDB/XML, tests, source, logs, TRX, private report or cache was added.

A fresh system-temporary extraction outside the repository/build output passed `--help` (exit 0, version 0.1.0), invalid format and unscoped `--yes` (each exit 2 before collection), executable ProductVersion 0.1.0/FileVersion 0.1.0.0, parent/Worker .NET 10 runtime configuration, dependency-manifest inspection and the metadata-only execution fingerprint (**21 inputs / 9 Worker files**) using the installed runtime host. The corrected README and third-party notices are in the archive. At this correction-pass snapshot, these were **non-live** checks: no preview collection, export or cancellation had been run on the refreshed ZIP. The earlier live result applied only to the original hash; the separately authorized refreshed-candidate session is recorded above. M6 remains incomplete and uncheckpointed.

## M6 packaged live validation — 2026-09-24

The owner authorized live checks of the **existing** Gate 2 candidate, not a rebuild. Before execution, `main` remained at `367769e33ed2bbc58f877756ae3d4e559d34f54b` with the expected 18 modified tracked files, 3 nonignored untracked files and nothing staged. The ZIP was 734751 bytes with 25 entries (9 Worker files); its SHA-256 matched the Gate 2 checksum: `0328525a466e955686d6680430e3ead6e5a7d3546f7a1d78e53d5e806e8b5295`. It was extracted to a fresh unique system-temporary directory outside the repository, build output and package staging. The extracted executable's bytes matched its ZIP entry after the checks. Every live command targeted that extracted executable. The Windows token was non-administrator for the live commands; no elevation or hardware setting change was made.

The first package-aware verification attempt in the restricted process sandbox reached preview but returned an empty result; the script stopped at its schema check rather than diagnosing the empty output. A direct diagnostic invocation in the same restricted sandbox returned exit 3 with no JSON and the generic incomplete-collection notice. The unchanged extracted candidate then completed a direct JSON preview with exit 0 and a non-administrator token when given the process access needed by its private Worker pipes. The unchanged `verify-cli.ps1 -CliExecutable <extracted exe>` passed under that access: help; invalid format, unscoped `--yes` and UNC rejection (exit 2); JSON preview/schema and no default file; redirected-input export refusal (exit 4, no file); and existing-file refusal (exit 5, original sentinel content/hash preserved). The restricted failures are consistent with the process-sandbox limitation seen in Gate 2 tests; their precise internal cause was not separately proven. They are retained as failed attempts, not reported as passes or silently omitted.

One separate explicit packaged `--format json --output <ignored local path> --yes` run returned **exit 0**, created a 9312-byte report, previewed the collected snapshot on stderr and wrote nothing to stdout. The exported JSON passed `schemas/report-0.2.0.schema.json`; schema/tool/privacy versions were **0.2.0 / 0.1.0 / 0.2**. `Get-M3PrivacyFailures` returned **0**, and the complete existing one-laptop `Get-M3ReportFailures` check returned **0**. No raw device-instance identity, native path, LUID, EDID identifier, SetupAPI key, provider exception, private local path or hidden correlation key was found by those checks; filtering is not an anonymity guarantee, so manual review remains necessary before sharing.

All five collection operations succeeded in the explicit export. Projected topology was available with one active `display-1`, target available, and both source and target endpoints exactly associated with report-local `gpu-2`. The optional friendly name remained `unknown/missingValue`; the display collector recorded that single nonblocking issue. Findings were `inventory.multiple-adapters` and `topology.endpoint-adapter-association`, both informational. The warnings were `inventoryOnly`, `providerReportedValues`, `topologyIsNotRendering` and `reviewBeforeSharing`, with no `collectionIncomplete`. **All five finding evidence references resolved** in the exported JSON; both finding claims were supported by the referenced observations and the two endpoint associations used exact evidence. This says nothing about application rendering, workload, electrical routing, MUX state, health or cause.

Exactly one packaged `test-m4-cancel.ps1 -Mode Cancel -Runs 1 -CliExecutable <extracted exe>` run passed. The harness observed the intended Worker attempt and delivered the first Ctrl+C; cancellation was observed, no later Worker started, and the CLI exited **3**. The cancelled run produced **no report or preview**, stdout was empty, and the cancellation notice was present. Signal-call start to observed CLI exit was **26.2081 ms**; this includes delivery and observation overhead and is not an isolated cleanup measurement. All **4 tracked Workers** had confirmed cleanup and post-exit state `Exited`; **0 survived**. The harness reported **0 failures**. No second-interrupt or repeated stress run was performed.

These results cover only this laptop, this Windows session, its current one-active-path configuration, ordinary non-administrator execution and this framework-dependent package. They do not physically validate external displays, clone/extend/hot-plug, AMD or other Intel/NVIDIA configurations, ARM64, eGPU, RDP, virtual displays, other machines, universal timing or broader runtime-install scenarios. The package operated from the extracted temporary path, using its own Worker closure rather than development executable output. Raw reports, preview and process evidence remain in ignored `artifacts/` or temporary storage; only this sanitized summary is tracked. M6 remains incomplete and uncheckpointed. Independent Gate 3 final review is next; staging and committing require separate owner authorization.

## M6 Gate 2 — v0.1 non-live release candidate — 2026-09-23

Gate 0 scope and Gate 1 release plan were accepted before this authorized implementation pass. Gate 2 established tool version 0.1.0 while retaining report schema 0.2.0 and privacy version 0.2; added the allowlisted framework-dependent Windows x64 ZIP workflow; updated current instructions, generated examples and dependency notices; and performed source, audit, package and extracted-package checks. This is **not** packaged physical validation, Gate 3 approval or local v0.1 completion. No live collection or cancellation was run for M6 Gate 2.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build **0 warnings / 0 errors**; xUnit **304 passed / 0 failed / 0 skipped** |
| Fresh source copy `./scripts/dev.ps1 -Action build` | **139** tracked/nonignored files copied to a new temporary directory outside the repository; locked restore and Release build passed with **0 warnings / 0 errors**. The copy reused the existing portable SDK and package cache through toolchain junctions, but no prior source/build outputs |
| Schema and helper checks in the canonical workflow | Synthetic schema positive/negative checks passed; M3 helpers **49**, M4 integrated schema **8**, admission **1**, deployment **10**, execution fingerprints **8**, process evidence **6** passed |
| `./scripts/dev.ps1 -Action audit` | Passed locked restore and advisory audit with network access; no advisory warning was reported |
| Package creation | `./scripts/package-v0.1.ps1` passed its Release build, allowlist, x64/version/runtime, worker-closure and byte/hash checks |
| Final archive | `artifacts/WinGPUDoctor-0.1.0-win-x64.zip`; **734751 bytes**, **25 entries**, including **9 Worker closure files** under the single `WinGPUDoctor-0.1.0-win-x64/` root |
| ZIP SHA-256 | `0328525a466e955686d6680430e3ead6e5a7d3546f7a1d78e53d5e806e8b5295`; matching `artifacts/WinGPUDoctor-0.1.0-win-x64.zip.sha256` contains this hash and the ZIP filename |
| Extracted copy outside the repository | `--help` exit **0** and version 0.1.0; invalid format and direct UNC destination exit **2** before collection; executable ProductVersion 0.1.0/FileVersion 0.1.0.0; metadata-only Worker fingerprint probe passed with **21 inputs / 9 Worker files** |

The first test attempt in the restricted process sandbox built successfully but failed 10 existing `WorkerProcessTests` at private pipe-client creation (**294 passed / 10 failed**). The identical canonical workflow passed **304/304** with the process permissions required by those tests; no product code or test behavior was changed to conceal the restriction. An initial restricted audit could not reach the advisory endpoint (NU1900); the separately rerun audit with network access passed. The fresh-source build reported a .NET first-run PATH advisory but completed successfully without a persistent PATH change. This machine has no installed SDK; the fresh copy reused the checkout's portable SDK and cached packages, while all source and build outputs were independent. The full 304-test workflow was run in the working checkout, not repeated in the fresh copy. Hosted CI has not been run.

The application package comes from the normal CLI Release build, with required parent files explicitly selected and the Worker closure derived from `worker-runtime-closure.ps1`. The final ZIP entry scan found no PDB/XML documentation, source, tests, raw reports, logs, TRX, SDK/cache or build intermediates. Parent and Worker runtime assets were present; Worker bytes matched their same-build inputs and the metadata-only deployment fingerprint accepted the extracted layout. The extracted folder did not depend on a source/build directory or `.tools` for its pre-collection commands; the installed .NET 10 x64 runtime was available. This does not substitute for a packaged live collection.

The release dependency graph contains direct `System.Management` **10.0.12** and transitive `System.CodeDom` **10.0.12**; lock-file changes only adjusted internal project-reference constraints from 1.0.0 to 0.1.0, without changing external package versions. Local package metadata records MIT for both; their supplied notice texts were inspected, and `THIRD-PARTY-NOTICES.md` records the shipped package licenses. The .NET shared runtime is a prerequisite, not bundled.

Bounded publication-hygiene inspection covered **136** tracked paths on `main`, path names across **9** reachable commits (**219** occurrences), targeted secret/path patterns in reachable committed text, all **25** ZIP entries and the checksum file. No generated/private artifact path was found in the tracked tree or history, and no package entry fell outside the selected layout. The pattern scan surfaced only a synthetic `C:\Users\Alice` path and fake `ghp_abcdefghijklmnopqrstuvwxyz` literal in historical test code; neither is a real credential or machine report. Pattern checks and file review cannot prove the absence of every possible secret. Ignored local build, test and physical evidence remains outside the package and proposed Git changes.

At the Gate 2 snapshot, packaged live preview/export, refusal/overwrite behavior and first-interrupt cancellation still required separate owner authorization; the later M6 packaged-live section above records their authorized execution. Independent Gate 3 review, final checkpoint approval and a real private vulnerability-reporting channel before public distribution remain pending. No staging, commit, remote, push, tag or release occurred in Gate 2.

## M5 — Explainable Active-Display Associations: Gate 3 correction and Gate 4 physical validation — 2026-09-23

### Gate 3 F1 correction and closure

The independent semantics/privacy review found no production semantics or privacy defect. It identified one test-only example-reproducibility issue: checkout line endings could make otherwise identical generated examples compare differently. The test now normalizes example-file text from CRLF to LF and lone CR to LF while preserving LF. Comparison remains otherwise exact, so final-newline differences, other whitespace differences, and genuine content mismatches remain detectable. The independent F1 re-review passed. Gate 3 is complete.

The focused M5 re-review passed **34 tests**. The full correction-pass workflow passed locked restore, a Release build with **0 warnings / 0 errors**, and **304 xUnit tests passed, 0 failed, 0 skipped**.

| Correction-pass check | Result |
|---|---|
| Synthetic schema positive and negative checks | Passed |
| Integrated schema fixtures | **8 passed** |
| M3 helper checks | **49 passed** |
| M4 admission probe / deployment checks | **1 / 10 passed** |
| Execution fingerprint / M4 process-evidence checks | **8 / 6 passed** |
| git diff --check | Passed |

The build, test, schema and helper outcomes above are correction-pass and focused re-review results; they were inspected, not rerun, during this documentation reconciliation or the final readiness review. git diff --check was rerun after this documentation update and passed. The earlier Gate 2 **301/301** result and restricted **289/299** attempt remain historical records above; they are not rewritten as the latest result.

### Gate 4 scoped physical validation

After a fresh successful Release build, the authorized existing hardware-smoke.ps1 workflow performed one live collection on the current laptop as a non-administrator. The CLI returned **0**, and all five collection operations succeeded. The sanitized report contained available topology with one active path: display-1; exact source association to gpu-2; exact target association to gpu-2; target available = true.

Findings appeared in order: inventory.multiple-adapters, then topology.endpoint-adapter-association. M5 evidence references resolved. JSON and Markdown findings matched from the same snapshot. Schema 0.2.0 passed, and privacy/report checks returned zero failures. The optional monitor friendly name remained unknown/missingValue and nonblocking; no collectionIncomplete warning was emitted. Candidate-file hashes were unchanged by the physical run. Raw reports, preview, logs, and other run evidence remain ignored/private local evidence and are not included here.

This physical result covers only the current laptop and its current one-active-path configuration. It does not physically validate unresolved correlation, unavailable target, empty or unavailable topology, external displays, clone/extend/hot-plug, AMD, other Intel/NVIDIA configurations, ARM64, eGPU, RDP, virtual displays, other machines, or universal timing. Synthetic tests cover the unobserved semantic states.

## M5 Gate 2 implementation — 2026-09-23

The owner authorized implementation of the frozen four-finding interpretation contract, tests, generated synthetic examples and existing documentation. At the Gate 2 snapshot, independent Gate 3 semantics/privacy review and separate checkpoint authorization were still pending. This pass did not alter Windows collection, Worker, Protocol, Supervisor, native code, dependencies, schema version or CLI exit logic. The generated examples use the synthetic topology fixture through `PrivacyPolicy.Prepare` and both production writers; an xUnit test checks exact agreement with the tracked example files.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` with ordinary process permissions | Locked restore and Release build passed, **0 warnings / 0 errors**; **301/301** xUnit passed, 0 failed, 0 skipped |
| Synthetic schema | Version 0.2.0 example accepted; contradictory failed-with-value observation and undeclared field rejected |
| Existing integrated schema fixtures | **8** passed |
| M3 helper / M4 admission / deployment | **49 / 1 / 10** passed |
| Execution fingerprint / M4 process evidence helpers | **8 / 6** passed |
| New M5 cases | **31** deterministic cases in the 301-test result, covering rules, state variants, evidence resolution/rejection, ordering, privacy, writer escaping and production-generated examples |

An initial run under the restricted process sandbox built successfully but failed 10 existing `WorkerProcessTests` at private-pipe client creation (**289 passed / 10 failed**). This matches the historical environment restriction and is not an M5 rule failure. The workflow was rerun with the permissions required by those process tests, without changing product code or settings; after two additional focused regressions, the final run passed 301/301. The final result supersedes the restricted run for the complete test verdict; both outcomes are reported.

No new physical M5 validation was performed during Gate 2. Earlier M4 single-laptop collections and cancellation checks remain historical evidence. No opt-in live hardware or CLI collection script was run here, and no new machine/configuration coverage is claimed.

## M4 targeted final checkpoint corrections — 2026-09-22

The final timing/checkpoint-readiness review returned FAIL. This authorized correction pass addresses F1-F5 without changing production timing constants, containment, correlation, privacy semantics, schema 0.2.0 or CLI exits. **M4 is pending final checkpoint-readiness re-review**, remains incomplete and is unstaged/uncommitted. Earlier dated sections are historical; the corrections below supersede their overstatements.

### Correction and re-review map

| Finding | Current implementation / record | Verification |
|---|---|---|
| F1: late callback/output race | `HostCancellationController` atomic active → cancelled/output-committed state; actual CLI boundary `CollectionOutput.RunAsync` | Six new `CancellationHandoffTests` cases; retained first/second interrupt, late valid result, no-later-operation and cleanup regressions |
| F2: live readiness, signal timing, survival | `M4CollectionProbe`, `CollectionProgress`, `test-m4-cancel.ps1`, `m4-process-evidence.ps1` | Three corrected first-interrupt runs plus one non-signalled control; six process-evidence helper checks |
| F3: per-run fingerprints / admission claims | `validate-m4-final.ps1` persists both lists around each CLI process; direct PID/birth checks | Three healthy runs; successful same-host reuse and poisoned admission remain deterministic supervisor/admission evidence |
| F4: stage definitions and policy rationale | Timing definitions and margin table below | Existing instrumentation inspected; all five production-policy boundary cases passed; no constants changed |
| F5: historical numbers / baseline wording | Explicit historical errata below | Recomputed medians from preserved samples; named historical and current fingerprint baselines |

### Atomic cancellation/output handoff

One compare-and-exchange decides the winner while collection is active. Cancellation publishes the winning state before token signaling; `TryCommitOutput` can succeed only from active. `CollectionOutput` invokes no report preparation/preview/export until output has won, collection is closed and the handler is removed. A callback captured before removal but executing after commitment cannot change the winner; it permits ordinary/default termination. Second/reentrant interrupts also permit default termination. Failure closes active collection, and source disposal waits logically for active callbacks without blocking the console callback on worker cleanup or process waits.

The six new deterministic cases force both captured-callback interleavings using gates, hold a token callback in flight while cancellation vetoes the real CLI boundary, invoke a late callback during handler removal and output, check reentrancy/second interrupt, and verify failure closes the lifecycle without output. No sleep is the primary proof. Existing `CancellationTests` still reject a valid late result after cancellation and prevent the next operation; its after-attempt test now also verifies the progress trace.

### Corrected live evidence

The private environment hook `WINGPUDOCTOR_M4_PROCESS_PROBE=1` enables a normally absent progress sink. It records only fixed operation/attempt names, sequence numbers, CLI/worker PID plus start-time identity, and monotonic timestamps into ignored private stderr. It adds no public CLI argument or hardware identifier. Instrumented stderr is drained or redirected to private files; instrumentation overhead is included in these runs. This is not a hard real-time guarantee.

Readiness means the production parent validated Ready identity, Start and attempt 1 for `wmi.displayDrivers`, with that same PID/birth worker still alive. The worker announces the attempt immediately before its read; this proves the intended supervised attempt has begun, not a particular instruction inside WMI. Worker identities are captured at launch before Request, retained before signal, and checked directly after CLI exit. Creation-time job active-process limit one excludes nested product workers. The trace also checks that no later application worker started. No global process-name search or rediscovery through an exited root is used; console hosts are excluded.

Console attachment and discovery occur before the recorded signal call. UTC plus monotonic call-start/call-return points are persisted; latency is call-start to observed CLI exit and includes delivery, handler/cleanup and observation overhead. It is not an isolated cleanup or exact physical keypress latency.

| Corrected run | Bundle subdirectory | Exit | Signal call → CLI exit observed | Workers tracked / alive afterward | Preview / export |
|---|---|---|---|---|---|
| Cancel pilot | `m4-cancel-87c5ca5a906a4e6b9f2c69a6243839d0` | 3 | 79.8221 ms | 4 / 0 | none / none |
| Cancel 2 | `m4-cancel-6e15c3402358409f85e76ad5562863b4` | 3 | 39.0612 ms | 4 / 0 | none / none |
| Cancel 3 | `m4-cancel-4ca8c0e346c449f8aed58d80bc3cf908` | 3 | 45.7510 ms | 4 / 0 | none / none |
| Observe control | `m4-observe-92566cdab5384662aac08b876c6ec763` | 0 | no signal | 5 / 0 | present / present |

Every corrected cancellation had readiness, successful signal delivery, a cancellation trace/notice, empty stdout, confirmed worker cleanup, no later worker launch and no preview/export. Three earlier development attempts failed console attachment before any signal because attachment preceded console startup; they remain failures in `controlled-cancel.log` and their original subdirectories. Moving attachment after validated readiness resolved that harness startup race. No forced second Ctrl+C or provider hang was exercised live.

Healthy evidence is `m4-healthy-e1c7e43bf71743de903120e3c8939cc1/summary.json` within the bundle. Each run persists its own `BeforeFingerprint` and `AfterFingerprint` lists/timestamps, match result, CLI identity, lifecycle trace and five worker checks. All three pairs match one another and that summary's `BaselineFingerprint` (the corrected-build batch baseline). They are not claimed identical to older builds.

| Fresh separate CLI process | Whole-process duration | Exit | Per-run fingerprints | Tracked workers alive |
|---|---|---|---|---|
| 1 | 6615 ms | 0 | match | 0 of 5 |
| 2 | 6418 ms | 0 | match | 0 of 5 |
| 3 | 6475 ms | 0 | match | 0 of 5 |

Fresh minimum/median/maximum: **6418 / 6475 / 6615 ms**. These include CLI startup, collection, preview/export and instrumented process observation; they are separate from collector-only calibration and do not quantify operation headroom. Schema, decoded privacy/semantic checks, inventory, signed-driver association and preview/export checks passed each time. Repeated fresh processes establish repeated startup/deployment success and no tracked surviving worker in these runs, not process-wide admission reuse. `SupervisorTests` exercises a later supervisor with the same admission owner after confirmed success, while poison tests and the admission probe establish same-host blocking.

### Timing segment definitions and nesting

| Recorded segment | Actual boundary / interpretation |
|---|---|
| `DeploymentPreparation` | `WorkerDeployment.Resolve` in the native session factory; excludes preceding parent security inspection |
| `ProcessCreation` | Entire `NativeWorkerLauncher.LaunchAsync`, including security checks, pipe/job/attribute/environment setup and process creation; not only the native CreateProcess call |
| `WorkerStartupToReady` | First frame receive after session launch and Request send, through framing/decoding Ready; parent sequence/identity/state checks follow it. Not the entire connect budget or process lifetime to Ready |
| `RequestTransfer` / `StartTransfer` | Parent send plus sequence recording for the corresponding frame; Start follows validated Ready identity |
| `Operation` | Parent operation entry through its finally after cleanup/state finalization for a started operation; includes launch, provider wait, frames and cleanup. Admission-lease release follows the recorded sample |
| `ResultTransferValidation` | Final loop receive through framing/decoding and sequence validation, including remaining provider wait; final result state acceptance follows. Nested in Operation, not an independent additive stage |
| `CleanupTotal` | Starts before termination request and pending-I/O cancellation, then drain and exit confirmation under the one cleanup deadline. Called even after a healthy result |
| `PendingIoCancellation` | Drain/completion observation after the cancellation request; nested in CleanupTotal |
| `WorkerExitConfirmation` | Exit polling after termination was requested and I/O drain was observed; nested in CleanupTotal, not isolated natural-exit latency |

The historical calibration remains six healthy `CollectAsync` calls under a larger calibration policy (15 s operation, 5 s connect, 15 s frame), plus 18 synthetic infrastructure samples. It measures collector duration, not CLI startup/output or an exit code. The synthetic scenario named `blocked` does not itself issue a blocked parent read; the partial-frame case does. No actual provider hang was induced.

### Unchanged policy and relevant rationale

| Constant | Corrected rationale |
|---|---|
| 10 s operation / 10 s later reservation | Historical inclusive healthy maximum **4299.02 ms**; nominal operation allowance is about **2.33×**, a **5700.98 ms** difference. The nested 4002.36 ms result measurement must not be added. Reservations can clip the actual deadline |
| 8 s frame | Historical terminal receive/validation maximum **4002.36 ms**, including provider wait: about **2.00×**, **3997.64 ms** difference. A frame is still clipped by the operation deadline |
| 2 s cleanup | Historical bounded cleanup maximum **28.02 ms**: about **71.4×**, **1971.98 ms** difference. Corrected live cancellation latencies above support end-to-end behavior but do not isolate cleanup |
| 2 s connect | No measurement exactly matches this budget, which starts before native session creation and clips security/deployment/launcher setup. Deployment 15.94 ms and launcher 10.51 ms are supporting components; the 110.88 ms first Ready receive occurs later. Margin remains engineering judgment, not directly measured connect headroom |
| 500 ms final bookkeeping | Conservative engineering reserve, **not separately calibrated**. Worker cleanup/exit measurements cannot justify it; report preparation/preview/export are outside collection |
| 60 s overall | Finite engineering policy with separate active/cleanup/later/bookkeeping reservations. Five full 10 s active + 2 s cleanup allowances plus 0.5 s total 60.5 s, so the first of five operations is intentionally clipped to **9.5 s**. No deadline was weakened |

Five fake-clock policy cases establish strict-before acceptance/equality timeout, reservation clipping, completion, omitted-driver reservation release and exhausted-overall skipped starts. Successful fresh production-policy runs establish that the policy was not exceeded on those runs. They do not prove OS scheduling bounds, population percentiles or cross-hardware compatibility. No new instrumentation or timing constant change was needed to correct the mismatched rationale.

### Historical numerical and evidence errata (F5)

- Six collector-calibration durations give median **6769.21725 ms (approximately 6769.2 ms)**, not 6777.9 ms. The old six whole-CLI durations give median **8445 ms**, not 8447 ms.
- The earlier 788/781/858 ms cancellation intervals began before discovery/attachment and are not actual signal-call latency. Root-descendant rediscovery did not prove orphan absence; those old zero-survivor claims are withdrawn. Fixed-delay readiness is superseded by the validated marker above.
- The old healthy script launched six different CLI processes, despite the phrase “one process.” It captured one global baseline and post-run comparisons, not independently persisted before/after lists for each run. It did not establish same-host admission reuse or independently track all workers.
- The identical historical baseline comparison is specifically between `m4-final-validation-bef96858762e4c7a9d5246914748a53a` and first-pass `m4-final-validation-58554e5b7f4240e8b3fb17d47b2c5a6e`, not the earlier integrated-corrections bundle `m4-integrated-corrections-c8ebf882b0df4bf79c82c45fde3384e5`. That earlier build differs in CLI/Supervisor inputs as expected. No historical fingerprint was reconstructed.
- Remove “two orders of magnitude”: 2 s / 28.02 ms is about 71.4. A numerical 500 / 28.02 ratio (17.8) would still compare unrelated bookkeeping and cleanup stages and is not a valid rationale.

### Fresh corrected-build validation and preserved evidence

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release **0 warnings, 0 errors** |
| xUnit / TRX | **270 total, 270 passed, 0 failed, 0 skipped**; includes six new handoff cases, retained Gate 1 native/process, integrated privacy/semantics and five policy cases |
| Schema | Existing positive/negative checks passed; **8** integrated fixtures passed; schema remains 0.2.0 |
| M3 / admission / deployment helpers | **49 / 1 / 10** passed |
| Execution-fingerprint / process-evidence helpers | **8 / 6** passed |
| `./scripts/verify-cli.ps1` | **7** passed: help, invalid format, unscoped yes, UNC rejection, JSON preview/schema/no file, redirected-input refusal, overwrite protection |
| Corrected live checks | **3/3** first-interrupt cancellations, **1/1** non-signalled control, **3/3** fresh healthy processes passed |
| Documentation/scripts/whitespace | **39** local Markdown link targets resolved; **20** PowerShell scripts parsed; **92** candidate files passed LF/trailing-whitespace/final-newline checks (five generated lock files excluded); `git diff --check` passed |

The first restricted deterministic run built successfully but failed 10 native private-pipe process cases (260/270); its log/TRX remain preserved. The complete standard-user run outside that command restriction passed 270/270 with no application elevation, privilege change, DACL/containment weakening or global tooling change. A full restore/build/helper log and its matching TRX are now retained together. Vulnerability audit was not run; CI remains unexecuted.

The coherent ignored private bundle is `artifacts/m4-final-corrections-70f0b2c3618842dd9bc0aebb001d5802/`: the final matching pair `final-deterministic-confirmed.log` and `final-270-tests-confirmed.trx`, `schema-fixtures/`, `final-cli.log` and `cli-checks/`, corrected cancellation/control logs and subdirectories, `healthy-validation.log` and healthy summary, plus final source/Git/static-check evidence and a bundle manifest. The final reentrant-token test asserts outside the callback so callback exception containment cannot swallow a failed assertion; the complete workflow was rerun afterward. `post-final-test-fingerprint.json` confirms all 21 production execution inputs still exactly match the corrected live batch after that test-only rebuild. Earlier successful, failed/intermediate logs and the prior 264-test TRX are retained separately. Raw reports, stderr, fingerprints, process identities, build logs and binaries remain outside the checkpoint candidate set.

Preflight found 24 tracked modifications and 67 untracked candidate files, M3 HEAD unchanged, an empty index and no remotes/tags. Final candidates are **24 tracked modifications + 73 untracked files = 97**: 48 source/project/lock files, 20 test/project/lock files, 17 scripts, 11 documents and one solution. This pass modifies 22 existing candidates and adds six files; all other initial file hashes are preserved, including Core, Windows collectors, Protocol, Worker, production timing policy and dependency locks. Git index/HEAD/config hashes and all M1-M3 commit/tree identities remain unchanged; nothing is staged. Exact final status/stat and expanded candidate paths are retained in the private bundle.

Current records reconciled: `STATUS.md`, `ROADMAP.md`, `ARCHITECTURE.md`, `README.md`, `PRIVACY.md`, `SECURITY.md`, `docs/API-FEASIBILITY.md`, this record and ADR 0007/index. `docs/REPORT-SCHEMA.md` was reviewed and requires no additional change in this pass: no report field, schema version, cancellation fixture or exit code changed.

Physical evidence is limited to this non-administrator laptop with its unchanged single internal display. No external display/attach-detach, stress, driver/service/power change, second forced interrupt, provider hang, remote operation or broad hardware validation was performed. All M1-M3 history and the existing index are preserved. No stage, commit, publication, tag, release or next-milestone work is authorized by this pass.

## M4 final pre-checkpoint phase — cancellation, timing calibration and healthy validation, 2026-09-21

Historical snapshot: the 2026-09-22 correction section above supersedes this section's cancellation handoff, readiness/survivor/timing claims, fingerprint/admission wording and numerical margins. Retained samples remain useful only within those corrected limits.

The targeted integrated semantics/privacy re-review passed. This authorized phase completed controlled Ctrl+C behavior, internal timing calibration, bounded healthy-machine validation on this laptop and source-of-truth reconciliation. All M4 work remains unstaged and uncommitted. These results are local evidence for the independent final checkpoint-readiness review, not that review or a completion claim.

### Implementation map

| Area | Implementation | Regression evidence |
|---|---|---|
| Controlled cancellation | `HostCancellationController` separates the first interrupt (controlled) from later interrupts (forced). The CLI registers `Console.CancelKeyPress` only around collection, never performs I/O or cleanup in the handler, passes the token to `SupervisedWindowsCollector`, maps `SupervisedCollectionException` code `host-cancelled` to exit `3`, and re-checks cancellation at the serialized transition after collection | `CancellationTests`; `scripts/test-m4-cancel.ps1` |
| Timing instrumentation | `CollectionTiming` records safe stage names through an opt-in sink; production defaults to a null sink and never logs identifiers | `CalibrationTests` |
| Timing policy | `CollectionTimingPolicy.CalibratedProduction` with finite absolute budgets and unchanged deadline architecture | `ProductionTimingPolicyTests` |
| Live checks | `scripts/calibrate-m4.ps1`, `scripts/validate-m4-final.ps1`, `scripts/test-m4-cancel.ps1` | Evidence directories cited below |

### Cancellation behavior

A first Ctrl+C during supervised collection suppresses default process termination, records host cancellation, signals the active supervisor, closes result acceptance for the active operation, prevents any later operation from starting, performs bounded cleanup through the existing containment path, skips report preparation, preview and export, and returns `3` after a plain cancellation notice. It does not report a provider timeout, does not create a `Cancelled` report state, and does not fabricate operation failure information. Already accepted independent operation results may stay internally retained but are never reported. A second interrupt leaves the default handler in place, so forced termination proceeds without promising cleanup, exit `3` or a report; the creation-time Job Object kill-on-close remains the containment backstop.

### Deterministic cancellation evidence

`CancellationTests` (4 cases) covers first-interrupt-controlled versus second-interrupt-forced, cancellation during the startup handshake (terminal kind `Cancelled`, code `host-cancelled`, reason `QueryFailed` and explicitly not `Timeout`, with no completed results), cancellation after one accepted operation preventing the next operation from starting, and cancellation after the real-operation attempt marker, where a valid result offered immediately afterwards is still not accepted. Cancellation before any worker starts, cancellation during cleanup, and retention of accepted data across cancellation are covered by the existing `SupervisorTests` and `SupervisedCollectorTests` cases. Preview/export suppression is structural: the CLI's cancellation checks return before report preparation, and the live runs below confirm no file is written. No test depends on real keyboard input.

### Live cancellation evidence

`scripts/test-m4-cancel.ps1` launches the CLI in its own hidden console, attaches to that console and delivers a real `CTRL_C_EVENT`. The attach/signal runs in a child process so the caller keeps its console, and the signal is confined to the CLI's own console.

| Run | Mode | Delay before signal | Attached | Exit | Report exported | Surviving processes | Standard error |
|---|---|---|---|---|---|---|---|
| 1 | Cancel | 2.5 s | yes | `3` | no | 0 | `Collection cancelled. No report was exported.` |
| 2 | Cancel | 2.5 s | yes | `3` | no | 0 | same |
| 3 | Cancel | 2.5 s | yes | `3` | no | 0 | same |
| 4 | Observe (no signal) | — | — | `0` | yes | 0 | report preview + `Report saved locally.` |

At signal time the observed process tree was `cmd.exe` → `dotnet.exe` (CLI host) → `dotnet.exe` (worker) plus their console hosts, so a worker collection was genuinely in flight. The three cancelled runs exited 788 ms, 781 ms and 858 ms after the signal, consistent with the bounded cleanup deadline, and left no descendant process. The non-signalled control run confirms the harness itself does not perturb a normal collection. Evidence is in ignored `artifacts/m4-cancel-<guid>/probe-result.json` and `cli-stderr.txt`, with the three cancelled runs under `m4-cancel-386962a7504c4304b08240a3fa9a6128`, `m4-cancel-bb16281bf643415b9f24387344316dfc` and `m4-cancel-0b9eefd0a9244337986d0f28ccc89aa5`, and the control run under `m4-cancel-4d54ac652eb84f5fbef60e66c0f5edfb`.

Two earlier attempts wrote Ctrl+C into an ordinary interactive console session instead. That path never delivered a console signal here (a 15-second probe sleep ran to completion), so it is not used as evidence; the affected reports and the empty report directory are retained under ignored `artifacts/m4-cancel-live-<guid>/`. The second-interrupt forced-termination path has deterministic and synthetic coverage only and was deliberately not exercised live.

### Calibration method

Synthetic infrastructure calibration ran first: 18 samples (6 scenarios × 3 runs) covering immediate success, delayed completion, result-then-exit, result-then-remain-alive, a blocked read and a partial frame followed by a block, each with forced termination and cleanup confirmation. No real WMI or provider hang was induced.

Healthy-machine calibration then ran 6 sequential read-only collections as a standard (non-administrator) user on this unchanged laptop: no display, driver, service, permission or power change, no hardware attach/detach, no artificial CPU/GPU stress and no external display. Total durations were min 6311.7 ms, median 6777.9 ms, max 7412.4 ms; all six were complete and exit `0`. Observed cold/warm variation was a few hundred milliseconds without a stable order, and the sample is too small to separate cold from warm reliably. Timing records contain only safe stage/operation names and durations.

Measured healthy maxima used for policy selection:

| Segment | Max observed |
|---|---|
| `wmi.displayDrivers` operation | 4299.02 ms |
| Result transfer/validation (`wmi.displayDrivers`) | 4002.36 ms |
| `wmi.videoControllers` operation | 1459.75 ms |
| `display.activeTopology` operation | 595.20 ms |
| `wmi.computerSystem` / `wmi.operatingSystem` operation | 575.84 / 565.42 ms |
| Request transfer (max, topology) | 156.18 ms |
| Worker startup to Ready (max) | 110.88 ms |
| Cleanup total / exit confirmation (max) | 28.02 / 27.79 ms |
| Pending-I/O cancellation (max) | 0.07 ms |
| Deployment preparation / process creation (max) | 15.94 / 10.51 ms |

This is engineering calibration on one machine. It is not a p95, a p99, a statistically representative sample, or a statement about universal Windows performance.

### Selected timing policy

`CollectionTimingPolicy.CalibratedProduction`: overall budget 60 s, per-operation budget 10 s, cleanup allowance 2 s, later-operation reservation 10 s, final bookkeeping reserve 500 ms, connect budget 2 s, frame budget 8 s. Each value stays finite, never maps to `INFINITE`, and retains the existing absolute-deadline architecture with separate current cleanup, later active allowance and final bookkeeping reservations.

- The 10 s operation budget is more than double the slowest healthy operation (4.30 s) and still leaves meaningful time for later operations through the reservation arithmetic.
- The 2 s cleanup allowance and 500 ms bookkeeping reserve are roughly two orders of magnitude above the observed 28 ms cleanup and 27.8 ms exit confirmation, so normal engine overhead is absorbed without user-visible stalling.
- The 2 s connect and 8 s frame budgets bound handshake and frame transfer above the observed 0.11 s startup and 4.00 s result transfer.
- Margins are explicit multiples of measured maxima rather than a blanket multiplier, and no public timeout option was added. Users still observe only the overall bounded behaviour.

### Timing boundary tests

`ProductionTimingPolicyTests` (5 cases) uses a manual clock as the authoritative boundary proof: just-before acceptance succeeds and exactly-at-boundary acceptance times out (strict `now >= deadline`), production reservation arithmetic resolves the first of five operations to 9,500 ms and the second to 19,500 ms, all five operations complete, conditional driver omission releases the later reservation, and an exhausted overall budget skips later starts with zero attempts. Healthy measurements justify value selection only; they do not establish boundary correctness.

### Healthy-machine validation

`scripts/validate-m4-final.ps1` ran six consecutive exported read-only collections in one non-administrator process, with a five-second pause before the fourth run. All six returned exit `0`; durations were 10394, 8443, 8447, 10183, 8431 and 8323 ms (min 8323, median 8447, max 10394). Every run reported both GPUs, one `wmiSignedDriver` run with status `succeeded`, complete driver provider/version/date association for both controllers, one active internal path at 2560 × 1600 with the rational rate `74321400/450432`, an explicit `unknown/missingValue` monitor friendly name, and `redactedFields: 0`. Semantic output was consistent between runs apart from naturally variable timing, every schema/privacy/semantic check passed, and no stale worker, poisoned admission or surviving child process was observed. The earlier correction-phase validation directory also recorded 6/6 with the same semantics and an identical execution fingerprint. A small sample cannot prove zero resource leakage; what was measured is stated above.

Evidence is retained in ignored `artifacts/m4-final-validation-bef96858762e4c7a9d5246914748a53a/` (fresh) and `artifacts/m4-final-validation-58554e5b7f4240e8b3fb17d47b2c5a6e/` (first pass). `artifacts/m4-calibration-bad38113d63740a18358fb408ec7ee21/calibration.json` holds the synthetic and healthy timing samples.

### Privacy and export validation

Every fresh exported report contained `schemaVersion` 0.2.0, privacy policy 0.2 and `redactedFields: 0`, and the only identity-adjacent text present was the permitted GDI alias `\\.\DISPLAY1` and the evidence label `exactSetupApiInstanceId`. A pattern scan over all six exported reports found zero occurrences of raw PnP device-instance text (`PCI\`, `USB\`, `DISPLAY\`), extended device paths, `LUID`, `EDID`, `monitorId`, `deviceInstanceId` or `serial` markers. Live evidence therefore verifies report form without storing any raw identifier. Exact exclusion assertions continue to come from the existing synthetic marker tests, and no sensitive identifier was written to a validation log to demonstrate absence. Export files stay under ignored `artifacts/`.

### Schema and CLI validation

Schema remains 0.2.0 and no exit code was added. Fresh checks covered the healthy complete collection, existing controlled timeout/incomplete fixtures, the optional missing monitor-name fixture, a topology partial fixture and an attempts-zero fixture (M4 integrated schema helper: 8 passed). `./scripts/verify-cli.ps1` passed all seven production-path checks: complete healthy run `0`, invalid format and unscoped `--yes` and direct UNC destination `2`, redirected-input export refusal `4` with preview, and existing-file overwrite protection `5`. Controlled cancellation returns `3` with no preview, no export and no report fixture.

### Execution fingerprints

Before/after execution identities matched for all six validation runs, and the fresh baseline fingerprint list was identical to the correction-phase baseline, so no parent application input, runtime/dependency configuration, shared assembly, worker deployment closure, selected host identity or runtime/CoreLib identity changed during validation. The metadata-only worker probe performs no hardware collection.

### Fresh verification summary

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build **0 warnings, 0 errors** |
| xUnit | **264 total, 264 passed, 0 failed, 0 skipped** |
| Existing schema positive/negative checks | Passed against unchanged schema 0.2.0 |
| M4 integrated schema helper | 8 passed, 0 failed |
| M3 helper | 49 passed, 0 failed |
| M4 host admission probe | 1 passed, 0 failed |
| M4 deployment helper | 10 passed, 0 failed |
| Execution-fingerprint helper | 8 passed, 0 failed |
| `./scripts/verify-cli.ps1` | 7 passed, 0 failed |
| Live controlled cancellation | 3/3 cancelled runs exit `3`, no export, no survivors, plus 1 non-signalled control run exit `0` |
| Repeated healthy collections | 6/6 exit `0`, schema/privacy/semantic checks passed, fingerprints matched |

### Limitations

Only this laptop with a single active internal display path was exercised physically; external displays, clone, extended desktop, hot-plug, AMD, ARM64, RDP, virtual GPUs, eGPU and other machines remain unverified. Timing is one-machine engineering calibration, not a percentile or a hard real-time guarantee, and no real WMI/provider hang was induced. The second-interrupt forced-termination path is not exercised live. Local restore remains locked but skips vulnerability auditing, and the explicit audit action was not run. CI has not executed for this unpublished repository. No checkpoint commit was made.

### Source-of-truth records updated

`STATUS.md`, `ROADMAP.md`, `ARCHITECTURE.md`, `README.md`, `PRIVACY.md`, `SECURITY.md`, `docs/API-FEASIBILITY.md`, this record and [ADR 0007](decisions/0007-bounded-supervisor-and-worker-protocol.md) now describe the implemented cancellation contract, the selected calibrated timing policy, the scope of the live evidence and the remaining final-review gate. M1-M3 historical evidence was not rewritten.

## M4 integrated semantics/privacy corrections — 2026-09-21

The post-Gate-2 integrated review failed. Targeted F1-F6 corrections and bounded F7/F8 documentation/workflow reconciliation are now implemented locally. All M4 work remains unstaged and uncommitted. These successful checks support a targeted independent GPT integrated re-review; they do not constitute that review or M4 completion.

### Correction and re-review map

| Finding | Implementation | Durable regression evidence |
|---|---|---|
| F1: missing identities | `WorkerOperationDispatcher.Identity` normalizes null/empty/whitespace WMI identities to null while preserving rows; old mapping and strict Protocol semantics remain unchanged | `IntegratedBoundaryTests`: old/new null, empty, whitespace, mixed video/driver rows and unmatched topology |
| F2: actual attempt boundaries | Dispatcher emits WMI attempt 1 immediately before provider entry; `DisplayTopologyCollector` calls the progress seam at each existing retry boundary; `WorkerOperationRunner` sends each marker immediately | Integrated tests assert markers before provider/native entry, 1/2/3 topology attempts, controlled timeout before/after markers and callback transport failure |
| F3: omitted reservation | `SupervisedWindowsCollector` calls `CollectionSupervisor.OmitOperation` before topology; state machine decrements remaining operations without inventing an outcome or resetting the overall deadline | `SupervisorTests` uses synthetic policy arithmetic; integrated omitted-driver fixture preserves completion semantics |
| F4: outgoing ResourceLimit | Runner catches result-encoding ResourceLimit once and sends a fixed small `ResourceLimitFrame`; codec/sequence enforce its only allowed operation/state/reason/payload shape; supervisor applies the existing deadline-first failure path | `ResourceLimitFrameTests`, integrated oversized video/driver/topology/count tests, broken/undelivered fallback tests and supervisor deadline-equality test |
| F5: full path and privacy | Actual dispatcher → runner → wire framing/codec → production parent reconstruction → Core → shared privacy projection → JSON/Markdown | 35 `IntegratedBoundaryTests` cases; raw synthetic identities absent from Core before projection and both exports; six added integrated schema scenarios |
| F6: execution inputs | `execution-fingerprint.ps1`, M3 fingerprint wrapper/before-after checks and metadata-only worker execution-identity probe | `test-execution-fingerprint.ps1`: changed parent configuration/deps, host/runtime metadata, nested worker asset, mixed deployment and missing input; expanded M3 helper comparisons |

### Fresh verification

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build **0 warnings, 0 errors** |
| xUnit | **254 total, 254 passed, 0 failed, 0 skipped**; all previous 211 tests retained |
| Previous Gate 1 native/process tests | Passed, including 2 native layout, 9 native ownership and 10 synthetic process cases |
| Operation/equivalence/privacy tests | 6 existing Worker dispatch, 17 supervised collector and 35 new full-path integrated cases passed |
| Attempts/reservations/outgoing limits | Covered by integrated tests, 28 supervisor cases and 6 ResourceLimit frame cases; no actual provider hang induced |
| Existing schema positive/negative checks | Passed against unchanged schema 0.2.0 |
| M4 integrated schema helper | **8 passed**: previous timeout/incomplete plus WMI timeout, topology timeout, omitted driver, zero attempt, partial unmatched topology and optional missing name |
| M3 helper | **49 passed, 0 failed** |
| M4 host admission helper | **1 passed, 0 failed** |
| M4 deployment helper | **10 passed, 0 failed** |
| Execution-fingerprint helper | **8 passed, 0 failed** |
| `./scripts/verify-cli.ps1` | **7 passed, 0 failed** |
| Documentation/scripts/whitespace | **35** local Markdown link targets resolved; **15** PowerShell scripts parsed; **78** changed/untracked source and documentation files passed LF/trailing-whitespace/final-newline checks; generated lockfile formatting excluded; `git diff --check` passed |

Fresh logs, final `unit-tests.trx`, and categorized execution-input metadata are retained in ignored `artifacts/m4-integrated-corrections-c8ebf882b0df4bf79c82c45fde3384e5/`. Earlier local test evidence was preserved separately. An initial test analyzer error (`xUnit2031`) was corrected. Restricted execution then passed 242/252 intermediate cases and failed 10 native process cases because private-pipe access was denied. Authorized execution outside that command restriction passed; the final expanded suite passed 254/254. No application elevation, DACL weakening, containment change or system configuration change was used to obtain the pass.

Full-path tests use injected providers and controlled transport delivery cutoffs. They establish semantic/provenance behavior without claiming a real WMI hang or calibrated scheduling bound. The CLI script performs only existing harmless normal read-only collection on this unchanged laptop; it is incidental healthy-machine evidence, not broad hardware, controlled cancellation or timing validation. The execution-identity probe reports host/runtime metadata without entering collection.

Execution evidence now records parent deps/runtimeconfig, dependency-declared parent runtime/package assets, shared assemblies and manifest-bearing Supervisor, recursive worker closure including its deps/runtimeconfig, selected host path/version/hash and actual runtime version/directory/core-library identity/hash. It does not fingerprint all of .NET or persist hardware identities. M3 before/after comparisons remain; future summaries use fingerprint format 2. Historical M3 execution evidence was not reconstructed and retains its original coverage limits.

Local deterministic restore remains locked and skips NuGet vulnerability auditing; skipping audit does not prevent network package access. The explicit local `dev.ps1 -Action audit` action enables audited locked restore but was not executed in this task. Package versions are unchanged. CI still defines audited restore, with no executed CI evidence for this unpublished repository.

Full Ctrl+C integration, production timing calibration, broad live M4 validation, final review and checkpoint remain pending. Exact driver/topology correlation, M3 optional-name severity, schema 0.2.0, CLI exits and the shared privacy projection are unchanged.

## M4 Gate 2 — real collector integration, 2026-09-20

M4 Gate 1 passed. Gate 2 is implemented locally and awaits the integrated semantics/privacy review; it is not a final M4 checkpoint and has no timing calibration or live M4 hardware compatibility evidence.

The production CLI now composes `SupervisedWindowsCollector`. The five fixed worker operations are real:

1. `wmi.operatingSystem`
2. `wmi.computerSystem`
3. `wmi.videoControllers`
4. conditional `wmi.displayDrivers`
5. `display.activeTopology`

The parent runs them sequentially under one supervisor. The signed-driver worker is launched only after available nonempty video inventory. Completed independent facts survive later failures/timeouts, and the parent reconstructs the previous `CollectionSnapshot` shape and exact case-insensitive driver join without exposing transient identities. The topology worker executes the existing DisplayConfig → source/target metadata → SetupAPI resolver → exact WMI identity path and returns the complete existing topology run/displays semantics.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **211 total, 211 passed, 0 failed, 0 skipped** |
| M1–M3 regressions | Passed within the full suite |
| Gate 1 native/deadline/synthetic process tests | Passed within the full suite |
| Real worker dispatch tests | OS/computer/video/driver/topology mapping, provenance, transient identity, exact failure state and attempt count |
| Supervised orchestration tests | All-success order/assembly, video failure/driver omission, provider failure, exact/ambiguous/no-match/missing identity, video/driver/OS/computer/topology timeout preservation, unmatched/ambiguous/target-name partial topology, crash/malformed protocol failure, cleanup fatal, pre-cancellation |
| Privacy/export | Scripted typed-result parent → snapshot → privacy → export test passed. The subsequent integrated review found that this bypassed real dispatch/wire encoding; full-path coverage was added in the correction section above. |
| Schema | Existing positive/negative checks passed; integrated timeout and incomplete snapshots validated against schema 0.2.0 |
| M3 helper | 42 passed, 0 failed |
| M4 integrated schema helper | Timeout and incomplete fixtures passed |
| M4 host-process admission probe | 1 passed, 0 failed |
| M4 deployment helper checks | 10 passed, 0 failed |
| `./scripts/verify-cli.ps1` | All seven production-path checks passed; the normal read-only collection inside this check now uses the supervisor-backed path and is not timing calibration |
| Documentation/scripts/whitespace | 35 local Markdown targets resolved; 13 PowerShell scripts parsed; tracked diff and untracked whitespace checks passed |

The only real-machine evidence in this task is the harmless normal read-only collection performed by the existing CLI integration script through the new production path. No display mode, service, permission, driver, power, or hardware configuration was changed, and no hang was induced.

Production schema remains 0.2.0 and exit meanings remain `0/2/3/4/5`. Fatal host admission/deployment/cleanup failures stop preview/export; ordinary provider/timeout/protocol operation failures remain reportable incomplete results. Final Ctrl+C integration remains explicitly pending; the CLI currently has no new orderly interrupt handler.

## M4 final targeted Gate 1 corrections, 2026-09-20

This section records the final targeted corrections following the focused read-only re-review and supersedes only the earlier M4 correction test totals below. The first Gate 1 verdict remains FAIL until an independent final re-review; these local results are not a self-awarded pass.

The protocol trust-boundary semantic validator now treats the optional monitor friendly name as a special case only when the observation is exactly `unknown / null / missingValue`. Available names remain valid. Genuine target-name failures remain valid only with their appropriate failure state/reason and matching blocking `TargetName` issue: for example `failed / null / sessionAccessDenied` or `unsupported / null / notSupported`. Contradictory variants are rejected, including `failed / null / missingValue`, `unsupported / null / missingValue`, `unknown / null / notSupported`, and `failed / null / notSupported`. The rule is enforced in `ProtocolSemantics.MonitorName` at the protocol boundary and does not alter Core/M3 production behavior or generic `MissingValue` semantics.

Regression coverage was added at the direct decoder/validator, protocol sequence, and supervisor result-acceptance paths. The supervisor path rejects the contradictory result as a protocol failure rather than accepting it; direct decoder and sequence tests assert the intended `InvalidValue` semantic rejection. Legitimate target-name failure states are separately asserted to remain accepted.

The synthetic `delayed-completion` scenario now receives a deliberate 150 ms delay from `WorkerProcessTests`, and the test asserts that the result is observed after at least 100 ms before cleanup. This is a bounded synthetic timing check only; it is not timing calibration and does not route a real collector.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **188 total, 188 passed, 0 failed, 0 skipped** |
| Focused protocol/semantic/sequence/supervisor/synthetic-worker suite | 63 passed, 0 failed |
| M1–M3 preserved regressions | Passed within the 188-test run |
| Schema | Valid synthetic report accepted; contradictory observation and undeclared field rejected |
| M3 helper | 40 passed, 0 failed |
| M4 host-process admission probe | 1 passed, 0 failed |
| M4 deployment helper checks | 10 passed, 0 failed |
| Delayed completion | Nonzero 150 ms delayed child result observed after at least 100 ms and otherwise handled normally |

No real WMI or DisplayConfig worker collection, M4 hardware validation, timing calibration, Gate 2 work, schema change, CLI exit change, or production collector routing was performed.

## M4 Gate 1 targeted corrections, 2026-09-20

The first Gate 1 native/transport verdict was FAIL. F1-F13 corrections are now implemented and left unstaged for a separate read-only re-review. This section supersedes the original Phase 1 correctness claims below; historical passing tests did not establish the missing absolute-deadline, cancellation-ownership, recursive-closure or host-wide-poison guarantees. M1-M3 evidence below is unchanged.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **176 total, 176 passed, 0 failed, 0 skipped** |
| Deadline/state tests | 7 deadline/I/O and 24 supervisor cases: slow prefix/payload, strict equality, delayed/unresolved completion, one cleanup deadline, retained accepted facts, disposal/cleanup failure, host cancellation, 22-second skip/reservations and saturating finite arithmetic |
| Native layouts/ownership | 2 layout and 9 ownership/token cases: critical offsets, initialization/first-update/second-update failure, list/value teardown, parent-context rejection and read-only token verification |
| Protocol | 7 parser/sequence and 10 semantic cases: null/missing/wrong-source observations for all operations, contradictory topology, retry markers, zero-attempt failure, optional-name issues, partial state and missing inventory identity |
| Deployment | 7 cases: changed/missing nested dependency, extra asset, mixed assembly, normalized duplicates/traversal/MVID mismatch, parent-loaded shared identity, recognized runtime and quoting |
| Synthetic process | 10 cases: creation-time job/token/Ready checks, blocked/partial/malformed/exited workers, accepted-result shutdown, actual CreateProcess failure, repeated cleanup and unrelated inheritable sentinel exclusion |
| Existing collector/topology/privacy regressions | All 100 preserved pre-M4 tests passed: collector 21, topology 56, model/privacy/export 23 |
| Schema | 3 checks passed: valid synthetic report; contradictory observation and undeclared field rejected |
| M3 helper | 40 passed, 0 failed; fingerprints now include nested worker runtime files; no historical fingerprints reconstructed |
| M4 helper | Isolated host-process default-admission probe 1 passed; dependency-closure/copy/path/stale-output checks 10 passed |
| `./scripts/verify-cli.ps1` | 7 checks passed on the unchanged production path: help, invalid format, unscoped yes, UNC refusal, preview schema/no file, redirected export refusal and overwrite preservation |
| Documentation/scripts/whitespace | 35 local Markdown targets resolved; all 12 PowerShell scripts parsed; tracked diff and all 64 changed/untracked files passed whitespace/LF checks |

Fresh logs and TRX are in ignored `artifacts/m4-gate1-corrections-21bd928718fe40b494e48c13bd1572be/`. The previous rolling TRX was copied into an ignored `m4-gate1-correction-prior-*` directory before test runs. Synthetic deployment fixtures and CLI artifacts remain ignored. No M4 hardware run, real worker collector routing, timing calibration or broad compatibility claim was added. The CLI integration checks did invoke the existing pre-M4 read-only collectors; they are not M4 worker validation.

During implementation, regressions caught an extreme-duration decimal overflow and a fixed-size TOKEN_ELEVATION buffer-length error (Win32 error 24). Both were corrected before the successful run above. Restricted execution denied private-pipe access; authorized execution outside that restriction passed. This does not justify changing DACLs, privileges or elevation.

Tooling side effect: one intermediate direct SDK diagnostic test command, issued outside `dev.ps1`, printed the SDK first-run message that an ASP.NET Core HTTPS development certificate was installed. No trust command was executed, and no certificate was removed or modified afterward. Subsequent/final validation used `dev.ps1` with telemetry/certificate generation disabled and the configured local CLI home. This side effect is not a runtime feature or a validation prerequisite.

### Re-review map

| Findings | Implementation | Regressions |
|---|---|---|
| F1-F3 | `Deadline.cs`, `WorkerSession.cs`, `CollectionSupervisor.cs`, `HostAdmission.cs`, native pipe/process ownership | `DeadlineTests`, `SupervisorTests`, `WorkerProcessTests`, `test-m4-admission.ps1` |
| F4/F11 | `Native/ProcessAttributes.cs`, `SafeNativeHandles.cs`, `WorkerProcess.cs` | `NativeOwnershipTests`, `NativeLayoutTests`, inherited-event and failed-launch process cases |
| F5/F9/F12 | `WorkerDeployment.cs`, manifest generator, `worker-runtime-closure.ps1`, copy/fingerprint tooling, Ready/Start sequence | `DeploymentTests`, Ready mismatch supervisor cases, M3/M4 deployment helpers |
| F6/F8 | `ParentSecurityContext.cs`, admission categories, `CollectionSupervisorStateMachine.cs` | Token/fault tests, fatal-admission, cancellation, skip/reservation, cleanup and arithmetic cases |
| F7/F10 | Protocol codec, semantic/sequence validators, topology Run/Start DTOs and stub worker | `ProtocolTests`, `ProtocolSemanticTests` |
| F13 | ADR 0007, architecture/security/privacy/status/roadmap/API records and this evidence | Fresh suite, local documentation targets, PowerShell parsing and whitespace checks |

The managed pipe choice is justified by retained ownership and independently bounded completion observation, not by assuming cancellation completes immediately. An unresolved session is deliberately retained until host exit and the process cannot admit another worker through its default supervisor. Native scheduling latency and synchronous OS/deployment operations are not proven hard real-time; synthetic tests establish the specified supervisor waiting/acceptance and cleanup ownership behavior. Production policy remains uncalibrated. Current-user pipe access and hash/MVID comparisons do not protect against a hostile same-user process or concurrent malicious replacement of deployment files.

## Milestone 4 Phase 1 — original evidence, 2026-09-15 (superseded by corrections above)

Phase 1 was implemented against the frozen M4 contract and stopped at GPT Review Gate 1. Real WMI and DisplayConfig collection was not routed through the supervisor; the existing production path remains active.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit / TRX | 125 passed, 0 failed, 0 skipped |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared field rejected |
| M3/M4 helper checks | 39 passed, 0 failed; fingerprints now include parent and `worker/` runtime inputs with relative paths |
| Worker deployment | Embedded manifest generated; relative paths, lengths, SHA-256, duplicate/escape paths, missing files, and managed assembly identities validated |
| Native layouts | Process/job/security/startup/token structures and process-attribute identifiers checked for pointer-width layout |
| Synthetic process tests | Creation-time job membership, non-elevated token, inherited client-pipe handshake, timeout/partial-frame cleanup, complete-frame-then-alive shutdown, malformed frame, abnormal exit, and repeated launch/cleanup exercised |

Native checks cover `SECURITY_ATTRIBUTES`, `STARTUPINFO`, `STARTUPINFOEX`, `PROCESS_INFORMATION`, `JOBOBJECT_*`, token-elevation, and process-attribute identifiers. `PROC_THREAD_ATTRIBUTE_JOB_LIST` is supplied as a pointer to the job handle and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` as a pinned handle array; both raw inputs remain valid until the attribute list is destroyed. `CreateProcessW` uses creation-time job assignment with no create-then-assign fallback.

The private endpoint is created through `CreateNamedPipeW` with first-instance/one-instance byte mode, an explicit current-user DACL, and remote-client rejection. The natively opened overlapped server handle is wrapped in the framework asynchronous pipe stream so parent connect/read/write and cancellation complete through the supported overlapped implementation. The worker receives only the inherited client handle as standard input/output; the parent closes its extra copy after successful creation, and cleanup confirms process exit before allowing another worker.

Protocol v1 uses a four-byte little-endian length followed by strict UTF-8 JSON and the sequence request → ready → start → attemptStarted → result. Tests reject malformed/truncated UTF-8/JSON, duplicate/unknown properties, wrong version/kind/operation, invalid ordering, excessive depth/strings/collections, contradictory result state/payload combinations, and topology references to unknown GPU labels. Resource-bound rejections map to `ResourceLimit`; no truncation fallback was added.

The deadline state machine is deterministic and uses explicit monotonic timestamps in tests. It covers result just before deadline, equality selecting timeout, late results, late timeout callbacks, startup expiry, total-budget reservation, skipped starts, exactly-one terminal outcome, cancellation precedence, cleanup-budget exhaustion, poisoned admission, and preservation of an already completed operation when a later operation fails. Production timing values remain `UncalibratedProvisional` and are not used by the normal CLI path.

Named-pipe/child-process tests require host access outside the restricted command sandbox. Inside that restriction, endpoint creation returned `accessDenied`; the same tests passed outside the restriction. This is recorded as an execution-environment limitation. No M4 hardware run, production worker routing, timing calibration, final Ctrl+C integration, or broad compatibility claim was added.

## Authorized M3 local checkpoint, 2026-09-13

Final targeted read-only review passed; all three required pre-checkpoint findings are closed. Checkpoint preflight confirmed M2 HEAD `d3eaaea347f8c63d67cac493a9640e2baf2c1974`, unchanged M1/M2 history and baseline, no remote, an empty index, usable owner-configured Git identity, and exactly 18 reviewed changed paths. Source, tests, tooling, documentation, Git configuration, and all 21 historical M3 report/summary files matched final-review hashes. Only minimal checkpoint wording in `STATUS.md`, `ROADMAP.md`, and this section is changed during checkpoint execution.

This section belongs to M2's direct successor, `Milestone 3: harden topology validation and optional metadata handling`, which introduces `scripts/validate-m3.ps1`. The single local M3 checkpoint contains 18 changed paths and a 62-file repository snapshot; M1 and M2 remain unchanged. This identification avoids a self-referential commit hash or an extra bookkeeping commit.

| Fresh checkpoint check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| xUnit / TRX counters | 100 passed, 0 failed, 0 skipped; privacy/export and native layout/union regressions passed |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared field rejected |
| M3 helper regressions | 31 passed, 0 failed, including decoded privacy, diagnostic/reference, redaction, and application-fingerprint checks |
| `./scripts/verify-cli.ps1` | All seven integration checks passed; preview permits exit 0 or 3 and does not establish fresh hardware completeness |
| Documentation / PowerShell / whitespace | All 30 repository-local Markdown links resolved; four affected scripts parsed without errors; staged whitespace check passed |

Fresh logs and TRX are retained under ignored `artifacts/m3-checkpoint-e2081fb8fa9c441dae27eb1619376f76/`; the previous rolling TRX was copied there before the normal workflow refreshed it. Historical physical reports remain ignored and unchanged; the six-run protocol was not repeated for this checkpoint. Their provenance limitations below remain applicable, and no historical fingerprint was reconstructed. Real reports, local summaries/logs/TRX/fingerprints, SDK/cache files, and build output are excluded from the commit. No remote, publication, tag, release, or M4 work was introduced.

## M3 pre-checkpoint review corrections, 2026-09-13

The three required review findings were corrected only in validation tooling, synthetic tests, and associated documentation. Production source, collector severity, exact correlation, privacy projection, native declarations, schema 0.2.0, and CLI behavior were unchanged from correction preflight. `BlocksCompletion()`, recovery/shrink coverage, and exact rational-pair matching were left as reviewed.

Future `validate-m3.ps1` protocols discover application-owned runtime assemblies from the Release CLI dependency manifest: `wingpudoctor.dll`, `WinGPUDoctor.Core.dll`, and `WinGPUDoctor.Windows.dll`. SHA256 fingerprints include filename, project-library identity, and byte length. The summary persists the initial set, before/after sets and match results for every run, timestamps, protocol parameters, and the administrator-token/guard result. A mismatched pre-run build is not collected. Package/framework assemblies are not fingerprinted. These are future validation records, not retroactive evidence for older runs.

The helper validates decoded JSON strings with the documented dedicated-field GDI exception, requires the diagnostic for an unknown/missing name, rejects substantive or malformed diagnostics, requires each adapter reference to resolve to exactly one GPU, and rejects unexpected redaction for this known-laptop protocol. These stricter checks apply to the validation helper, not universal product behavior. Narrow shared functions are exercised by `scripts/test-m3-validation.ps1`, now included in `scripts/dev.ps1 -Action test`.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore and Release build passed; 0 warnings, 0 errors |
| xUnit | 100 passed, 0 failed, 0 skipped; includes strengthened M3 export marker assertions and existing native layout/union checks |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared identity field rejected |
| M3 helper deterministic checks | 31 passed, 0 failed; schema-valid escaped PCI/USB/device/monitor identifiers, prohibited GDI placement, serial/LUID/EDID labels and UUID rejected; Unicode escaping also rejected |
| Diagnostic/reference negatives | Missing diagnostic/structure, substantive issue, native failure on the name diagnostic, duplicate GPU IDs, dangling source/target references, unavailable relationship, GPU/driver redaction and redaction warning rejected |
| Build identity checks | Exactly three application assemblies discovered; unchanged fingerprints accepted; separate CLI/Core/Windows hash changes and missing assemblies rejected |
| `./scripts/verify-cli.ps1` | Seven integration checks passed: help/argument/destination checks, preview/schema/no default file, export refusal, and existing-file protection; preview accepts complete or incomplete collection and is not a new hardware-completeness claim |
| Documentation | All 30 repository-local Markdown links resolved |
| PowerShell parsing | No parse errors in the live helper, shared checks, synthetic tests, or development script |
| Whitespace and preservation | Tracked diff and untracked trailing-whitespace checks passed; all 21 historical M3 report/summary files, production source, index, HEAD file, and Git configuration matched correction-preflight hashes |

Fresh build/test/CLI logs and a copy of the fresh TRX are retained under ignored `artifacts/m3-correction-041a6fab1bc940339a551ab9d6252a31/`. The normal workflow refreshed its rolling `artifacts/test-results/unit-tests.trx`. Existing M3 physical report/summary files were preserved byte-for-byte. Offline application of the corrected report checks accepted all six reports in each successful folder (`73d5...` and `c6aa...`) and rejected all six in the failed folder (`9867...`). This is reinspection of preserved data, not new physical collection. The six-run protocol was not repeated because production behavior did not change.

## Milestone 3 — initial single-laptop hardening results, 2026-09-13

M3 was authorized as bounded single-laptop validation and hardening. The only approved externally visible correction was the severity of an empty optional monitor friendly name: the name remains explicit `unknown/null/missingValue` with a `targetName/missingValue` diagnostic, but that condition alone no longer makes the display run partial, adds `CollectionIncomplete`, or changes CLI exit to `3`. Substantive source/target API, mode, adapter-correlation, target-availability, provider, retry-exhaustion, and other required collection failures remain incomplete (exit `3`). Export refusal remains exit `4`; export failure remains exit `5`. Privacy removal remains explicit redaction with `ValuesRedacted` and the removed-field count; it does not by itself change collection completeness.

Deterministic validation after the change:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build completed with 0 warnings and 0 errors |
| Deterministic tests | 100 passed, 0 failed, 0 skipped (previous 86 plus 14 M3 cases/theory cases) |
| Privacy/export regressions | Passed, including the missing-name unknown representation, no `collectionIncomplete`, no raw identifier leak, and multi-path relationship preservation |
| Native layout/union regressions | Passed |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared identity field rejected |

The new deterministic coverage verifies the non-blocking name-only case; substantive mode, adapter, target-name API, and independent WMI failures still win; a later substantive issue remains the summary reason when the name diagnostic is first; retry growth, shrink, and exhaustion; mode/path count guards; repeated collector use without stale facts; per-path metadata isolation; and multi-path privacy references. The implementation adds no schema field or version change.

Live repeatability protocol:

```powershell
./scripts/validate-m3.ps1
```

The original helper performed six fresh CLI collections in two batches of three, paused 30 seconds between batches for ordinary desktop use, checked only the CLI assembly hash for changes, enforced a non-administrator guard, and retained reports plus `summary.json` under ignored `artifacts/`. Its final summary reported no CLI hash-change failures. It did not fingerprint Core/Windows or persist any hash/token result, so unchanged full-application content cannot be independently established from those records. Its schema, topology, rate, exit and warning checks supported the observations below, but review identified false negatives in serialized-text privacy and diagnostic/reference checks. The correction section above records the stronger checks; the old summaries were not altered.

Final live result: six of six runs exited `0`. Every run reported one available internal path, source and target matched the same inventory GPU, source resolution was 2560 × 1600, rotation was identity, scan ordering was progressive, and path/signal rates were `74321400/450432`. The friendly name was consistently absent (`unknown/missingValue`) and the only display issue was `targetName/missingValue`; there was no `collectionIncomplete`, retry, schema, privacy, or reference failure. Reports are under ignored `artifacts/m3-validation-73d5a793ae024f2c9799cf0e51e29497/`.

An earlier execution inside the reported restricted command context produced six runs with substantive WMI `accessDenied` failures and unmatched adapter joins. Those anomalous reports are preserved under ignored `artifacts/m3-validation-98677acd3c6247af8c126341acf58a6d/`. Later successful protocols are preserved under `artifacts/m3-validation-c6aa277f362c4cd085184be8602f8ae3/` and the final folder above. The evidence favors an execution-context restriction, but that explanation is plausible, not fully proven. A rebuild occurred between the initial failed and final successful protocols; identical executable content across them cannot be independently proven because application fingerprints were not preserved. The original helper's guard supports the reported non-admin execution, but its result was not persisted. No historical hash was reconstructed, and the failed runs remain failures rather than a product pass or broad compatibility result.

Not verified by M3: external, clone/extend, hot-plug, AMD, ARM64, RDP, virtual-adapter, or second-machine behavior; hard WMI cancellation; remote CI; interactive `EXPORT` entry; security audit; or broad hardware compatibility. The six-run result applies only to this unchanged laptop/internal-display setup.

## Authorized pre-M3 local Git checkpoint, 2026-09-13

Preflight confirmed usable owner-configured author/committer identity, unborn `main`, no remote, the unchanged 48-file M1 index, and no staged ignored files. `git diff --cached --exit-code refs/baselines/milestone-1 --` passed. Comparison with the saved M2 review index showed only the documented management integration: four modified documents plus `AGENTS.md` and `STATUS.md`.

M1 commit `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee` (`Milestone 1: validated inventory foundation`) was created from the existing index without additional staging. Its tree is exactly `fcc13aed94b20fab57acad9749fac1700ba789b3`, matching the preserved baseline. All 58 working source/project/documentation files were byte-identical across that commit; all M2 and management changes remained outside it.

The M2/management checkpoint is the commit that introduces `STATUS.md`, with message `Milestone 2: active topology and repository handoff`, directly after M1. Its 33 explicitly selected changed/added files complete the 58-file repository snapshot. This self-identification avoids embedding the commit's own hash or creating a third bookkeeping commit. Only `STATUS.md` and this dated section changed during checkpoint bookkeeping; the other 56 files, including implementation, tests, schemas, ADRs and tool version, were unchanged from preflight.

Fresh validation before the M2 commit:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| Deterministic suite / existing TRX counters | 86 passed, 0 failed, 0 skipped; includes privacy/export and native layout/union regressions |
| Existing synthetic schema checks | Valid example accepted; contradictory observation and undeclared field rejected |
| Execution token | Administrator role false; no elevation to administrator |
| Explicit staged manifest / `git diff --cached --check` | 33 intended M2/management files; whitespace check passed; no staged ignored files |
| Documentation links | All 27 repository-local Markdown links resolved |

Build/test output remains ignored; no real report, raw identifier dump, secret, SDK/cache, or generated validation artifact is part of either commit. Existing synthetic fixtures and historical validation summaries are retained. No hardware smoke, live CLI integration, M3 repetition protocol, graphics setting change, or fresh compatibility claim was added. M3, including the friendly-name severity correction, remains unstarted. No identity/configuration, baseline-ref, remote, publication, tag, or release change was made by the agent. Historical evidence below is preserved.

## Source-of-truth management checkpoint, 2026-09-10

Documentation integration at the completed M2 checkpoint; no M3 implementation or live hardware collection. Added the stable agent manual and one current status file, connected existing entry points, and aligned the future M3 roadmap with the owner's single-laptop constraint. Source, tests, schemas, scripts, dependencies, accepted ADRs, and historical M1/M2 evidence are preserved.

Verified locally in this checkpoint:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| Deterministic tests | 86 passed, 0 failed, 0 skipped; TRX counters confirmed in `artifacts/test-results/unit-tests.trx` (refreshed by the existing script) |
| Synthetic schema | Valid report accepted; contradictory failed-with-value observation and undeclared identity field rejected |
| Documentation | All 27 repository-local Markdown links resolved; focused `git diff --check` and new-file trailing-whitespace checks passed |
| Change scope | Exactly four existing documents changed and `AGENTS.md`/`STATUS.md` added; the other 52 original source/project files were byte-identical by SHA256, including source, tests, schemas, scripts, dependencies, and ADRs |
| Git/baseline integrity | SHA256 unchanged for the index, HEAD file, repository config, M1 baseline ref, and M1 archive; Git status preserved every existing entry and added only the two new untracked management files |

The build used the existing local SDK/cache and required no dependency or configuration changes. No live hardware/CLI integration run, interactive `EXPORT` check, external-display test, remote CI run, or broader compatibility/security validation was performed here. Previous live results below remain dated evidence, not refreshed observations. The optional-friendly-name partial/exit `3` behavior remains unchanged; its review belongs to the unstarted M3 scope. No test or code change was needed for this documentation task.

Git history limitation: `main` has no commit. The M1 reference is a tree object, with the M1 index and archive retained; M2 has no commit checkpoint. The existing local M2 review patch is historical and does not include this management integration. No staging, commit, identity/configuration, baseline-ref, remote, or publication change is authorized by this documentation checkpoint.

## Milestone 2 — current local results, 2026-09-10

Before changing M1, its documented workflow was rerun: Release build had 0 warnings/errors; all 41 tests and schema checks passed; local documentation links were valid. The only baseline whitespace findings were pre-existing extra blank lines at the end of `.editorconfig` and `.gitignore`; those files were preserved unchanged.

Git identity was not configured. Per the user's condition, no commit or identity change was made. The unchanged 48-file M1 state was staged and saved as tree `fcc13aed94b20fab57acad9749fac1700ba789b3`, with a local baseline reference and archive `artifacts/baseline/milestone-1.zip`. Archive SHA256: `B100BF433A8A4D850BAB263510BEB65E66B68BA298FB54C308357AB78B41589E`. M1 test evidence is retained in `artifacts/baseline/milestone-1-tests.trx`. M2 edits remain separate from the index; new files remain untracked. No remote or publication exists.

| Final check | Result |
|---|---|
| Documented locked restore / Release build | Passed; 0 warnings, 0 errors; no new package dependency |
| Deterministic tests | 86 passed, 0 failed, 0 skipped; all 41 M1 cases retained, plus 45 new cases |
| Native layout | Size/offset checks for CCD packets, union and SetupAPI records passed; independent binary union fixture decoded correctly |
| Matching/topology fixtures | One/two adapters, internal/external, multiple targets, clone/extend, separate source/target adapters, unmatched and ambiguous identities passed |
| Failure fixtures | Unsupported/missing API, session access denial, missing names, bad modes, buffer retries/exhaustion, excessive counts, unavailable target, and partial failures passed |
| Privacy fixtures | Native LUIDs, adapter/monitor paths, PnP IDs, EDID identifiers and injected unsafe labels/text excluded from JSON/Markdown; GDI alias accepted only in its dedicated field |
| Schema 0.2 | Live and synthetic reports accepted; contradictory observations and undeclared fields rejected |
| Final live collection | Non-administrator token (`False` for administrator role), active path collected and uniquely matched; exit `3` solely because friendly name was missing |
| CLI behavior | JSON-only preview, no default file creation, argument/UNC rejection, unconfirmed export refusal, and existing-file hash protection passed |

Only the M1 schema-version assertion changed to 0.2.0; no M1 test was removed. The first new privacy test used an over-broad substring check that matched the safe evidence label `exactSetupApiInstanceId`. It was corrected to prohibit actual instance-ID properties and values while retaining the evidence label. Generic native errors were also kept generic rather than classified as confirmed topology changes.

### Observed topology

On this Windows 11 x64 machine (numeric version `10.0.26200`), the final query reported:

| Fact | Observation |
|---|---|
| Video-controller inventory | NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics |
| Active paths | One, target available |
| Source and target adapter | Both matched Intel Graphics (`gpu-2` in that report) |
| Correlation evidence | Exact unique SetupAPI device-instance / WMI PnP-ID equality |
| Connector | `internal`, directly reported by DisplayConfig |
| Source resolution | 2560 × 1600 pixels |
| Path refresh | `74321400/450432` Hz, approximately 165 Hz |
| Target signal VSync | Same rational rate in this snapshot |
| Rotation / scan | Identity / progressive |
| Windows 11 refresh-boost flag | False |
| Monitor friendly name | Unknown (`missingValue`); no substitute name invented |
| Query | Active paths with virtual-mode and virtual-refresh awareness; one attempt |

This supports active internal display-path ownership by the correlated Intel adapter at collection time. It does not establish application rendering, iGPU/dGPU classification, hybrid/Optimus state, electrical MUX position, GPU workload/power, or why another adapter appears in inventory. No active path mapped to the NVIDIA inventory entry in this snapshot; that does not mean NVIDIA was idle or off.

The missing friendly name is a permitted API outcome, not proof of an interop defect or disconnected display. Native struct layouts and other returned fields passed checks. Names and correlation data are read separately from the successful path/mode query and may race with hot-plug. All live results and report files are local/ignored; published examples remain wholly synthetic. `artifacts/milestone-2-live.json` retains the first successful M2 topology run, with later checks under `artifacts/hardware-smoke-*.json`.

### Review and reproduction

```powershell
./scripts/dev.ps1 -Action test
# Explicit live read-only checks, separate from unit tests:
./scripts/hardware-smoke.ps1
./scripts/verify-cli.ps1
# Existing-file differences from the staged M1 baseline:
git diff
# Newly added M2 source/schema/ADR files:
git ls-files --others --exclude-standard
```

`artifacts/milestone-2.patch` is a complete review patch, including new M2 files, relative to the baseline tree. It is generated with a temporary Git index so the real index continues to hold M1. No global/local Git identity is invented. No M3 implementation has begun.

### Remaining gaps and manual checklist for a future validation milestone

Only the current local display configuration has been physically validated. Synthetic tests cover other shapes, not broad hardware compatibility. No external/clone/MST/RDP/ARM64/AMD/virtual-adapter hardware test, GPU power measurement, packet trace, global configuration audit, or security audit was performed. Standard-user success was verified locally; RDP/access-denied failures were simulated, not induced by modifying Windows access policies. DisplayConfig access failures can be session limitations; elevation is not an assumed remedy. WMI can still hang beyond its nominal timeout. Friendly-name redaction is not a guarantee of anonymity.

For later manual testing, only when the user chooses to do so:

1. Save a reviewed current report. Record the user's visible configuration without collecting names/serials from unrelated system data.
2. If an external monitor is available, have the user connect it and choose Extend, then collect. Check distinct endpoints and exact adapter evidence; do not expect an adapter based on port brand or enumeration order.
3. If desired, have the user choose Duplicate and collect. Check shared-source/clone relationships without forcing a one-source/one-target model.
4. Let the user disconnect/reconnect the display, then collect. Check bounded retry diagnostics, target availability, and preservation of other facts. Do not automate mode changes to force a race.
5. In an available remote session, collect once and record either supported output or explicit session/access limitation. Do not elevate to conceal the limitation.
6. If the user independently chooses a different graphics mode later, compare only the reported paths/identities; do not infer workload, MUX mechanism, or a cause from the difference alone.

### Material files changed

Windows: new `DisplayConfigNative.cs`, `SetupApiAdapterResolver.cs`, `DisplayTopologyCollector.cs`, and test visibility; `WindowsCollector.cs` composes the new collector. Core: topology facts/metadata, new `TopologyPrivacy.cs`, privacy projection and Markdown rendering. CLI: live composition, version/help, incomplete-status handling. Tests: new `TopologyTests.cs`, two composition tests, and the schema-version assertion. Schema/examples/scripts now exercise 0.2.0. README, architecture, privacy, feasibility, schema guide, roadmap, security/contribution notes, validation, and ADR 0005 record the final behavior. M1 dependencies/license and the two original whitespace-only files remain unchanged.

## Milestone 1 — historical evidence

Date: 2026-09-10. Results below are local observations, not a broad hardware compatibility claim. Source review references are in `API-FEASIBILITY.md`.

## Verified

| Check | Result |
|---|---|
| Release solution build | Passed, 0 warnings / 0 errors |
| Deterministic xUnit tests | 41 passed, 0 failed, 0 skipped |
| Synthetic JSON round-trip | Passed through the actual privacy boundary and writer |
| JSON Schema | Synthetic and live report accepted; contradictory failed-with-value observation and undeclared hostname rejected |
| Non-administrator live collection | Passed with process administrator-token check `False`; final collector exit `0` |
| Live GPU inventory | Two video controllers returned; names, PCI type IDs, matched providers/versions/dates available |
| Same-provider plumbing check | Reported GPU count agreed with subsequent narrow CIM query; not independent hardware confirmation |
| CLI help and invalid arguments | Passed; invalid format, standalone `--yes`, and direct UNC destination rejected |
| Default JSON preview | Valid report on stdout; no files created in the isolated check directory |
| Unconfirmed export with redirected stdin | Exit `4`, preview shown, no file created |
| Explicit local export | Same sanitized report model previewed and saved under ignored artifacts; schema passed |
| Existing-file protection | Exit `5`; existing synthetic target hash unchanged |
| Local Git foundation | Initialized `main`; no commits, remote, upload, or publication |
| Ignore rules | Project-local SDK and real reports under `.tools/` and `artifacts/` ignored |
| Narrow source inspection | No application network calls, account-name queries, provider method calls, configuration setters, or privilege enablement found in source; not a dynamic security audit |

The deterministic suite covers observation invariants and unavailable-state serialization, provenance, explicit empty versus failed inventory, conservative findings, exact driver matching, ambiguous/missing joins, independent query failure, missing fields, PCI parsing, date parsing, whole-field redaction, markup/control text, numeric version false positives, and preserving the original snapshot.

## Live-check scope

One Windows 11 x64 computer, numeric version `10.0.26200`, was checked in its current configuration. WMI reported an NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics. This validates that these controller records and matched driver facts are retrievable here with a non-administrator process. It does **not** establish iGPU/dGPU roles, physical routing, current hybrid/MUX mode, GPU activity, electrical sleep, or actual application rendering. No graphics-mode/driver/power-setting changes were made to test scenarios.

The first live run exposed a real parser limitation: `Win32_PnPSignedDriver.DriverDate` returned DMTF strings with `******+***` in the subsecond/timezone positions. The initial strict parser returned `unknown/invalidValue`, causing a partial result. After inspecting only that field, date-only parsing was corrected to preserve known calendar dates while allowing unspecified time precision. Four regression cases were added. The final run had complete implemented fields. The original partial report remains in ignored artifacts as evidence, not as a publishable example.

## Toolchain and environment

The machine initially had .NET runtimes but no SDK. A portable official SDK 10.0.401 was downloaded into `.tools/dotnet`, with the Windows x64 archive checked against Microsoft's release-metadata SHA512:

```text
24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430
```

Source: `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`; SDK archive: `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip`. The runtime dependency is `System.Management` 10.0.12; lock files capture its transitive dependencies and test dependencies.

The first SDK invocation unexpectedly generated an ASP.NET development certificate in the current-user personal certificate store. Its exact thumbprint and creation time were matched to this SDK's first-use sentinel; only that certificate was removed and absence was verified. No certificate trust action was performed. Subsequent commands and the supplied script set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`. SDK telemetry was disabled for all SDK executions that performed restore/build/test. No system SDK installation or persistent PATH change was made.

The restricted execution environment stalled an initial multi-worker build. Only processes belonging to the project's portable SDK path were stopped. The final build used a single worker with shared compilation disabled outside that sandbox, retaining a non-administrator token. This was a build-environment limitation, separate from the one ordinary compile-name ambiguity fixed before successful builds.

## Reproduce

```powershell
./scripts/dev.ps1 -Action test
# The following are opt-in hardware checks, separate from the deterministic suite:
./scripts/hardware-smoke.ps1
./scripts/verify-cli.ps1
```

`artifacts/test-results/unit-tests.trx` holds local test results. Hardware reports and CLI results are in ignored artifacts subdirectories. `examples/report.example.json` and `.md` are entirely synthetic and use the same exporters as the CLI.

## Not verified / limitations

- GitHub Actions has been defined but has not run remotely; the project is not published.
- Interactive terminal entry of `EXPORT` was not manually exercised; explicit `--yes`, redirected-input refusal, same-snapshot rendering, and overwrite refusal were checked.
- No second physical machine, AMD GPU, eGPU, disabled/disconnected GPU, virtual/indirect display, headless system, RDP, clone/MST topology, Windows on ARM, or older Windows validation.
- No DisplayConfig/DXGI implementation or native topology test yet. Classification is intentionally unsupported.
- No independent Device Manager/SetupAPI driver-value confirmation, electrical power measurement, GPU wake-up experiment, packet trace, full configuration-diff audit, fuzzing campaign, or security audit.
- Expected WMI access/provider/timeout failures were tested with fakes, not induced by changing Windows services or access policy. There is no guaranteed total timeout for a hung provider.
- Regex filtering cannot guarantee anonymity of arbitrary OEM/provider strings; preview remains necessary. Fixed-drive output can still be redirected/synchronized by the filesystem or other software.

## Next bounded milestone

Follow `ROADMAP.md`: active display topology only, using DisplayConfig plus minimum DXGI adapter mapping, explicit unknowns, race handling, local identifiers, and standard-user tests. Application activity, vendor modules, GUI, and remediation remain out of scope.
