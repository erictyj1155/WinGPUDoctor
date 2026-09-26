# Privacy and data minimization

Policy 0.2 — 2026-09-10. This proof of concept reduces report exposure; it does not promise anonymous reports.

## What is collected

M1 collects numeric Windows version/build, manufacturer/model, WMI controller names, two PCI type IDs, and matched driver provider/version/date. M2 adds active paths, source dimensions, configured rational refresh, rotation, connector type, availability, validated GDI aliases, and filtered monitor friendly names. WMI projections remain explicit and restricted to the intended classes. No unrelated files, directories, processes, accounts, networking, event logs, or firmware tables are inspected.

GPU PnP instance IDs, signed-driver IDs, DisplayConfig LUIDs and adapter interface paths, and SetupAPI instance IDs are temporary correlation data in the Windows collector. They are never logged, exported, or hashed into stable pseudonyms. The target-name packet unavoidably contains monitor path and EDID manufacturer/product fields alongside the friendly name; only the friendly name is projected. The other fields are discarded without use. No raw EDID blob or EDID serial query is performed. Disposal does not guarantee secure erasure of managed strings. Windows/providers may internally access additional information; the application retains only its allowlist.

| Information | Policy |
|---|---|
| Windows username, hostname/computer name, domain | Do not query or export |
| User profile paths, IP/MAC addresses | Do not query or export |
| Serial numbers, UUIDs, monitor EDID blobs | Do not query or export |
| LUIDs, adapter/monitor paths, instance IDs, EDID fields in target-name packets | Internal working data only; no report properties for them |
| Personal files/directories, installed applications, process command lines | Do not inspect |
| Secrets/tokens, environment dumps, raw errors/stack traces | Do not collect or export |
| PCI vendor/device IDs | Four-digit type identifiers only; no instance suffix |
| Manufacturer/model/device/monitor descriptions | Filtered as untrusted text; review before sharing |
| GDI alias | Only exact `\\.\DISPLAY` plus a positive numeric suffix in its dedicated field; not an internal-panel label |
| Report identity/time | Neutral per-report GPU/path/source/target/adapter/clone labels and UTC date; no stable identity |

## Filtering and preview

Both exporters require the privacy projection. Versions, PCI IDs, and dates use restrictive formats. Topology keys are regenerated; GPU links require unique references. Connector/rotation/scan-line strings and GDI aliases have dedicated allowlists. Free text is length-limited and removed whole if it resembles a path, address, serial/credential label, common token, control sequence, or unsafe markup. Removal produces `redacted`, null value, `sensitiveValue`, and a count increment. Neutral relabeling is not counted as removed data. Markdown also escapes formatting characters.

This is defense in depth, **not a general secret detector**. A customized model/monitor name or an unlabeled serial, hostname, username, or token embedded in an ordinary description may evade checks. Common words can also be removed unnecessarily. Versions like `1.2.3.4` are validated as versions rather than mistaken for IP addresses. Never claim that all arbitrary strings are safe.

Default behavior is a sanitized console preview with no application-created report/log file. Console scrollback and any terminal recording are outside the application's control. `--output` displays the sanitized snapshot on stderr and requires `EXPORT`; `--yes --output` is explicit unattended acceptance. The same snapshot is saved. Inspect the result before attaching it to an issue. M1 has no per-field interactive editor; omit the report or manually remove optional fields before sharing when necessary.

The desktop GUI (M7, in development and not yet packaged) displays only the privacy-projected report, read back from the JSON writer output. Its save screen previews the exact Markdown or JSON writer text of the scan's one retained report; a format change produces a new preview, and Save writes exactly that text through the same local-destination and `CreateNew` rules as the CLI, without overwriting. Copying or dragging text out of the preview is blocked because a cloud-synchronized clipboard would leave the machine. The GUI creates no application report or log file other than the one the user chooses to save.

## Storage and network

The diagnostic program contains no telemetry, upload, update check, remote WMI target, or other network client. Export uses an explicit user-chosen file and never overwrites an existing file. It rejects direct UNC/device paths, alternate data streams, and non-fixed drives. It does **not** resolve every junction/reparse point or detect cloud-synchronized folders: a path on a fixed drive can still lead to storage synchronized by another application. Choose an ordinary local folder if the report must remain local. Shell redirection is controlled by the caller.

No report is published or sent automatically. Local reports and build outputs are ignored by Git under `reports/` and `artifacts/`; committed examples contain synthetic data only. Git ignore rules are not a privacy scanner: contributors must review any files they choose to stage.

Build tools are distinct from the runtime program. SDK installation/restore and CI can download packages; SDK telemetry and first-run certificate generation are disabled by the supplied development script. Operating-system crash reporting and behavior of installed providers remain OS/user policy, not a guarantee made by this application.

## New fields and future bundles

Before adding a field: document its diagnostic need, source, sensitivity, retention, unknown states, export representation, and a negative privacy test. Do not add raw dumps to make troubleshooting easier. Future ZIP bundles must contain only reviewed generated reports and a manifest; no automatic log scraping, screenshots, minidumps, account identifiers, or arbitrary user files.

## M4 IPC working data

Production collection now uses the private parent/worker transport for the five fixed operations. The current-user pipe DACL, inherited client handle, Job Object handle, worker process/token handles, and operation identifiers are transient local IPC/containment state. Hardware identifiers, provider values, and topology identities are not placed on the command line, in the environment, in logs, or in persistent files. Only after Ready build identity verification does Start carry transient GPU labels and full WMI instance IDs in the private pipe; missing identities remain null. The parent uses those values for exact driver/topology joins and drops them when assembling Core facts. The same-user DACL is not protection against a hostile process already running as that user.

The worker does not export reports or invoke privacy projection, and M4 introduces no persistent hardware identifier, telemetry, network transport, or temporary transport file. `IntegratedBoundaryTests` now exercise real dispatch, wire encoding/decoding, parent reconstruction, Core-before-projection assertions, and both exports with synthetic identifiers. Earlier Gate 2 scripted DTO tests alone did not establish that full path. Oversized worker output is replaced by a fixed ResourceLimit failure containing no provider text or native-error invention. Managed strings and OS buffers are not claimed to be securely erased.

Development execution fingerprints retain selected host/runtime installation metadata in ignored local validation evidence, separate from diagnostic reports. The worker's internal metadata-only probe reads its execution identity without collecting WMI/display data. Historical evidence is not reconstructed.

## Cancellation and timing evidence

Controlled cancellation writes no report, no export and no new persisted state: the console handler only records cancellation in memory, and the CLI returns `3` with a plain notice. The timing sink is opt-in and defaults to a no-op in production; it records only safe stage and operation names with durations, never hardware identifiers, provider text or exception messages. Calibrated budgets are compile-time policy rather than collected data, and calibration/validation samples stay in ignored local `artifacts/` directories outside the report contract.

The separate development lifecycle hook is enabled only by `WINGPUDOCTOR_M4_PROCESS_PROBE=1` in the validation harness. Its stderr records contain process IDs and birth identities, fixed operation/attempt names and monotonic timestamps, with no hardware identities/provider text; this process metadata stays in ignored private evidence and never enters Core or reports. Normal CLI execution has no lifecycle sink. Cancellation and output commitment now share one atomic state; a late callback after output commitment cannot create a hidden controlled cancellation.
