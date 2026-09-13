# Security

This is a pre-release proof of concept, with no security audit or production-hardening claim. There is no established supported-release maintenance policy yet. Test only builds whose source you trust.

## Reporting a vulnerability

A private security reporting channel has not yet been configured because this repository has not been published. Do not post secrets, full diagnostic dumps, exploit-sensitive details, or personal machine information in a public issue. Once an upstream exists, use GitHub private vulnerability reporting if enabled; otherwise ask the maintainer for a private channel without disclosing the vulnerability publicly. No response SLA or invented contact address is promised. The owner must configure this channel before a public release.

## Security boundaries

- Local fixed read queries; no arbitrary WQL, remote machine parameter, remediation commands, or extra privileges.
- `asInvoker` executable; no elevation prompts or administrator-only features.
- WMI/native strings are untrusted. Report allowlist and whole-field redaction precede all export; no raw exception output.
- No app network client, telemetry, automatic updates, service, or dynamic vendor DLL loader.
- File export is explicit and create-new only. A failed write may leave a new partial file; existing files remain intact. Trusted destination selection remains the user's responsibility.
- Dependencies are versioned/locked; CI uses read-only repository permissions and does not attach live hardware reports. GitHub-hosted CI has not run until the repository is published.

## Known limits

Provider timeouts are not a hard total deadline. Windows/driver/provider defects can still affect collection. The application cannot guarantee secret detection in arbitrary descriptions, secure erasure of managed memory, safe behavior by compromised providers, or zero transient device activity. Read-only means no intentional configuration-changing operations; it does not bypass OS security or make third-party drivers harmless.

Before distributing binaries: enable private vulnerability reporting, review dependency advisories and licenses, test standard-user behavior across supported configurations, review packaging/signing and downloaded-binary warnings, and publish a precise support matrix.
