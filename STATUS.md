# Project status

Last updated: 2026-09-13 (Asia/Kuala_Lumpur). Record language: English.

## Current checkpoint

- **M1–M3 are locally checkpointed; M3 implementation and final read-only review are complete, and all required pre-checkpoint findings are closed.** This snapshot belongs to the owner-authorized M3 commit. No remote, publication, tag, or release exists; M4 has not started.
- Completed through M3: read-only WMI inventory, active DisplayConfig paths, exact SetupAPI/WMI correlation, privacy-projected JSON/Markdown and CLI export, schema 0.2.0, deterministic hardening tests, and six-run non-admin single-laptop validation. The approved correction makes an empty optional monitor friendly name diagnostic-only rather than independently incomplete. See [architecture](ARCHITECTURE.md), [ADR 0006](docs/decisions/0006-optional-monitor-name-severity.md), and [M3 evidence](docs/VALIDATION.md).
- Management integration: `AGENTS.md` provides model/provider-independent operating rules; this file is the sole current handoff. Existing roadmap, contribution guide, README, and validation record remain the supporting records.

## Git state: read before editing

- `main` now has M1 commit `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee` (`Milestone 1: validated inventory foundation`), created from the existing index only.
- M1's 48-file commit tree exactly matches local `refs/baselines/milestone-1`: `fcc13aed94b20fab57acad9749fac1700ba789b3`. The ref remains a tree, not a commit. The ignored archive `artifacts/baseline/milestone-1.zip` matches the SHA256 recorded in `docs/VALIDATION.md`.
- M2 and management work survived the M1 commit unchanged. M1's direct successor, `Milestone 2: active topology and repository handoff`, records all 58 intended files, including the original status snapshot. Identify that checkpoint using `git log --diff-filter=A --format="%H %s" -- STATUS.md`. M3 changes were reviewed against it; inspect actual Git status before new work.
- `artifacts/milestone-2.patch` is the existing M2-only review snapshot, not a patch of the later management integration. The archive/ref/patch remain historical local review aids; the two approved commits now preserve the M1 and M2/management checkpoints.
- M3 is M2 `d3eaaea347f8c63d67cac493a9640e2baf2c1974`'s direct successor, `Milestone 3: harden topology validation and optional metadata handling`: 18 reviewed changed paths (14 modifications and four additions), completing a 62-file snapshot. Identify it with `git log --diff-filter=A --format="%H %s" -- scripts/validate-m3.ps1`. Only minimal checkpoint wording in this file, `ROADMAP.md`, and `docs/VALIDATION.md` was added after final review. Owner-configured Git identity remains unchanged.

```powershell
git status --short --branch
git diff
git diff --cached --stat
git ls-files --others --exclude-standard
git show-ref
git cat-file -t refs/baselines/milestone-1
git log -5 --oneline
```

## Verification and limits

- Fresh M3 checkpoint validation on September 13: `./scripts/dev.ps1 -Action test` passed locked restore, Release build (0 warnings/errors), all 100 xUnit tests (0 failed/skipped), schema positive/negative checks, and 31 deterministic M3 helper checks. Privacy/export and native layout/union regressions passed; all seven separately requested CLI integration checks passed. All 30 local documentation links resolved; four affected PowerShell scripts parsed and staged whitespace checks passed. Implementation, tests, and tooling remain unchanged from final review. Evidence: `docs/VALIDATION.md`.
- Preserved M3 live validation: the original `./scripts/validate-m3.ps1` recorded six CLI collections in two batches with a 30-second interval and enforced a non-administrator guard. All six exited `0`; each reported one available internal 2560 × 1600 path, identity/progressive semantics, exact source/target correlation to the same inventory GPU, and path/signal rational rates `74321400/450432`. The friendly name stayed `unknown/missingValue` with a `targetName/missingValue` diagnostic; no `collectionIncomplete`, privacy redaction, retry, schema, or reference failure occurred. Reports and summary remain under ignored `artifacts/m3-validation-73d5a793ae024f2c9799cf0e51e29497/`. The six-run protocol was not repeated for these helper-only fixes.
- The initial restricted-context reports contain substantive WMI `accessDenied` failures, preserved under ignored `artifacts/m3-validation-98677acd3c6247af8c126341acf58a6d/`. Later protocols succeeded outside the reported restriction. An execution-context restriction is plausible but not fully proven: a rebuild occurred between the initial failed and final successful protocols, and their summaries saved neither application hashes nor the token result. The original helper checked only CLI-assembly stability within each protocol; identical full-application content within or across those historical protocols cannot be independently proven. No historical fingerprint was reconstructed.
- Only the owner's laptop/internal display is available (owner-reported). External/clone/hot-plug, AMD, ARM64, RDP, and broad hardware compatibility remain unverified physically; synthetic coverage and six runs on one machine are not broad compatibility proof. WMI has no hard overall deadline; separate metadata reads can race; text filtering cannot guarantee anonymity. Remote CI and interactive `EXPORT` entry remain unverified.

## Confirmed decisions and next action

- Preserve C#/.NET, Core/Windows/CLI separation, exact SetupAPI correlation, privacy/export boundaries, and ADR 0005 (including its supersession of the provisional DXGI plan). Topology does not establish rendering GPU, workload/power, or Hybrid/Optimus/MUX state.
- M3 remains bounded by the single-laptop evidence in [ROADMAP.md](ROADMAP.md). The approved friendly-name correction is implemented; substantive failures remain incomplete; schema stays 0.2.0; no dependency, native API, privacy, correlation, or CLI export change was introduced.
- Repository policy does not define a versioning rule for this behavior correction. Tool version was therefore left at `0.2.0-poc`; changing it requires a separate decision rather than inventing a new convention.
- The three pre-checkpoint review findings are addressed in tooling: manifest-derived application fingerprints with per-run before/after evidence and administrator-guard metadata; decoded-string privacy checks; required missing-name diagnostics, unique report-local GPU resolution, and unexpected-redaction checks. Synthetic negative regressions run in the normal test workflow. Future physical runs can record the improved provenance; old reports retain their limitations.
- Next action: await a separately scoped owner authorization for future work from the roadmap. M4 and broader compatibility work have not started; further commits or publication require their own authorization.

## Resume

Read `AGENTS.md` → this file → actual Git status/diff/history (including untracked files) → relevant code/tests and accepted ADRs. Perform only the authorized task, run appropriate checks, and update this snapshot if state changes. Use `docs/VALIDATION.md` for dated evidence and Git for approved history; do not create another handoff/memory/progress file or rely on conversation context.
