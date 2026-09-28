# WinGPUDoctor v0.2.0 release notes (draft)

> **Draft: not published.** The package below is candidate 4, built from clean build output and validated locally on 2026-09-28. Still open before release: a final review. On 2026-09-28 the maintainer moved a trial without the .NET 10 Desktop Runtime and usability sessions with people who don't use the console to after the release; both are listed under "Not verified". Tagging and publishing need separate owner authorization after the final review; if the package is rebuilt, its size and SHA-256 must be updated here.

WinGPUDoctor reads what Windows reports about graphics adapters, drivers and active display paths, explains it, and produces a report that you review and save yourself. Version 0.2.0 adds a desktop app for people who don't use a console.

## What's new

- **Desktop app, `wingpudoctor-gui.exe`.** Start a scan (which you can cancel), read the results as plain-language cards with ⓘ explanations and optional technical details, then review the exact report text and save it as Markdown or JSON. The Welcome screen says what the report is for: someone helping you with your graphics or display gets the report, which you save and send yourself.
- **Wording when a reading step doesn't finish in time.** If a reading step isn't completed within the scan's time limit, the values it would have read show "Didn't finish in time" instead of the general "Couldn't be read", and the summary suggests scanning again, with a Scan again button right under it.
- **Saving follows the command-line rules.** The text you review is exactly what is saved; reports are saved only to a folder on a fixed drive of this PC (direct network paths and removable drives are refused, but a folder there that links or syncs elsewhere is not detected; see `PRIVACY.md`), existing files are never replaced, and the app suggests choosing a folder that OneDrive, Dropbox or a similar app doesn't sync. Copying from the preview is blocked, and the saved file is exactly the preview you saw.
- **Accessibility and appearance.** The app follows the Windows dark, light and high-contrast themes and the Windows "Animation effects" setting, works with the keyboard, and gives its controls UI Automation names. It uses only built-in Windows fonts and icons, with no vendor logos.
- **One package.** `WinGPUDoctor-0.2.0-win-x64.zip` contains the app, the command-line tool `wingpudoctor.exe` and one shared `worker` folder in a single folder (ADR 0008, decision 6), plus the README's screenshots in `docs/images`.
- **Packaging checks.** Packaging fails if a packaged file carries a local build or user-profile path, if an executable is not the SDK's own launcher with only the SDK's edits, if a runtime file that an executable declares is missing, if packaged code references .NET networking or imports a Windows networking library, or if the output folder is reached through a junction or link.

## Unchanged

- The report schema stays 0.2.0 and the privacy policy stays 0.2. Only `toolVersion` changes, to 0.2.0.
- The command-line tool's arguments, output and exit codes are unchanged; `--help` now shows 0.2.0.
- WinGPUDoctor stays read-only: no automatic fixes, setting changes, elevation requests, telemetry, network client or uploads.

## Requirements

- 64-bit (x64) Windows. Tested in depth on one Windows 11 x64 laptop; the maintainer also ran a limited test of candidates 2 and 3 on a second laptop, whose Windows version was not recorded.
- The [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) for the app. It includes the .NET 10 Runtime that the command-line tool needs; to use only the command-line tool, the .NET 10 Runtime (x64) is enough.
- No installer and no administrator rights: extract the ZIP and open `wingpudoctor-gui.exe`. The README has a step-by-step guide.

## Not code-signed

The executables are not code-signed, so Windows can't show a verified publisher: if SmartScreen shows "Windows protected your PC", it names the publisher as "Unknown publisher". Before opening the app, compare the ZIP's SHA-256 with the value below and with the first 64 characters of the attached `.sha256` file (the rest of its line is the file name):

```powershell
(Get-FileHash .\WinGPUDoctor-0.2.0-win-x64.zip -Algorithm SHA256).Hash
```

| File | Size | SHA-256 |
|---|---|---|
| `WinGPUDoctor-0.2.0-win-x64.zip` | 1,248,708 bytes | `7b28ef3b3e2566b6785287d9522b34f8eb767f8717e6974a91f9b0b2e82a297c` |

A matching checksum shows that the download is complete and unchanged; it can't prove who built it. If it matches, select **More info**, then **Run anyway**. If it doesn't, delete the ZIP and download it again.

## What it can't tell you

A scan shows what Windows reports. It can't tell you which graphics adapter a game or app uses, how busy an adapter is or its power state, whether hybrid graphics or a display mode switch (MUX) is active, whether a driver is up to date, whether the PC, an adapter or a display is healthy or working correctly, or which physical port a display is plugged into. Inactive displays are not listed. Review every report before sharing it: model and device names can be distinctive, and automatic filtering can't guarantee anonymity.

## Verified so far

Details and dates are in `docs/VALIDATION.md`.

- **Deterministic checks** (2026-09-28, after a clean build of the candidate commit): Release build with 0 warnings and 0 errors, 383 xUnit tests, the schema and helper checks and 61 package-layout checks passed. The dependency audit (same lock files) found no known vulnerable direct or transitive NuGet package.
- **Release package:** built after deleting all build output, from the candidate commit with a clean working tree; the packaging checks (local paths, launcher check, declared runtime files, .NET networking references and listed networking imports, entry hashes) passed, and an independent rescan of the ZIP found nothing.
- **Command-line tool:** compared with 0.1.0 on every path that returns before collection (help, invalid or duplicate arguments, rejected destinations), the output and exit codes are identical apart from the version in `--help`.
- **From the extracted candidate 4, live on the first laptop (Windows 11 x64), non-administrator, dark mode** (2026-09-28): the command-line checks (help, preview, typed `EXPORT`, typed decline, `--yes`), and the app's scan, cancel while reading, closing during a scan, and Markdown and JSON saves byte-identical to the preview, with no worker left running.
- **On a second laptop with integrated graphics only** (a limited test of candidates 2 and 3 by the maintainer): the downloaded ZIP triggered SmartScreen and ran after **Run anyway**; light and dark modes displayed correctly. On a first scan, one reading step did not finish (the driver step with candidate 2; Windows version and build with candidate 3, while Windows Security reported a Microsoft Defender cloud scan); a second scan read everything. The cause was not confirmed.
- **From the extracted candidate 2, live on the first laptop, non-administrator, dark mode** (2026-09-27; changed since then: the app's wording, including how a timed-out value is shown, a Scan again button under the summary after a timeout, and the packaged documents; unchanged: the command-line tool, the worker, the collection code and the packaging scripts):
  - Command-line tool: `--help` shows 0.2.0; Markdown and JSON previews create no file; export after typing `EXPORT` in a console and with `--yes` saved a report that passed the schema and privacy checks; typing another answer, or redirected input without `--yes`, declined with exit code 4 and no file; an existing file was not replaced.
  - App, launched directly and through Explorer: scan, results, cancel (stopped in about 0.2 s), saving Markdown and JSON through the real Save dialog (byte-identical to the preview; JSON passed the schema and privacy checks), "1 entry" and "2 entries" on the save screen, and a clean close with no worker left running and the package folder unchanged.
  - Cancelling a scan before or during any of its five reading steps stopped it, and closing the window during a scan ended the app (exit code 0); no worker process was left.
  - Files: while watchers recorded `%APPDATA%`, `%LOCALAPPDATA%` and `%TEMP%` during the monitored scans and saves, nothing there was attributable to WinGPUDoctor, and the report was not added to Windows Recent items; the reports were written where chosen in the Save dialog or on the command line. Other folders and the registry were not monitored, and without administrator tools file activity can be attributed only by path.
  - Network disconnected: the maintainer ran the candidate by hand in airplane mode and reported that it worked.
  - Network: with the network connected, the app, the command-line tool and their worker processes owned no TCP or UDP connection in any poll (about every 20-35 ms); polling can miss a connection that opens and closes between two polls. A static check found no reference to .NET networking assemblies or types and no import of the listed Windows networking libraries in the packaged files; it does not cover the shared .NET runtime.
- **App, earlier live checks** (2026-09-26 and 2026-09-27, from builds and local test packages of the app):
  - Scan, results, cancel (stopped in about 0.3 s), saving Markdown and JSON through the real Save dialog, and a clean close with no worker left running.
  - Saved files were byte-identical to the preview; JSON reports passed the schema and privacy checks.
  - Dark and light themes, and an active high-contrast theme.
  - Opening the app from an extracted test package through Explorer's open action, as a double-click does.
  - Selected report text drawn opaquely in dark and high contrast and readable through UI Automation.
  - Keyboard access to the ⓘ explanations.
- The published v0.1.0 ZIP and the local test packages were rescanned with the current packaging checks and are clean.

## Not verified

- A release ZIP in high contrast, and in light mode on the first laptop. Light mode displayed correctly with candidate 2 on the second laptop, and both themes were checked earlier with test packages.
- The current timeout wording, "Didn't finish in time", in a real scan: no step timed out during the live checks on the first laptop, and the second laptop's runs used earlier candidates. Deterministic tests cover it.
- PCs beyond the two laptops, other GPU configurations, external or multiple displays, Windows 10, Windows on ARM, virtual machines and remote sessions.
- A screen reader such as Narrator or NVDA.
- **What happens when the .NET 10 Desktop Runtime is missing.** Not tested; the maintainer moved this check to after the release. If you try the app without it, please report what you saw.
- **Use by people who don't use the console.** No usability sessions were held; the maintainer moved them to after the release. Please report anything that was hard to follow.
- Live: a scan that is too far along to stop ("Almost done…"), "Animation effects" turned off, and the minimum window size.

## Feedback

Please report problems, and anything that was hard to follow, through [GitHub Issues](https://github.com/erictyj1155/WinGPUDoctor/issues). Reports are especially welcome from PCs without the .NET 10 Desktop Runtime and from people who don't use the console. Describe what you saw rather than pasting a full report: review a report before sharing any of it, because model and device names can be distinctive. Report security problems privately as described in `SECURITY.md`.
