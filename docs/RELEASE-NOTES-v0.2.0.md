# WinGPUDoctor v0.2.0 release notes (draft)

> **Draft: not published.** The package below is candidate 2, built from clean build output and validated locally on 2026-09-27. Two checks are still open: a run with the network disconnected (by the maintainer) and a trial without the .NET 10 Desktop Runtime (by a friend). Tagging and publishing need separate owner authorization after the final review; if the package is rebuilt, its size and SHA-256 must be updated here.

WinGPUDoctor reads what Windows reports about graphics adapters, drivers and active display paths, explains it, and produces a report that you review and save yourself. Version 0.2.0 adds a desktop app for people who don't use a console.

## What's new

- **Desktop app, `wingpudoctor-gui.exe`.** Start a scan (which you can cancel), read the results as plain-language cards with ⓘ explanations and optional technical details, then review the exact report text and save it as Markdown or JSON. The Welcome screen says what the report is for: someone helping you with your graphics or display gets the report, which you save and send yourself.
- **Saving follows the command-line rules.** The text you review is exactly what is saved; reports go only to a local drive of this PC, existing files are never replaced, and the app suggests choosing a folder that OneDrive, Dropbox or a similar app doesn't sync. Copying from the preview is blocked, and the saved file is exactly the preview you saw.
- **Accessibility and appearance.** The app follows the Windows dark, light and high-contrast themes and the Windows "Animation effects" setting, works with the keyboard, and gives its controls UI Automation names. It uses only built-in Windows fonts and icons, with no vendor logos.
- **One package.** `WinGPUDoctor-0.2.0-win-x64.zip` contains the app, the command-line tool `wingpudoctor.exe` and one shared `worker` folder in a single folder (ADR 0008, decision 6), plus the README's screenshots in `docs/images`.
- **Packaging checks.** Packaging fails if a packaged file carries a local build or user-profile path, if an executable is not the SDK's own launcher with only the SDK's edits, if a runtime file that an executable declares is missing, if packaged code references .NET networking or imports a Windows networking library, or if the output folder is reached through a junction or link.

## Unchanged

- The report schema stays 0.2.0 and the privacy policy stays 0.2. Only `toolVersion` changes, to 0.2.0.
- The command-line tool's arguments, output and exit codes are unchanged; `--help` now shows 0.2.0.
- WinGPUDoctor stays read-only: no automatic fixes, setting changes, elevation requests, telemetry, network client or uploads.

## Requirements

- 64-bit (x64) Windows. Tested on one Windows 11 x64 laptop only.
- The [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) for the app. It includes the .NET 10 Runtime that the command-line tool needs; to use only the command-line tool, the .NET 10 Runtime (x64) is enough.
- No installer and no administrator rights: extract the ZIP and open `wingpudoctor-gui.exe`. The README has a step-by-step guide.

## Not code-signed

The executables are not code-signed, so Windows can't show a verified publisher, and SmartScreen may show "Windows protected your PC" with "Unknown publisher". Before opening the app, compare the ZIP's SHA-256 with the value below and with the first 64 characters of the attached `.sha256` file (the rest of its line is the file name):

```powershell
(Get-FileHash .\WinGPUDoctor-0.2.0-win-x64.zip -Algorithm SHA256).Hash
```

| File | Size | SHA-256 |
|---|---|---|
| `WinGPUDoctor-0.2.0-win-x64.zip` | 1,248,093 bytes | `be75bf3d3115b9803e67964148ef8d6e2727d7bffcd36e8d0cd12ceb5d1842f1` |

A matching checksum shows that the download is complete and unchanged; it can't prove who built it. If it matches, select **More info**, then **Run anyway**. If it doesn't, delete the ZIP and download it again.

## What it can't tell you

A scan shows what Windows reports. It can't tell you which graphics adapter a game or app uses, how busy an adapter is or its power state, whether hybrid graphics or a display mode switch (MUX) is active, whether a driver is up to date, whether the PC, an adapter or a display is healthy or working correctly, or which physical port a display is plugged into. Inactive displays are not listed. Review every report before sharing it: model and device names can be distinctive, and automatic filtering can't guarantee anonymity.

## Verified so far

Details and dates are in `docs/VALIDATION.md`.

- **Deterministic checks** (2026-09-27, after a clean build of the candidate commit): Release build with 0 warnings and 0 errors, 375 xUnit tests, the schema and helper checks and 61 package-layout checks passed. The dependency audit (same lock files) found no known vulnerable direct or transitive NuGet package.
- **Release package:** built after deleting all build output, from the candidate commit with a clean working tree; the packaging checks (local paths, launcher check, declared runtime files, network capability, entry hashes) passed, and an independent rescan of the ZIP found nothing.
- **Command-line tool:** compared with 0.1.0 on every path that returns before collection (help, invalid or duplicate arguments, rejected destinations), the output and exit codes are identical apart from the version in `--help`.
- **From the extracted candidate 2, live on one Windows 11 x64 laptop, non-administrator, dark mode** (2026-09-27):
  - Command-line tool: `--help` shows 0.2.0; Markdown and JSON previews create no file; export after typing `EXPORT` in a console and with `--yes` saved a report that passed the schema and privacy checks; typing another answer, or redirected input without `--yes`, declined with exit code 4 and no file; an existing file was not replaced.
  - App, launched directly and through Explorer: scan, results, cancel (stopped in about 0.2 s), saving Markdown and JSON through the real Save dialog (byte-identical to the preview; JSON passed the schema and privacy checks), "1 entry" and "2 entries" on the save screen, and a clean close with no worker left running and the package folder unchanged.
  - No extra files: during scans and saves, nothing attributable to WinGPUDoctor was written under `%APPDATA%`, `%LOCALAPPDATA%` or `%TEMP%`; the only new files were the reports chosen in the Save dialog or on the command line (the report is not added to Windows Recent items).
  - No network connections: with the network connected, the app, the command-line tool and their worker processes owned no TCP or UDP connection in any poll (about every 20-35 ms), and packaged code has no network capability. Polling can miss a connection that opens and closes between two polls.
- **App, earlier live checks** (2026-09-26 and 2026-09-27, from builds and local test packages of the app):
  - Scan, results, cancel (stopped in about 0.3 s), saving Markdown and JSON through the real Save dialog, and a clean close with no worker left running.
  - Saved files were byte-identical to the preview; JSON reports passed the schema and privacy checks.
  - Dark and light themes, and an active high-contrast theme.
  - Opening the app from an extracted test package through Explorer's open action, as a double-click does.
  - Selected report text drawn opaquely in dark and high contrast and readable through UI Automation.
  - Keyboard access to the ⓘ explanations.
- The published v0.1.0 ZIP and the local test packages were rescanned with the current packaging checks and are clean.

## Not verified

- A run with the network disconnected (airplane mode): to be done by hand by the maintainer.
- The release ZIP itself in the light and high-contrast themes (both were checked earlier with test packages).
- Other PCs, GPU configurations, external or multiple displays, Windows 10, Windows on ARM, virtual machines and remote sessions.
- A screen reader such as Narrator or NVDA, and usability sessions with beginners.
- What happens when the .NET 10 Desktop Runtime is missing: to be checked in a friend's trial.
- SmartScreen on a downloaded ZIP (the local test packages had no download mark).
- Live: a scan that is too far along to stop ("Almost done…"), "Animation effects" turned off, and the minimum window size.
- The independent review of the app's wording (M7 Step 6).
