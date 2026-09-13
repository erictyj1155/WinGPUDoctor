# Project status

Last updated: 2026-09-13 (Asia/Kuala_Lumpur). Record language: English.

## Current checkpoint

- **M1 and M2 are locally checkpointed; M3 has not begun.** This snapshot belongs to the owner-authorized M2/management checkpoint; no feature implementation was part of the checkpoint task.
- Completed: read-only WMI inventory, active DisplayConfig paths, exact SetupAPI/WMI correlation, privacy-projected JSON/Markdown and CLI export, schema 0.2.0, and deterministic tests. See [architecture](ARCHITECTURE.md) and [M2 evidence](docs/VALIDATION.md).
- Management integration: `AGENTS.md` provides model/provider-independent operating rules; this file is the sole current handoff. Existing roadmap, contribution guide, README, and validation record remain the supporting records.

## Git state: read before editing

- `main` now has M1 commit `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee` (`Milestone 1: validated inventory foundation`), created from the existing index only.
- M1's 48-file commit tree exactly matches local `refs/baselines/milestone-1`: `fcc13aed94b20fab57acad9749fac1700ba789b3`. The ref remains a tree, not a commit. The ignored archive `artifacts/baseline/milestone-1.zip` matches the SHA256 recorded in `docs/VALIDATION.md`.
- M2 and management work survived the M1 commit unchanged. M1's direct successor, `Milestone 2: active topology and repository handoff`, records all 58 intended files, including this status snapshot. Identify that checkpoint without a self-referential hash using `git log --diff-filter=A --format="%H %s" -- STATUS.md`. Future M3 changes must be reviewed against it; inspect actual Git status before new work.
- `artifacts/milestone-2.patch` is the existing M2-only review snapshot, not a patch of the later management integration. The archive/ref/patch remain historical local review aids; the two approved commits now preserve the M1 and M2/management checkpoints.
- The owner manually configured Git identity before this task resumed; the agent did not change identity or configuration. Only the two named local checkpoint commits are authorized. No remote, publication, tag, or release is part of this task.

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

- Fresh checkpoint validation on September 13: `./scripts/dev.ps1 -Action test` passed locked restore, Release build (0 warnings/errors), all 86 tests (0 failed/skipped), and synthetic schema positive/negative checks. Privacy and native layout/union regressions passed within that suite. The process administrator token was false. Only this file and `docs/VALIDATION.md` changed during checkpoint bookkeeping; implementation and tests are unchanged. Details: `docs/VALIDATION.md`.
- Historical M2 evidence: Release build 0 warnings/errors; 86 deterministic tests passed; schema and live CLI checks passed. The non-admin internal-display run reported one Intel-owned 2560 × 1600 path at approximately 165 Hz. No hardware collection was repeated for this documentation task.
- Current missing-friendly-name behavior remains `unknown/missingValue`, partial collection, exit `3`; its severity is a planned review item, not an implemented fix.
- Only the owner's laptop/internal display is available (owner-reported). External/clone/hot-plug and other hardware/session coverage remain unverified physically; existing synthetic coverage is not physical compatibility proof. WMI has no hard overall deadline; separate metadata reads can race; text filtering cannot guarantee anonymity. Remote CI and interactive `EXPORT` entry remain unverified.

## Confirmed decisions and next action

- Preserve C#/.NET, Core/Windows/CLI separation, exact SetupAPI correlation, privacy/export boundaries, and ADR 0005 (including its supersession of the provisional DXGI plan). Topology does not establish rendering GPU, workload/power, or Hybrid/Optimus/MUX state.
- The next feature milestone remains bounded **M3: single-laptop compatibility validation and hardening**, as outlined in [ROADMAP.md](ROADMAP.md). Do not wait for unavailable external hardware or require physical Extend/Duplicate/hot-plug tests for completion.
- When explicitly instructed to execute M3: inspect the existing tests first; repeatedly validate the unchanged internal-display setup; review optional-name severity; harden only supported in-scope issues; add missing deterministic synthetic cases; record physical and synthetic evidence separately. Do not change graphics/MUX/BIOS/drivers/power settings or introduce new diagnostic capabilities.
- Next action: await a separately authorized M3 implementation task. The owner conceptually approved non-blocking absence of an optional friendly name; the existing partial/exit `3` behavior remains untouched here. Broader physical compatibility remains unverified. Further commits require their own checkpoint authorization.

## Resume

Read `AGENTS.md` → this file → actual Git status/diff/history (including untracked files) → relevant code/tests and accepted ADRs. Perform only the authorized task, run appropriate checks, and update this snapshot if state changes. Use `docs/VALIDATION.md` for dated evidence and Git for approved history; do not create another handoff/memory/progress file or rely on conversation context.
