# Agent operating manual

This repository is the durable source of truth for every contributor and coding agent, regardless of model, provider, or conversation. Follow explicit owner scope and applicable directory instructions. Do not rely on another agent's chat history or private memory to continue work.

## Required workflow

Read `AGENTS.md` → read `STATUS.md` → inspect Git status/diff/history → inspect relevant code/tests → perform the authorized task → run appropriate build/tests → update `STATUS.md` if project state changed.

Before editing, identify the current milestone, its exclusions, existing user changes, and any other active writer. Preserve unfinished work and integrate management changes at a safe checkpoint. A roadmap item or handoff next step does not by itself authorize starting a new milestone. Use the smallest complete change; avoid unrelated cleanup, redesign, dependencies, or abstractions.

Start Git inspection with:

```powershell
git status --short --branch
git diff --stat
git diff
git diff --cached --stat
git diff --cached
git ls-files --others --exclude-standard
git rev-parse --verify --quiet HEAD
# Run only if HEAD exists:
git log -5 --oneline
```

If there is no commit, consult the baseline instructions in `STATUS.md`; do not treat an unborn branch as a clean or empty project. Read relevant untracked files too: ordinary `git diff` omits them.

## Agent responsibility and review

Assign work according to its scope, risk, required capability, and need for independent review. Keep the user's selected primary model and effort unless the user changes them. The primary agent owns scope, decisions, integration, and the final handoff; delegate bounded work only when it provides a clear benefit. All contributors and agents must reconstruct current state from this repository rather than another conversation or private memory.

Independent review by a reviewer with the relevant capability is required for architecture, native ownership/lifetime, containment/IPC, public protocol/schema semantics, privacy/security boundaries, cancellation/lifecycle races, and substantial milestone readiness. The reviewer must inspect repository state, code, tests, validation evidence, and ADRs rather than accept the implementer's summary as proof. A different agent or model name alone does not establish independence; an implementer must not be the sole final gate reviewer for a high-risk change.

Agent choice, delegation, and review do not grant Git or publication authority. They never override `AGENTS.md`, `STATUS.md`, live Git state, code/tests, ADRs, or owner authorization; they do not authorize staging, committing, amending, resetting, cleaning, stashing, adding a remote, pushing, tagging, or releasing.

## Where information belongs

| Record | Responsibility |
|---|---|
| `AGENTS.md` | Stable operating rules, boundaries, and completion requirements; no rolling milestone status |
| `STATUS.md` | One concise current handoff: milestone, completed/in-progress work, next action, confirmed decisions, open issues, limits, and verification |
| Git history and diff | Approved history, recoverable checkpoints, and current changes; not proof of behavioral correctness |
| Code, tests, and scripts | Implemented behavior and reproducible checks; documentation alone cannot establish correctness |
| `ARCHITECTURE.md`, `docs/decisions/` | Architecture and durable decision rationale; consult accepted superseding ADRs |
| `ROADMAP.md` | Bounded future scope and authorization gates |
| `docs/VALIDATION.md` | Dated verification evidence and physical-versus-synthetic coverage; older sections remain historical |
| `PRIVACY.md`, `SECURITY.md`, `docs/REPORT-SCHEMA.md` | Detailed privacy/security rules and versioned report contract |

Keep each fact in its appropriate record and link to details instead of copying them. Update the current snapshot in place; do not turn `STATUS.md` into a diary. Reuse the validation record and ADRs rather than adding parallel handoff, memory, progress, daily-worklog, or model-specific instruction files. Resolve documentation/code conflicts from evidence and record material unresolved differences; do not silently invent a resolution.

## Purpose, architecture, and conventions

- **Read → Explain → Report:** read-only Windows GPU/display observations with conservative interpretation and reviewed local export. Keep the project separate from university/research material.
- C#/.NET, SDK selected by `global.json`, locked packages, and xUnit. Follow `.editorconfig` (UTF-8, LF, final newline, spaces) and existing local naming/style. `Directory.Build.props` enables nullable references, deterministic builds, and warnings as errors. Avoid unrelated reformatting.
- `src/WinGPUDoctor.Core/`: typed facts/states, pure rules, privacy projection, JSON/Markdown. No WMI, native API, hardware, GUI, or file-I/O dependencies.
- `src/WinGPUDoctor.Windows/`: fixed local WMI queries, isolated DisplayConfig/SetupAPI interop, resource disposal, and exact identity matching; depends on Core.
- `src/WinGPUDoctor.Cli/`: composition, arguments, preview, and explicit local export. Reuse the same sanitized snapshot for preview and export.
- `tests/WinGPUDoctor.Tests/`: deterministic synthetic unit/collector-contract tests. `schemas/` contains versioned contracts; `examples/` contains synthetic reports only; `scripts/` contains development and separately opt-in live checks; `.github/workflows/` defines deterministic CI.
- Follow `CONTRIBUTING.md`, architecture, relevant ADRs, and the schema guide before behavioral edits. Intentional dependency changes need lock-file and license/security review; architecture-boundary changes need a justified decision record. Do not add DXGI, a vendor SDK, framework, or generated interop without a concrete authorized need.

## Windows, evidence, and privacy constraints

- Inventory, active display topology, path ownership, application rendering GPU, utilization, power, and Hybrid/Optimus/MUX state are different facts. Never infer one from another without matching evidence or claim health/causation from inventory or topology alone.
- Preserve distinct source/target adapters and relationships, rational path/signal rates, explicit unavailable states, and collection provenance. Do not assume one GPU per display, stable enumeration order, or that DISPLAY1 is the internal panel.
- Correlation uses the exact DisplayConfig interface → SetupAPI instance → unique case-insensitive WMI instance join. Preserve unmatched/ambiguous outcomes; no name/order/PCI-type heuristics. ADR 0005 supersedes the early provisional DXGI requirement.
- Native changes require current Microsoft SDK/API contract review, struct/union/offset/field-width/Unicode/resource-lifetime checks, and relevant deterministic regressions. Keep OS/query-mode differences explicit, retries and counts bounded, and independent collected facts intact on failure. Metadata reads are not a globally atomic snapshot; nominal WMI timeouts are not a hard overall deadline.
- Raw LUIDs, device paths, instance IDs, EDID identifiers, raw provider objects, and exception messages stay out of Core/report exports. Use neutral per-report labels and the shared privacy projection. Treat provider text as untrusted; filtering does not guarantee anonymity.
- No automatic fixes, setters, elevation requests, telemetry, runtime network client, background service, or automatic graphics/BIOS/MUX/driver/power-setting changes. Live checks use only hardware actually available and authorized configurations. Report session/access limitations; do not elevate to conceal them.
- Keep real machine reports, SDK/cache/build output, and local evidence in ignored locations. Never stage secrets, raw dumps, identifying data, or real reports; review selected diffs even when ignore rules exist. No remote creation, sending, upload, publication, or release without explicit owner authorization.

## Build and verification

Use PowerShell 7 from the repository root:

```powershell
./scripts/dev.ps1 -Action test
```

This selects `.tools/dotnet` when available, otherwise the installed SDK; restores locked dependencies; builds Release with one worker/shared compilation disabled; runs deterministic xUnit tests; and checks the synthetic schema, including negative cases. It temporarily disables SDK telemetry/certificate generation and restores the caller's environment. SDK/package restore may need network access; the diagnostic runtime does not. Do not install/change global tooling or persistent settings to hide an environment failure.

Use `-Action build` for a build-only check or `./scripts/verify-schema.ps1` for a schema-only check when appropriate. Add or adjust meaningful tests for changed behavior, failure states, privacy, serialization, and native layouts; preserve existing regressions. For a management/documentation checkpoint, run the existing test workflow and check changed documentation links/consistency; do not invent tests that merely mirror prose.

`./scripts/hardware-smoke.ps1` and `./scripts/verify-cli.ps1` both invoke live collection and write ignored local artifacts. They require an existing Release build and separate opt-in scope; they are not part of deterministic CI. Distinguish executed physical checks from synthetic scenarios and historical results. Record commands, date, outcomes, failures, and untested conditions in `docs/VALIDATION.md`; summarize the latest relevant result in `STATUS.md`. If blocked, report the actual environment/permission/dependency limitation rather than claiming a pass.

## Git checkpoints and completion

- Preserve the existing index, staged/unstaged split, untracked work, baseline refs, and unrelated changes. Do not reset, clean, stash, restage, or rewrite history merely to simplify handoff.
- Follow the owner's existing commit policy: no blanket authorization to commit unconfirmed work. Create a clearly named milestone/stable-stage commit only when that specific checkpoint is authorized and identity is already configured. Review exactly what it includes; never use a blanket add in a dirty tree.
- Do not invent an identity or modify global Git identity/settings. Do not silently configure local identity either. If an authorized commit cannot be made, report that limitation and retain the existing documented baseline rather than implying a commit exists.
- At completion inspect the focused diff, fix in-scope validation failures, and confirm unrelated work remains intact. Update `STATUS.md` when project state changes, with the next safe action and any prerequisite/authorization gate. Put important decisions/evidence into existing repository records before handoff.
- Report files changed and why, checks actually run, remaining uncertainty, and Git/commit effects. Do not claim completion from a plan, a document assertion, or an unverified test result; do not automatically start the next milestone.
