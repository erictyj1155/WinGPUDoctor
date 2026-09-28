# Project status

Last updated: 2026-09-28 (Asia/Kuala_Lumpur). Record language: English.

This file is the current snapshot. Dated evidence, review findings with their dispositions, and superseded candidates are in [docs/VALIDATION.md](docs/VALIDATION.md); earlier versions of this file are in Git history.

## Current checkpoint

- **Released: v0.2.0 (2026-09-28), the desktop app and the command-line tool.** Release source S3 `dbbd8d295cde1b182c5cb4c59393013ef70e19b8` (the rc5 build commit) is the target of the annotated tag `v0.2.0` (tag object `22bc9a47ab38f277560e5eb543af59f12e1b8b66`). The public GitHub Release "WinGPUDoctor v0.2.0" (latest, not a draft or pre-release, published 2026-09-28 02:38:04 UTC) has exactly two uploaded assets: `WinGPUDoctor-0.2.0-win-x64.zip` (1248912 bytes, SHA-256 `92ef52047fd06a3bebe7f07309c56bb39d487d833b32622f066896030f6e18da`), which is candidate 5, and its `.sha256`. The binaries are unsigned. The publication record is in `docs/VALIDATION.md`.
- **Released earlier: v0.1.0 (2026-09-25), the command-line tool.** Release source S2 `62e25b7b12d04499f7b39bae417418d33cf297ee` is the target of the annotated tag `v0.1.0` (tag object `0a3da645f624636dce1b5707a79abe3a0fbf987c`). Its Release is unchanged, with its two assets: `WinGPUDoctor-0.1.0-win-x64.zip` (734729 bytes, SHA-256 `65162553242aea18e5b1cebe963b769de76ebd7f7729d360ae151fa984ce63a8`) and its `.sha256`.
- **Never to be published:** the superseded v0.2.0 candidates rc1-rc4, the `-dev` test packages, the rebuild used to check rc5, and the earlier v0.1 ZIPs `0328525a…`, `cdefbb10…` and `1a319aa6…`, all kept under ignored `artifacts/`.
- **Milestones.** M1-M6 are complete; M6 (v0.1 Release Hardening and OSS Readiness) is the latest technical milestone checkpoint. M7, the beginner desktop app `wingpudoctor-gui` (ADR 0008), shipped in v0.2.0. Its remaining Step 6 items, a trial without the .NET 10 Desktop Runtime and usability sessions with 2-3 people who don't use the console, were moved after the release by the owner and are ROADMAP v0.2.1 items. The tool version is 0.2.0; the report schema stays 0.2.0 and the privacy policy 0.2.
- **Repository.** Public. Private Vulnerability Reporting, secret scanning, push protection and a `main` ruleset that blocks force pushes and deletion are active. GitHub's dependency graph and Dependabot alerts are enabled but don't cover the lock files' full transitive set, so the local direct-and-transitive audit (`./scripts/dev.ps1 -Action audit`) is the dependency gate.
- **Physical coverage.** In-depth checks ran on one Windows 11 x64 laptop with one internal display path. The maintainer ran a limited test of rc2 and rc3 on a second laptop with integrated graphics only (make, model and Windows version not recorded); on a first scan one reading step did not finish, and a second scan read everything. The cause is not confirmed; the hypothesis is in `docs/VALIDATION.md`.

## Decisions in force

- **Release gate revision (owner, 2026-09-28).** The missing-runtime trial and the beginner usability sessions moved from the v0.2.0 release gate to after the release. The reasons and the feedback plan (GitHub Issues, a friend's trial, results recorded in `docs/VALIDATION.md`) are in `docs/GUI_PLAN.md` Step 6 and ROADMAP v0.2.1.
- **Reproducibility of the v0.2.0 ZIP (owner, 2026-09-28).** A rebuild from S3 reproduced every one of the 33 entries byte for byte, but the ZIP itself differs because the package stores each built file's modification time. The owner accepted entry-by-entry identity as the release check and published candidate 5's own files. A packaging change for reproducible ZIP timestamps would need its own scope.
- **Timing unchanged.** `CollectionTimingPolicy.CalibratedProduction`: 60 s overall, 10 s per operation, 2 s cleanup, 10 s later-operation reservation, 500 ms final bookkeeping, 2 s connect and 8 s frame, calibrated on the first laptop; fake-clock tests are the boundary proof. The second laptop's first-scan results are a v0.2.1 item (code signing, the operation timeout and a retry policy, with measurements from several computers and an independent review).
- **Authorization.** The owner authorizes each batch. New work, pushes of `main`, tags, Releases and PRs each need explicit owner authorization.

## Git state: read before editing

- Live Git determines the current branch, HEAD, index and working tree.
- `main` was fast-forwarded from `c2355047513076b7610ecb6d4073287d77e1bba5` to `09c8df38107734ce3674a528c742f0bcd4ccb230` and pushed on 2026-09-28; hosted CI run #22 passed on that commit. The v0.2.0 publication record is a later docs-only commit on `main` and is not part of the release source. `m7-desktop-gui` (local and on `origin`) remains as the M7 backup branch at `09c8df3`.
- Tags: `v0.1.0` (S2) and `v0.2.0` (S3).
- Checkpoints: M1 `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee` (tree `fcc13aed94b20fab57acad9749fac1700ba789b3`, matching `refs/baselines/milestone-1`); M2/management `d3eaaea347f8c63d67cac493a9640e2baf2c1974`; M3 `16f19ae42f6df6c71f18224cb3e7599fcd05446e` (tree `f68fbfdbade9c9e7ad63e767423b0efb649b498a`); M4 `57abbec81ac8db83e04a2a588d39fe18c1650070` (tree `d9ebe8ae5ee476229bd08b930f4c80d9b0e3f12c`); M5 `43d3bc39a6310a48ca7f819bf307650f3afaa6a1` (tree `e5187c757c29a2f492832dee11730ee9c1b8b404`); M6 `f966e1b602561044c19eaca6a84cf4baa146b62a` (tree `f4972e15628c75d663b04604d376487b48395897`). Documentation commits such as `702a1ac43e54bf1a0baf0637ff3ff7d1f9f3d15b` and `dedad9a3940ec3a009433b137d072536ed2f9616` are not milestones.
- Real reports, SDK/cache/build output and local evidence stay in ignored locations and are never staged. `artifacts/design/gui-mockup-v1.html` contains real hardware names and must never be staged.

## Verification and limits

- **v0.2.0 release checks** (2026-09-28): the rebuild from S3 matched rc5 entry by entry; hosted CI run #22 on `main` passed 383/383 tests and schema verification with no uploaded artifact; the draft's two assets and the public, unauthenticated downloads matched rc5's ZIP and `.sha256`, as did GitHub's asset digests.
- **Last local deterministic run** (after the clean build of rc5): Release build 0 warnings / 0 errors, **383/383** xUnit tests, schema and helper checks, **61/61** package-layout checks.
- **Dependency audit:** last run for the first v0.2.0 candidate (`fceb401`, 2026-09-27): no known vulnerable direct or transitive package. No lock file has changed since.
- **Last live checks:** candidate 5 (2026-09-28, first laptop, dark mode): the CLI (`verify-cli.ps1` 7/7, help, preview, typed `EXPORT`, typed decline, `--yes`) and the GUI (scan, results, cancel while reading, Markdown and JSON saves byte-identical to the preview, closing during a scan with exit code 0 and no worker left).
- **Not verified for v0.2.0** (also listed in the Release): the current timeout wording in a real scan (deterministic tests only); a release ZIP in high contrast, and in light mode on the first laptop; a screen reader; the `Forced` window ("Almost done…"), "Animation effects" turned off and the minimum window size, live; the missing-runtime experience and beginner usability; the CLI on the second laptop; other PCs, external or multiple displays, Windows 10, Windows on ARM, virtual machines and remote sessions.
- **Standing limits:** `SECURITY.md` (the current-user pipe DACL is not authentication against hostile same-user processes; provider timeouts are not a hard total deadline) and `PRIVACY.md` (filtering can't guarantee anonymity; a folder that links or syncs elsewhere is not detected). Controlled cancellation produces no report and no new exit code; `host-cancelled` stays an internal supervisor code.
- **Notes recorded without change** (details in `docs/VALIDATION.md`): the owner-accepted non-blocking findings deferred after v0.1.0, the Step 1 review's N3 (test naming), and the Step 5 second re-review's nit on the process-wide selection switch (a test keeps it to the one read-only preview).

## Next action

No release step is pending. Follow-ups, each needing owner authorization:
- `docs/RELEASE-NOTES-v0.2.0.md` still carries its draft title and "Draft: not published" note (the published Release text omits them), and ROADMAP and `docs/GUI_PLAN.md` still describe the v0.2.0 gate as open; they can be updated to the published state.
- Record feedback from GitHub Issues and any friend's trial in `docs/VALIDATION.md`.
- v0.2.1 (ROADMAP): the missing-runtime experience, usability, and the evaluation of code signing, the operation timeout and a retry policy.

## Resume

Read `AGENTS.md` → this file → live Git status/diff/history → relevant code/tests/ADRs. Dated validation and review records describe their own snapshots.
