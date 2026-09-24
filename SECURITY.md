# Security

This is a v0.1 release candidate, with no comprehensive security-audit or production-hardening claim. The tested scope is Windows 11 x64 on one ordinary-user laptop; other configurations are unvalidated. There is no response SLA or established maintenance schedule. Use only builds whose source you trust.

## Reporting a vulnerability

No private vulnerability-reporting channel is configured or verified yet. Do not post secrets, full diagnostic dumps, exploit-sensitive details, or personal machine information in a public issue. During GitHub publication, enable and verify GitHub Private Vulnerability Reporting immediately after the repository becomes public. If it is unavailable, establish and document a working private alternative before proceeding to the v0.1.0 GitHub Release. No response SLA or contact address is promised before one is established.

## Security boundaries

- Local fixed read queries; no arbitrary WQL, remote machine parameter, remediation commands, or extra privileges.
- `asInvoker` executable; no elevation prompts or administrator-only features.
- WMI/native strings are untrusted. Report allowlist and whole-field redaction precede all export; no raw exception output.
- DisplayConfig and SetupAPI interop is isolated, read-only, and size/offset tested. Temporary device-information sets use SafeHandle disposal. Raw device identifiers never enter the report model; bounded array sizing/retries and explicit matching failures avoid fallback guesses.
- No app network client, telemetry, automatic updates, service, or dynamic vendor DLL loader.
- File export is explicit and create-new only. A failed write may leave a new partial file; existing files remain intact. Trusted destination selection remains the user's responsibility.
- Dependencies are versioned/locked; CI uses read-only repository permissions and does not attach live hardware reports. Check the [GitHub Actions history](https://github.com/erictyj1155/WinGPUDoctor/actions) for hosted CI results.
- M4 workers are launched from the validated deployment directory inside a fresh Job Object at process creation with kill-on-close, one active process, and no breakaway. Exactly two handles are explicitly inherited: the pipe client for standard input/output and write-only NUL for standard error; the job, server-pipe, process/token, console, and unrelated handles are not inherited.
- The worker protocol is a bounded private local named pipe with an explicit current-user DACL, remote-client rejection, first-instance/one-instance byte mode, strict frame framing, and no reconnect or temporary-file fallback. A non-elevated parent at or below medium integrity is verified before launch. Recursive deployment hashes, shared assembly MVIDs and ready identity are validated before Start. A single absolute cleanup deadline governs cancellation/exit observation; unresolved owners remain quarantined and default admission is permanently poisoned for that process.
- The first Ctrl+C while supervised collection is active competes atomically with output commitment. If cancellation wins, result acceptance closes, no later operation starts, bounded worker cleanup runs, preview/export are skipped and exit remains `3`. Once output wins, even an already-dispatched late callback permits ordinary/default termination without setting hidden cancellation. Second/reentrant interrupts likewise permit default termination with no cleanup, exit-code or report promise. The creation-time job kill-on-close remains the containment backstop; the console callback performs no worker cleanup, I/O or process waits.
- The opt-in development lifecycle probe writes only fixed operation/attempt markers, process IDs/birth identities and monotonic timestamps to private validation stderr. It is disabled in normal execution, adds no public CLI option and does not weaken containment or authenticate hostile same-user processes. No tracked application worker survived the corrected scoped runs; this is not a proof of zero leakage on every path.

The current-user pipe DACL is not authentication against hostile processes running as the same user. Deployment checks detect accidental stale/mixed outputs; they do not establish a security boundary against an account or process compromise. Production collection uses one short-lived worker per fixed operation. Host-fatal admission/deployment/cleanup failures stop preview/export; ordinary provider/timeout/protocol operation failures remain reportable under the existing incomplete-report path.

## Known limits

Provider timeouts are not a hard total deadline. Windows/driver/provider defects can still affect collection. The application cannot guarantee secret detection in arbitrary descriptions, secure erasure of managed memory, safe behavior by compromised providers, or zero transient device activity. Read-only means no intentional configuration-changing operations; it does not bypass OS security or make third-party drivers harmless.

Before distributing binaries: enable private vulnerability reporting, review dependency advisories and licenses, test standard-user behavior across supported configurations, review packaging/signing and downloaded-binary warnings, and publish a precise support matrix.
