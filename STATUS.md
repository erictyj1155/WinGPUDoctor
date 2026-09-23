# Project status

Last updated: 2026-09-23 (Asia/Kuala_Lumpur). Record language: English.

## Current checkpoint

- **M1-M4 are complete locally. M4's final checkpoint-readiness review passed, and the bounded supervised collection/cancellation work is checkpointed at `57abbec81ac8db83e04a2a588d39fe18c1650070` (`Milestone 4: bounded collection and controlled cancellation`). The working tree was clean immediately after the commit. The checkpoint is local only: no remote, push, tag, or release. No later milestone has started.**
- The production CLI uses the supervisor-backed five-operation path: real operating-system, computer-system, video-controller, conditional signed-driver, and active-topology operations execute in short-lived workers in the established logical order. `WindowsCollector` remains a reference/test aggregate and shares its mapping helpers with the real worker dispatcher.
- Cancellation and output compete for one atomic controller state. Cancellation winning during collection closes result acceptance, prevents later operations, uses bounded worker cleanup and returns `3` before report preparation/preview/export. Output winning permanently closes controlled cancellation before handler removal; an already-dispatched late callback permits ordinary/default termination and cannot create hidden cancellation. Second/reentrant interrupts also permit default termination. No report state or provider timeout is fabricated.
- `CollectionTimingPolicy.CalibratedProduction` is unchanged: overall 60 s, operation 10 s, cleanup 2 s, later-operation reservation 10 s, final bookkeeping 500 ms, connect 2 s and frame 8 s. Inclusive operation, nested terminal-frame wait and cleanup measurements support the choices on one laptop. Connect headroom is engineering judgment without an exactly matched measurement; final bookkeeping is an unmeasured conservative reserve. Fake-clock tests remain the boundary proof.
- Three fresh non-administrator healthy CLI processes on the unchanged internal-display configuration completed with exit `0`, explicit per-run before/after fingerprints and no tracked worker surviving. Schema 0.2.0, privacy projection, exact correlation, report semantics and exit meanings are unchanged. This does not establish cross-machine coverage or same-host admission reuse.

## Git state: read before editing

- M1 `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee`; M2/management `d3eaaea347f8c63d67cac493a9640e2baf2c1974`; M3 `16f19ae42f6df6c71f18224cb3e7599fcd05446e`; M4 `57abbec81ac8db83e04a2a588d39fe18c1650070` (parent M3).
- M1 tree `fcc13aed94b20fab57acad9749fac1700ba789b3` matches `refs/baselines/milestone-1`; M3 commit tree `f68fbfdbade9c9e7ad63e767423b0efb649b498a`; M4 HEAD tree `d9ebe8ae5ee476229bd08b930f4c80d9b0e3f12c`.
- The M4 checkpoint contains 97 reviewed paths (73 additions and 24 modifications); its commit tree is `d9ebe8ae5ee476229bd08b930f4c80d9b0e3f12c`. M1-M3 history and the M1 baseline ref are unchanged.
- The working tree was clean immediately after the M4 commit. No remote, push, tag, or release exists. Raw, build, and private validation evidence remains ignored and uncommitted.
- M4 source, projects, tests, scripts and ADR are tracked in the checkpoint. The only current working-tree changes are the post-M4 documentation updates; the index is empty and no nonignored untracked files remain. SDK/cache/build output, real reports and local validation evidence remain ignored.

## Verification and limits

Fresh correction evidence is recorded in [docs/VALIDATION.md](docs/VALIDATION.md): locked restore, Release build with 0 warnings/0 errors, **270/270** xUnit tests, existing schema positive/negative checks, 8 integrated schema fixtures, 49 M3 helper checks, 1 admission probe, 10 deployment checks, 8 execution-fingerprint checks, 6 process-evidence checks, 7 CLI checks, 3/3 healthy collections and 3/3 corrected live Ctrl+C runs plus a non-signalled control. The final checkpoint-readiness review passed. The coherent build/test/helper/live evidence bundle remains ignored and private. Physical validation remains limited to the documented one-laptop scope.

- Cancellation: six new CLI-boundary race cases use gates, including a captured callback before/after commitment, in-flight token signaling, reentrancy and output suppression. Existing late-result, no-later-operation and cleanup tests remain. Live readiness means the parent validated Ready, Start and attempt 1 for `wmi.displayDrivers`; PID plus birth identities are retained and checked independently after CLI exit. The three signal-call-to-observed-exit durations were 79.8221, 39.0612 and 45.7510 ms, with exit `3`, no preview/export and no later worker launch in the trace. The second forced interrupt remains deterministic-only.
- Timing: the historical 4.299 s inclusive operation contains the 4.002 s terminal-frame wait and cleanup; these are not additive. The 0.111 s first Ready receive is not the connect stage. Cleanup 28.02 ms supports a roughly 71-fold allowance, not a bookkeeping claim. Whole-CLI fresh durations 6615, 6418 and 6475 ms include startup/output and do not measure operation headroom.
- Format-2 fingerprints matched before/after each of the three current runs and against that batch's explicit baseline. No claim equates the corrected build with an older build. The metadata-only fingerprint worker performs no hardware collection; the separate opt-in lifecycle probe records process metadata only into ignored evidence. Same-host admission reuse/poison claims come from deterministic tests, not separate CLI processes.
- Local deterministic restore skips vulnerability auditing but remains locked; the separate `dev.ps1 -Action audit` action was not run. CI retains audited restore in its definition; this unpublished repository has no executed CI evidence.
- Live validation exercised only this laptop with a single active internal display path at 2560 × 1600. External displays, clone/extended/hot-plug, AMD, ARM64, RDP, virtual/eGPU and other machines remain unverified; timing and cancellation results do not extend to them.
- `host-cancelled` stays an internal supervisor code. Schema remains 0.2.0, no new exit code was added, and controlled cancellation deliberately produces no report.
- The managed pipe wrapper retains operation/cancellation/handle ownership, bounds completion observation and quarantines unresolved sessions until host exit. Exactly two handles are inherited: the pipe client for stdin/stdout and a write-only NUL for stderr. A current-user DACL is not authentication against hostile same-user processes.

## Next action

M4 is checkpointed locally and complete. No later milestone has started or is authorized by this handoff; wait for a separately defined owner-approved scope before beginning further milestone work.

## Resume

Read `AGENTS.md` -> this file -> actual Git status/diff/history -> relevant code/tests/ADRs. The repository is the source of truth; dated validation and review records describe their own snapshots. The final M4 checkpoint-readiness review passed, and the M4 commit is recorded above.
