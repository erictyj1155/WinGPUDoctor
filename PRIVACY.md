# Privacy and data minimization

Policy 0.1 — 2026-09-10. This proof of concept reduces report exposure; it does not promise anonymous reports.

## What is collected

Only numeric Windows version/build, computer manufacturer/model, WMI video-controller names, two PCI type identifiers, and matched GPU driver provider/version/calendar date. WMI query projections are explicit and driver enumeration is restricted to the display class. The app does not inspect unrelated files, directories, processes, accounts, networking, event logs, or firmware tables.

Full GPU PnP instance IDs and signed-driver device IDs are the sole temporary join identifiers. They may include device-instance-specific information; they stay in collector memory to join the correct records, never enter the report DTO, and are not logged or hashed into stable pseudonyms. Disposal does not guarantee secure erasure of managed strings. Windows/WMI may internally access additional information; the application selects and retains only its allowlist.

| Information | Policy |
|---|---|
| Windows username, hostname/computer name, domain | Do not query or export |
| User profile paths, IP/MAC addresses | Do not query or export |
| Serial numbers, UUIDs, monitor EDID blobs, raw device paths | Do not query for reports; temporary GPU join IDs are the exception above |
| Personal files/directories, installed applications, process command lines | Do not inspect |
| Secrets/tokens, environment dumps, raw errors/stack traces | Do not collect or export |
| PCI vendor/device IDs | Four-digit type identifiers only; no instance suffix |
| Manufacturer/model/device descriptions | Allowlisted diagnostic facts, filtered as untrusted text; user review required |
| Report identity/time | Sequential report-local GPU labels and UTC calendar date; no persistent machine ID or precise timestamp |

## Filtering and preview

Both exporters accept only the privacy-projected report. Numeric version/build, PCI IDs, and driver dates use restrictive formats. Free text is length-limited and removed as a whole if it resembles a path, address, serial/credential label, common token, terminal control sequence, or unsafe markup. Redaction changes the field to `redacted`, removes the value, records `sensitiveValue`, and increments a count. Markdown also escapes formatting characters.

This is defense in depth, **not a general secret detector**. A customized model name or an unlabeled serial, hostname, username, or token embedded in an otherwise ordinary device description may evade pattern checks. Common words can also be removed unnecessarily. Version fields are validated as versions, so values like `1.2.3.4` are not mistakenly treated as IP addresses. Never strengthen this into a claim that all arbitrary strings are safe.

Default behavior is a sanitized console preview with no application-created report/log file. Console scrollback and any terminal recording are outside the application's control. `--output` displays the sanitized snapshot on stderr and requires `EXPORT`; `--yes --output` is explicit unattended acceptance. The same snapshot is saved. Inspect the result before attaching it to an issue. M1 has no per-field interactive editor; omit the report or manually remove optional fields before sharing when necessary.

## Storage and network

The diagnostic program contains no telemetry, upload, update check, remote WMI target, or other network client. Export uses an explicit user-chosen file and never overwrites an existing file. It rejects direct UNC/device paths, alternate data streams, and non-fixed drives. It does **not** resolve every junction/reparse point or detect cloud-synchronized folders: a path on a fixed drive can still lead to storage synchronized by another application. Choose an ordinary local folder if the report must remain local. Shell redirection is controlled by the caller.

No report is published or sent automatically. Local reports and build outputs are ignored by Git under `reports/` and `artifacts/`; committed examples contain synthetic data only. Git ignore rules are not a privacy scanner: contributors must review any files they choose to stage.

Build tools are distinct from the runtime program. SDK installation/restore and CI can download packages; SDK telemetry and first-run certificate generation are disabled by the supplied development script. Operating-system crash reporting and behavior of installed providers remain OS/user policy, not a guarantee made by this application.

## New fields and future bundles

Before adding a field: document its diagnostic need, source, sensitivity, retention, unknown states, export representation, and a negative privacy test. Do not add raw dumps to make troubleshooting easier. Future ZIP bundles must contain only reviewed generated reports and a manifest; no automatic log scraping, screenshots, minidumps, account identifiers, or arbitrary user files.
