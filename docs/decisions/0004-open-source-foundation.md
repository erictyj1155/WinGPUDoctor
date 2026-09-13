# ADR 0004: MIT and local-first foundation

Status: accepted, 2026-09-10.

Decision: adopt the standard MIT license with a contributors copyright notice. The aim is straightforward permissive reuse of the diagnostic core and report tooling. Keep this project separate from university/research work. No affiliation, endorsement, or institutional source material is assumed.

Create a local repository and deterministic CI definition, but do not create a remote, upload system reports, publish binaries, or assign an invented maintainer email. Configure GitHub private vulnerability reporting and a release support policy before public distribution.

No telemetry, network client, automatic update check, background service, or remediation API is part of the initial program. SDK downloads and package restore are development activities, not runtime functionality.

Consequences: publication and release operations remain owner decisions. Third-party packages retain their own licenses; vendor SDK redistribution requires separate review if ever introduced. MIT provides no warranty or security certification.
