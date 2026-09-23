# Validation evidence

Use [STATUS.md](../STATUS.md) for the current checkpoint and [AGENTS.md](../AGENTS.md) for the operating workflow. Dated sections below describe their own checked snapshot; historical next-step proposals do not override current scope or accepted superseding ADRs.

## M4 targeted final checkpoint corrections — 2026-09-22

The final timing/checkpoint-readiness review returned FAIL. This authorized correction pass addresses F1-F5 without changing production timing constants, containment, correlation, privacy semantics, schema 0.2.0 or CLI exits. **M4 is pending final checkpoint-readiness re-review**, remains incomplete and is unstaged/uncommitted. Earlier dated sections are historical; the corrections below supersede their overstatements.

### Correction and re-review map

| Finding | Current implementation / record | Verification |
|---|---|---|
| F1: late callback/output race | `HostCancellationController` atomic active → cancelled/output-committed state; actual CLI boundary `CollectionOutput.RunAsync` | Six new `CancellationHandoffTests` cases; retained first/second interrupt, late valid result, no-later-operation and cleanup regressions |
| F2: live readiness, signal timing, survival | `M4CollectionProbe`, `CollectionProgress`, `test-m4-cancel.ps1`, `m4-process-evidence.ps1` | Three corrected first-interrupt runs plus one non-signalled control; six process-evidence helper checks |
| F3: per-run fingerprints / admission claims | `validate-m4-final.ps1` persists both lists around each CLI process; direct PID/birth checks | Three healthy runs; successful same-host reuse and poisoned admission remain deterministic supervisor/admission evidence |
| F4: stage definitions and policy rationale | Timing definitions and margin table below | Existing instrumentation inspected; all five production-policy boundary cases passed; no constants changed |
| F5: historical numbers / baseline wording | Explicit historical errata below | Recomputed medians from preserved samples; named historical and current fingerprint baselines |

### Atomic cancellation/output handoff

One compare-and-exchange decides the winner while collection is active. Cancellation publishes the winning state before token signaling; `TryCommitOutput` can succeed only from active. `CollectionOutput` invokes no report preparation/preview/export until output has won, collection is closed and the handler is removed. A callback captured before removal but executing after commitment cannot change the winner; it permits ordinary/default termination. Second/reentrant interrupts also permit default termination. Failure closes active collection, and source disposal waits logically for active callbacks without blocking the console callback on worker cleanup or process waits.

The six new deterministic cases force both captured-callback interleavings using gates, hold a token callback in flight while cancellation vetoes the real CLI boundary, invoke a late callback during handler removal and output, check reentrancy/second interrupt, and verify failure closes the lifecycle without output. No sleep is the primary proof. Existing `CancellationTests` still reject a valid late result after cancellation and prevent the next operation; its after-attempt test now also verifies the progress trace.

### Corrected live evidence

The private environment hook `WINGPUDOCTOR_M4_PROCESS_PROBE=1` enables a normally absent progress sink. It records only fixed operation/attempt names, sequence numbers, CLI/worker PID plus start-time identity, and monotonic timestamps into ignored private stderr. It adds no public CLI argument or hardware identifier. Instrumented stderr is drained or redirected to private files; instrumentation overhead is included in these runs. This is not a hard real-time guarantee.

Readiness means the production parent validated Ready identity, Start and attempt 1 for `wmi.displayDrivers`, with that same PID/birth worker still alive. The worker announces the attempt immediately before its read; this proves the intended supervised attempt has begun, not a particular instruction inside WMI. Worker identities are captured at launch before Request, retained before signal, and checked directly after CLI exit. Creation-time job active-process limit one excludes nested product workers. The trace also checks that no later application worker started. No global process-name search or rediscovery through an exited root is used; console hosts are excluded.

Console attachment and discovery occur before the recorded signal call. UTC plus monotonic call-start/call-return points are persisted; latency is call-start to observed CLI exit and includes delivery, handler/cleanup and observation overhead. It is not an isolated cleanup or exact physical keypress latency.

| Corrected run | Bundle subdirectory | Exit | Signal call → CLI exit observed | Workers tracked / alive afterward | Preview / export |
|---|---|---|---|---|---|
| Cancel pilot | `m4-cancel-87c5ca5a906a4e6b9f2c69a6243839d0` | 3 | 79.8221 ms | 4 / 0 | none / none |
| Cancel 2 | `m4-cancel-6e15c3402358409f85e76ad5562863b4` | 3 | 39.0612 ms | 4 / 0 | none / none |
| Cancel 3 | `m4-cancel-4ca8c0e346c449f8aed58d80bc3cf908` | 3 | 45.7510 ms | 4 / 0 | none / none |
| Observe control | `m4-observe-92566cdab5384662aac08b876c6ec763` | 0 | no signal | 5 / 0 | present / present |

Every corrected cancellation had readiness, successful signal delivery, a cancellation trace/notice, empty stdout, confirmed worker cleanup, no later worker launch and no preview/export. Three earlier development attempts failed console attachment before any signal because attachment preceded console startup; they remain failures in `controlled-cancel.log` and their original subdirectories. Moving attachment after validated readiness resolved that harness startup race. No forced second Ctrl+C or provider hang was exercised live.

Healthy evidence is `m4-healthy-e1c7e43bf71743de903120e3c8939cc1/summary.json` within the bundle. Each run persists its own `BeforeFingerprint` and `AfterFingerprint` lists/timestamps, match result, CLI identity, lifecycle trace and five worker checks. All three pairs match one another and that summary's `BaselineFingerprint` (the corrected-build batch baseline). They are not claimed identical to older builds.

| Fresh separate CLI process | Whole-process duration | Exit | Per-run fingerprints | Tracked workers alive |
|---|---|---|---|---|
| 1 | 6615 ms | 0 | match | 0 of 5 |
| 2 | 6418 ms | 0 | match | 0 of 5 |
| 3 | 6475 ms | 0 | match | 0 of 5 |

Fresh minimum/median/maximum: **6418 / 6475 / 6615 ms**. These include CLI startup, collection, preview/export and instrumented process observation; they are separate from collector-only calibration and do not quantify operation headroom. Schema, decoded privacy/semantic checks, inventory, signed-driver association and preview/export checks passed each time. Repeated fresh processes establish repeated startup/deployment success and no tracked surviving worker in these runs, not process-wide admission reuse. `SupervisorTests` exercises a later supervisor with the same admission owner after confirmed success, while poison tests and the admission probe establish same-host blocking.

### Timing segment definitions and nesting

| Recorded segment | Actual boundary / interpretation |
|---|---|
| `DeploymentPreparation` | `WorkerDeployment.Resolve` in the native session factory; excludes preceding parent security inspection |
| `ProcessCreation` | Entire `NativeWorkerLauncher.LaunchAsync`, including security checks, pipe/job/attribute/environment setup and process creation; not only the native CreateProcess call |
| `WorkerStartupToReady` | First frame receive after session launch and Request send, through framing/decoding Ready; parent sequence/identity/state checks follow it. Not the entire connect budget or process lifetime to Ready |
| `RequestTransfer` / `StartTransfer` | Parent send plus sequence recording for the corresponding frame; Start follows validated Ready identity |
| `Operation` | Parent operation entry through its finally after cleanup/state finalization for a started operation; includes launch, provider wait, frames and cleanup. Admission-lease release follows the recorded sample |
| `ResultTransferValidation` | Final loop receive through framing/decoding and sequence validation, including remaining provider wait; final result state acceptance follows. Nested in Operation, not an independent additive stage |
| `CleanupTotal` | Starts before termination request and pending-I/O cancellation, then drain and exit confirmation under the one cleanup deadline. Called even after a healthy result |
| `PendingIoCancellation` | Drain/completion observation after the cancellation request; nested in CleanupTotal |
| `WorkerExitConfirmation` | Exit polling after termination was requested and I/O drain was observed; nested in CleanupTotal, not isolated natural-exit latency |

The historical calibration remains six healthy `CollectAsync` calls under a larger calibration policy (15 s operation, 5 s connect, 15 s frame), plus 18 synthetic infrastructure samples. It measures collector duration, not CLI startup/output or an exit code. The synthetic scenario named `blocked` does not itself issue a blocked parent read; the partial-frame case does. No actual provider hang was induced.

### Unchanged policy and relevant rationale

| Constant | Corrected rationale |
|---|---|
| 10 s operation / 10 s later reservation | Historical inclusive healthy maximum **4299.02 ms**; nominal operation allowance is about **2.33×**, a **5700.98 ms** difference. The nested 4002.36 ms result measurement must not be added. Reservations can clip the actual deadline |
| 8 s frame | Historical terminal receive/validation maximum **4002.36 ms**, including provider wait: about **2.00×**, **3997.64 ms** difference. A frame is still clipped by the operation deadline |
| 2 s cleanup | Historical bounded cleanup maximum **28.02 ms**: about **71.4×**, **1971.98 ms** difference. Corrected live cancellation latencies above support end-to-end behavior but do not isolate cleanup |
| 2 s connect | No measurement exactly matches this budget, which starts before native session creation and clips security/deployment/launcher setup. Deployment 15.94 ms and launcher 10.51 ms are supporting components; the 110.88 ms first Ready receive occurs later. Margin remains engineering judgment, not directly measured connect headroom |
| 500 ms final bookkeeping | Conservative engineering reserve, **not separately calibrated**. Worker cleanup/exit measurements cannot justify it; report preparation/preview/export are outside collection |
| 60 s overall | Finite engineering policy with separate active/cleanup/later/bookkeeping reservations. Five full 10 s active + 2 s cleanup allowances plus 0.5 s total 60.5 s, so the first of five operations is intentionally clipped to **9.5 s**. No deadline was weakened |

Five fake-clock policy cases establish strict-before acceptance/equality timeout, reservation clipping, completion, omitted-driver reservation release and exhausted-overall skipped starts. Successful fresh production-policy runs establish that the policy was not exceeded on those runs. They do not prove OS scheduling bounds, population percentiles or cross-hardware compatibility. No new instrumentation or timing constant change was needed to correct the mismatched rationale.

### Historical numerical and evidence errata (F5)

- Six collector-calibration durations give median **6769.21725 ms (approximately 6769.2 ms)**, not 6777.9 ms. The old six whole-CLI durations give median **8445 ms**, not 8447 ms.
- The earlier 788/781/858 ms cancellation intervals began before discovery/attachment and are not actual signal-call latency. Root-descendant rediscovery did not prove orphan absence; those old zero-survivor claims are withdrawn. Fixed-delay readiness is superseded by the validated marker above.
- The old healthy script launched six different CLI processes, despite the phrase “one process.” It captured one global baseline and post-run comparisons, not independently persisted before/after lists for each run. It did not establish same-host admission reuse or independently track all workers.
- The identical historical baseline comparison is specifically between `m4-final-validation-bef96858762e4c7a9d5246914748a53a` and first-pass `m4-final-validation-58554e5b7f4240e8b3fb17d47b2c5a6e`, not the earlier integrated-corrections bundle `m4-integrated-corrections-c8ebf882b0df4bf79c82c45fde3384e5`. That earlier build differs in CLI/Supervisor inputs as expected. No historical fingerprint was reconstructed.
- Remove “two orders of magnitude”: 2 s / 28.02 ms is about 71.4. A numerical 500 / 28.02 ratio (17.8) would still compare unrelated bookkeeping and cleanup stages and is not a valid rationale.

### Fresh corrected-build validation and preserved evidence

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release **0 warnings, 0 errors** |
| xUnit / TRX | **270 total, 270 passed, 0 failed, 0 skipped**; includes six new handoff cases, retained Gate 1 native/process, integrated privacy/semantics and five policy cases |
| Schema | Existing positive/negative checks passed; **8** integrated fixtures passed; schema remains 0.2.0 |
| M3 / admission / deployment helpers | **49 / 1 / 10** passed |
| Execution-fingerprint / process-evidence helpers | **8 / 6** passed |
| `./scripts/verify-cli.ps1` | **7** passed: help, invalid format, unscoped yes, UNC rejection, JSON preview/schema/no file, redirected-input refusal, overwrite protection |
| Corrected live checks | **3/3** first-interrupt cancellations, **1/1** non-signalled control, **3/3** fresh healthy processes passed |
| Documentation/scripts/whitespace | **39** local Markdown link targets resolved; **20** PowerShell scripts parsed; **92** candidate files passed LF/trailing-whitespace/final-newline checks (five generated lock files excluded); `git diff --check` passed |

The first restricted deterministic run built successfully but failed 10 native private-pipe process cases (260/270); its log/TRX remain preserved. The complete standard-user run outside that command restriction passed 270/270 with no application elevation, privilege change, DACL/containment weakening or global tooling change. A full restore/build/helper log and its matching TRX are now retained together. Vulnerability audit was not run; CI remains unexecuted.

The coherent ignored private bundle is `artifacts/m4-final-corrections-70f0b2c3618842dd9bc0aebb001d5802/`: the final matching pair `final-deterministic-confirmed.log` and `final-270-tests-confirmed.trx`, `schema-fixtures/`, `final-cli.log` and `cli-checks/`, corrected cancellation/control logs and subdirectories, `healthy-validation.log` and healthy summary, plus final source/Git/static-check evidence and a bundle manifest. The final reentrant-token test asserts outside the callback so callback exception containment cannot swallow a failed assertion; the complete workflow was rerun afterward. `post-final-test-fingerprint.json` confirms all 21 production execution inputs still exactly match the corrected live batch after that test-only rebuild. Earlier successful, failed/intermediate logs and the prior 264-test TRX are retained separately. Raw reports, stderr, fingerprints, process identities, build logs and binaries remain outside the checkpoint candidate set.

Preflight found 24 tracked modifications and 67 untracked candidate files, M3 HEAD unchanged, an empty index and no remotes/tags. Final candidates are **24 tracked modifications + 73 untracked files = 97**: 48 source/project/lock files, 20 test/project/lock files, 17 scripts, 11 documents and one solution. This pass modifies 22 existing candidates and adds six files; all other initial file hashes are preserved, including Core, Windows collectors, Protocol, Worker, production timing policy and dependency locks. Git index/HEAD/config hashes and all M1-M3 commit/tree identities remain unchanged; nothing is staged. Exact final status/stat and expanded candidate paths are retained in the private bundle.

Current records reconciled: `STATUS.md`, `ROADMAP.md`, `ARCHITECTURE.md`, `README.md`, `PRIVACY.md`, `SECURITY.md`, `docs/API-FEASIBILITY.md`, this record and ADR 0007/index. `docs/REPORT-SCHEMA.md` was reviewed and requires no additional change in this pass: no report field, schema version, cancellation fixture or exit code changed.

Physical evidence is limited to this non-administrator laptop with its unchanged single internal display. No external display/attach-detach, stress, driver/service/power change, second forced interrupt, provider hang, remote operation or broad hardware validation was performed. All M1-M3 history and the existing index are preserved. No stage, commit, publication, tag, release or next-milestone work is authorized by this pass.

## M4 final pre-checkpoint phase — cancellation, timing calibration and healthy validation, 2026-09-21

Historical snapshot: the 2026-09-22 correction section above supersedes this section's cancellation handoff, readiness/survivor/timing claims, fingerprint/admission wording and numerical margins. Retained samples remain useful only within those corrected limits.

The targeted integrated semantics/privacy re-review passed. This authorized phase completed controlled Ctrl+C behavior, internal timing calibration, bounded healthy-machine validation on this laptop and source-of-truth reconciliation. All M4 work remains unstaged and uncommitted. These results are local evidence for the independent final checkpoint-readiness review, not that review or a completion claim.

### Implementation map

| Area | Implementation | Regression evidence |
|---|---|---|
| Controlled cancellation | `HostCancellationController` separates the first interrupt (controlled) from later interrupts (forced). The CLI registers `Console.CancelKeyPress` only around collection, never performs I/O or cleanup in the handler, passes the token to `SupervisedWindowsCollector`, maps `SupervisedCollectionException` code `host-cancelled` to exit `3`, and re-checks cancellation at the serialized transition after collection | `CancellationTests`; `scripts/test-m4-cancel.ps1` |
| Timing instrumentation | `CollectionTiming` records safe stage names through an opt-in sink; production defaults to a null sink and never logs identifiers | `CalibrationTests` |
| Timing policy | `CollectionTimingPolicy.CalibratedProduction` with finite absolute budgets and unchanged deadline architecture | `ProductionTimingPolicyTests` |
| Live checks | `scripts/calibrate-m4.ps1`, `scripts/validate-m4-final.ps1`, `scripts/test-m4-cancel.ps1` | Evidence directories cited below |

### Cancellation behavior

A first Ctrl+C during supervised collection suppresses default process termination, records host cancellation, signals the active supervisor, closes result acceptance for the active operation, prevents any later operation from starting, performs bounded cleanup through the existing containment path, skips report preparation, preview and export, and returns `3` after a plain cancellation notice. It does not report a provider timeout, does not create a `Cancelled` report state, and does not fabricate operation failure information. Already accepted independent operation results may stay internally retained but are never reported. A second interrupt leaves the default handler in place, so forced termination proceeds without promising cleanup, exit `3` or a report; the creation-time Job Object kill-on-close remains the containment backstop.

### Deterministic cancellation evidence

`CancellationTests` (4 cases) covers first-interrupt-controlled versus second-interrupt-forced, cancellation during the startup handshake (terminal kind `Cancelled`, code `host-cancelled`, reason `QueryFailed` and explicitly not `Timeout`, with no completed results), cancellation after one accepted operation preventing the next operation from starting, and cancellation after the real-operation attempt marker, where a valid result offered immediately afterwards is still not accepted. Cancellation before any worker starts, cancellation during cleanup, and retention of accepted data across cancellation are covered by the existing `SupervisorTests` and `SupervisedCollectorTests` cases. Preview/export suppression is structural: the CLI's cancellation checks return before report preparation, and the live runs below confirm no file is written. No test depends on real keyboard input.

### Live cancellation evidence

`scripts/test-m4-cancel.ps1` launches the CLI in its own hidden console, attaches to that console and delivers a real `CTRL_C_EVENT`. The attach/signal runs in a child process so the caller keeps its console, and the signal is confined to the CLI's own console.

| Run | Mode | Delay before signal | Attached | Exit | Report exported | Surviving processes | Standard error |
|---|---|---|---|---|---|---|---|
| 1 | Cancel | 2.5 s | yes | `3` | no | 0 | `Collection cancelled. No report was exported.` |
| 2 | Cancel | 2.5 s | yes | `3` | no | 0 | same |
| 3 | Cancel | 2.5 s | yes | `3` | no | 0 | same |
| 4 | Observe (no signal) | — | — | `0` | yes | 0 | report preview + `Report saved locally.` |

At signal time the observed process tree was `cmd.exe` → `dotnet.exe` (CLI host) → `dotnet.exe` (worker) plus their console hosts, so a worker collection was genuinely in flight. The three cancelled runs exited 788 ms, 781 ms and 858 ms after the signal, consistent with the bounded cleanup deadline, and left no descendant process. The non-signalled control run confirms the harness itself does not perturb a normal collection. Evidence is in ignored `artifacts/m4-cancel-<guid>/probe-result.json` and `cli-stderr.txt`, with the three cancelled runs under `m4-cancel-386962a7504c4304b08240a3fa9a6128`, `m4-cancel-bb16281bf643415b9f24387344316dfc` and `m4-cancel-0b9eefd0a9244337986d0f28ccc89aa5`, and the control run under `m4-cancel-4d54ac652eb84f5fbef60e66c0f5edfb`.

Two earlier attempts wrote Ctrl+C into an ordinary interactive console session instead. That path never delivered a console signal here (a 15-second probe sleep ran to completion), so it is not used as evidence; the affected reports and the empty report directory are retained under ignored `artifacts/m4-cancel-live-<guid>/`. The second-interrupt forced-termination path has deterministic and synthetic coverage only and was deliberately not exercised live.

### Calibration method

Synthetic infrastructure calibration ran first: 18 samples (6 scenarios × 3 runs) covering immediate success, delayed completion, result-then-exit, result-then-remain-alive, a blocked read and a partial frame followed by a block, each with forced termination and cleanup confirmation. No real WMI or provider hang was induced.

Healthy-machine calibration then ran 6 sequential read-only collections as a standard (non-administrator) user on this unchanged laptop: no display, driver, service, permission or power change, no hardware attach/detach, no artificial CPU/GPU stress and no external display. Total durations were min 6311.7 ms, median 6777.9 ms, max 7412.4 ms; all six were complete and exit `0`. Observed cold/warm variation was a few hundred milliseconds without a stable order, and the sample is too small to separate cold from warm reliably. Timing records contain only safe stage/operation names and durations.

Measured healthy maxima used for policy selection:

| Segment | Max observed |
|---|---|
| `wmi.displayDrivers` operation | 4299.02 ms |
| Result transfer/validation (`wmi.displayDrivers`) | 4002.36 ms |
| `wmi.videoControllers` operation | 1459.75 ms |
| `display.activeTopology` operation | 595.20 ms |
| `wmi.computerSystem` / `wmi.operatingSystem` operation | 575.84 / 565.42 ms |
| Request transfer (max, topology) | 156.18 ms |
| Worker startup to Ready (max) | 110.88 ms |
| Cleanup total / exit confirmation (max) | 28.02 / 27.79 ms |
| Pending-I/O cancellation (max) | 0.07 ms |
| Deployment preparation / process creation (max) | 15.94 / 10.51 ms |

This is engineering calibration on one machine. It is not a p95, a p99, a statistically representative sample, or a statement about universal Windows performance.

### Selected timing policy

`CollectionTimingPolicy.CalibratedProduction`: overall budget 60 s, per-operation budget 10 s, cleanup allowance 2 s, later-operation reservation 10 s, final bookkeeping reserve 500 ms, connect budget 2 s, frame budget 8 s. Each value stays finite, never maps to `INFINITE`, and retains the existing absolute-deadline architecture with separate current cleanup, later active allowance and final bookkeeping reservations.

- The 10 s operation budget is more than double the slowest healthy operation (4.30 s) and still leaves meaningful time for later operations through the reservation arithmetic.
- The 2 s cleanup allowance and 500 ms bookkeeping reserve are roughly two orders of magnitude above the observed 28 ms cleanup and 27.8 ms exit confirmation, so normal engine overhead is absorbed without user-visible stalling.
- The 2 s connect and 8 s frame budgets bound handshake and frame transfer above the observed 0.11 s startup and 4.00 s result transfer.
- Margins are explicit multiples of measured maxima rather than a blanket multiplier, and no public timeout option was added. Users still observe only the overall bounded behaviour.

### Timing boundary tests

`ProductionTimingPolicyTests` (5 cases) uses a manual clock as the authoritative boundary proof: just-before acceptance succeeds and exactly-at-boundary acceptance times out (strict `now >= deadline`), production reservation arithmetic resolves the first of five operations to 9,500 ms and the second to 19,500 ms, all five operations complete, conditional driver omission releases the later reservation, and an exhausted overall budget skips later starts with zero attempts. Healthy measurements justify value selection only; they do not establish boundary correctness.

### Healthy-machine validation

`scripts/validate-m4-final.ps1` ran six consecutive exported read-only collections in one non-administrator process, with a five-second pause before the fourth run. All six returned exit `0`; durations were 10394, 8443, 8447, 10183, 8431 and 8323 ms (min 8323, median 8447, max 10394). Every run reported both GPUs, one `wmiSignedDriver` run with status `succeeded`, complete driver provider/version/date association for both controllers, one active internal path at 2560 × 1600 with the rational rate `74321400/450432`, an explicit `unknown/missingValue` monitor friendly name, and `redactedFields: 0`. Semantic output was consistent between runs apart from naturally variable timing, every schema/privacy/semantic check passed, and no stale worker, poisoned admission or surviving child process was observed. The earlier correction-phase validation directory also recorded 6/6 with the same semantics and an identical execution fingerprint. A small sample cannot prove zero resource leakage; what was measured is stated above.

Evidence is retained in ignored `artifacts/m4-final-validation-bef96858762e4c7a9d5246914748a53a/` (fresh) and `artifacts/m4-final-validation-58554e5b7f4240e8b3fb17d47b2c5a6e/` (first pass). `artifacts/m4-calibration-bad38113d63740a18358fb408ec7ee21/calibration.json` holds the synthetic and healthy timing samples.

### Privacy and export validation

Every fresh exported report contained `schemaVersion` 0.2.0, privacy policy 0.2 and `redactedFields: 0`, and the only identity-adjacent text present was the permitted GDI alias `\\.\DISPLAY1` and the evidence label `exactSetupApiInstanceId`. A pattern scan over all six exported reports found zero occurrences of raw PnP device-instance text (`PCI\`, `USB\`, `DISPLAY\`), extended device paths, `LUID`, `EDID`, `monitorId`, `deviceInstanceId` or `serial` markers. Live evidence therefore verifies report form without storing any raw identifier. Exact exclusion assertions continue to come from the existing synthetic marker tests, and no sensitive identifier was written to a validation log to demonstrate absence. Export files stay under ignored `artifacts/`.

### Schema and CLI validation

Schema remains 0.2.0 and no exit code was added. Fresh checks covered the healthy complete collection, existing controlled timeout/incomplete fixtures, the optional missing monitor-name fixture, a topology partial fixture and an attempts-zero fixture (M4 integrated schema helper: 8 passed). `./scripts/verify-cli.ps1` passed all seven production-path checks: complete healthy run `0`, invalid format and unscoped `--yes` and direct UNC destination `2`, redirected-input export refusal `4` with preview, and existing-file overwrite protection `5`. Controlled cancellation returns `3` with no preview, no export and no report fixture.

### Execution fingerprints

Before/after execution identities matched for all six validation runs, and the fresh baseline fingerprint list was identical to the correction-phase baseline, so no parent application input, runtime/dependency configuration, shared assembly, worker deployment closure, selected host identity or runtime/CoreLib identity changed during validation. The metadata-only worker probe performs no hardware collection.

### Fresh verification summary

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build **0 warnings, 0 errors** |
| xUnit | **264 total, 264 passed, 0 failed, 0 skipped** |
| Existing schema positive/negative checks | Passed against unchanged schema 0.2.0 |
| M4 integrated schema helper | 8 passed, 0 failed |
| M3 helper | 49 passed, 0 failed |
| M4 host admission probe | 1 passed, 0 failed |
| M4 deployment helper | 10 passed, 0 failed |
| Execution-fingerprint helper | 8 passed, 0 failed |
| `./scripts/verify-cli.ps1` | 7 passed, 0 failed |
| Live controlled cancellation | 3/3 cancelled runs exit `3`, no export, no survivors, plus 1 non-signalled control run exit `0` |
| Repeated healthy collections | 6/6 exit `0`, schema/privacy/semantic checks passed, fingerprints matched |

### Limitations

Only this laptop with a single active internal display path was exercised physically; external displays, clone, extended desktop, hot-plug, AMD, ARM64, RDP, virtual GPUs, eGPU and other machines remain unverified. Timing is one-machine engineering calibration, not a percentile or a hard real-time guarantee, and no real WMI/provider hang was induced. The second-interrupt forced-termination path is not exercised live. Local restore remains locked but skips vulnerability auditing, and the explicit audit action was not run. CI has not executed for this unpublished repository. No checkpoint commit was made.

### Source-of-truth records updated

`STATUS.md`, `ROADMAP.md`, `ARCHITECTURE.md`, `README.md`, `PRIVACY.md`, `SECURITY.md`, `docs/API-FEASIBILITY.md`, this record and [ADR 0007](decisions/0007-bounded-supervisor-and-worker-protocol.md) now describe the implemented cancellation contract, the selected calibrated timing policy, the scope of the live evidence and the remaining final-review gate. M1-M3 historical evidence was not rewritten.

## M4 integrated semantics/privacy corrections — 2026-09-21

The post-Gate-2 integrated review failed. Targeted F1-F6 corrections and bounded F7/F8 documentation/workflow reconciliation are now implemented locally. All M4 work remains unstaged and uncommitted. These successful checks support a targeted independent GPT integrated re-review; they do not constitute that review or M4 completion.

### Correction and re-review map

| Finding | Implementation | Durable regression evidence |
|---|---|---|
| F1: missing identities | `WorkerOperationDispatcher.Identity` normalizes null/empty/whitespace WMI identities to null while preserving rows; old mapping and strict Protocol semantics remain unchanged | `IntegratedBoundaryTests`: old/new null, empty, whitespace, mixed video/driver rows and unmatched topology |
| F2: actual attempt boundaries | Dispatcher emits WMI attempt 1 immediately before provider entry; `DisplayTopologyCollector` calls the progress seam at each existing retry boundary; `WorkerOperationRunner` sends each marker immediately | Integrated tests assert markers before provider/native entry, 1/2/3 topology attempts, controlled timeout before/after markers and callback transport failure |
| F3: omitted reservation | `SupervisedWindowsCollector` calls `CollectionSupervisor.OmitOperation` before topology; state machine decrements remaining operations without inventing an outcome or resetting the overall deadline | `SupervisorTests` uses synthetic policy arithmetic; integrated omitted-driver fixture preserves completion semantics |
| F4: outgoing ResourceLimit | Runner catches result-encoding ResourceLimit once and sends a fixed small `ResourceLimitFrame`; codec/sequence enforce its only allowed operation/state/reason/payload shape; supervisor applies the existing deadline-first failure path | `ResourceLimitFrameTests`, integrated oversized video/driver/topology/count tests, broken/undelivered fallback tests and supervisor deadline-equality test |
| F5: full path and privacy | Actual dispatcher → runner → wire framing/codec → production parent reconstruction → Core → shared privacy projection → JSON/Markdown | 35 `IntegratedBoundaryTests` cases; raw synthetic identities absent from Core before projection and both exports; six added integrated schema scenarios |
| F6: execution inputs | `execution-fingerprint.ps1`, M3 fingerprint wrapper/before-after checks and metadata-only worker execution-identity probe | `test-execution-fingerprint.ps1`: changed parent configuration/deps, host/runtime metadata, nested worker asset, mixed deployment and missing input; expanded M3 helper comparisons |

### Fresh verification

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build **0 warnings, 0 errors** |
| xUnit | **254 total, 254 passed, 0 failed, 0 skipped**; all previous 211 tests retained |
| Previous Gate 1 native/process tests | Passed, including 2 native layout, 9 native ownership and 10 synthetic process cases |
| Operation/equivalence/privacy tests | 6 existing Worker dispatch, 17 supervised collector and 35 new full-path integrated cases passed |
| Attempts/reservations/outgoing limits | Covered by integrated tests, 28 supervisor cases and 6 ResourceLimit frame cases; no actual provider hang induced |
| Existing schema positive/negative checks | Passed against unchanged schema 0.2.0 |
| M4 integrated schema helper | **8 passed**: previous timeout/incomplete plus WMI timeout, topology timeout, omitted driver, zero attempt, partial unmatched topology and optional missing name |
| M3 helper | **49 passed, 0 failed** |
| M4 host admission helper | **1 passed, 0 failed** |
| M4 deployment helper | **10 passed, 0 failed** |
| Execution-fingerprint helper | **8 passed, 0 failed** |
| `./scripts/verify-cli.ps1` | **7 passed, 0 failed** |
| Documentation/scripts/whitespace | **35** local Markdown link targets resolved; **15** PowerShell scripts parsed; **78** changed/untracked source and documentation files passed LF/trailing-whitespace/final-newline checks; generated lockfile formatting excluded; `git diff --check` passed |

Fresh logs, final `unit-tests.trx`, and categorized execution-input metadata are retained in ignored `artifacts/m4-integrated-corrections-c8ebf882b0df4bf79c82c45fde3384e5/`. Earlier local test evidence was preserved separately. An initial test analyzer error (`xUnit2031`) was corrected. Restricted execution then passed 242/252 intermediate cases and failed 10 native process cases because private-pipe access was denied. Authorized execution outside that command restriction passed; the final expanded suite passed 254/254. No application elevation, DACL weakening, containment change or system configuration change was used to obtain the pass.

Full-path tests use injected providers and controlled transport delivery cutoffs. They establish semantic/provenance behavior without claiming a real WMI hang or calibrated scheduling bound. The CLI script performs only existing harmless normal read-only collection on this unchanged laptop; it is incidental healthy-machine evidence, not broad hardware, controlled cancellation or timing validation. The execution-identity probe reports host/runtime metadata without entering collection.

Execution evidence now records parent deps/runtimeconfig, dependency-declared parent runtime/package assets, shared assemblies and manifest-bearing Supervisor, recursive worker closure including its deps/runtimeconfig, selected host path/version/hash and actual runtime version/directory/core-library identity/hash. It does not fingerprint all of .NET or persist hardware identities. M3 before/after comparisons remain; future summaries use fingerprint format 2. Historical M3 execution evidence was not reconstructed and retains its original coverage limits.

Local deterministic restore remains locked and skips NuGet vulnerability auditing; skipping audit does not prevent network package access. The explicit local `dev.ps1 -Action audit` action enables audited locked restore but was not executed in this task. Package versions are unchanged. CI still defines audited restore, with no executed CI evidence for this unpublished repository.

Full Ctrl+C integration, production timing calibration, broad live M4 validation, final review and checkpoint remain pending. Exact driver/topology correlation, M3 optional-name severity, schema 0.2.0, CLI exits and the shared privacy projection are unchanged.

## M4 Gate 2 — real collector integration, 2026-09-20

M4 Gate 1 passed. Gate 2 is implemented locally and awaits the integrated semantics/privacy review; it is not a final M4 checkpoint and has no timing calibration or live M4 hardware compatibility evidence.

The production CLI now composes `SupervisedWindowsCollector`. The five fixed worker operations are real:

1. `wmi.operatingSystem`
2. `wmi.computerSystem`
3. `wmi.videoControllers`
4. conditional `wmi.displayDrivers`
5. `display.activeTopology`

The parent runs them sequentially under one supervisor. The signed-driver worker is launched only after available nonempty video inventory. Completed independent facts survive later failures/timeouts, and the parent reconstructs the previous `CollectionSnapshot` shape and exact case-insensitive driver join without exposing transient identities. The topology worker executes the existing DisplayConfig → source/target metadata → SetupAPI resolver → exact WMI identity path and returns the complete existing topology run/displays semantics.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **211 total, 211 passed, 0 failed, 0 skipped** |
| M1–M3 regressions | Passed within the full suite |
| Gate 1 native/deadline/synthetic process tests | Passed within the full suite |
| Real worker dispatch tests | OS/computer/video/driver/topology mapping, provenance, transient identity, exact failure state and attempt count |
| Supervised orchestration tests | All-success order/assembly, video failure/driver omission, provider failure, exact/ambiguous/no-match/missing identity, video/driver/OS/computer/topology timeout preservation, unmatched/ambiguous/target-name partial topology, crash/malformed protocol failure, cleanup fatal, pre-cancellation |
| Privacy/export | Scripted typed-result parent → snapshot → privacy → export test passed. The subsequent integrated review found that this bypassed real dispatch/wire encoding; full-path coverage was added in the correction section above. |
| Schema | Existing positive/negative checks passed; integrated timeout and incomplete snapshots validated against schema 0.2.0 |
| M3 helper | 42 passed, 0 failed |
| M4 integrated schema helper | Timeout and incomplete fixtures passed |
| M4 host-process admission probe | 1 passed, 0 failed |
| M4 deployment helper checks | 10 passed, 0 failed |
| `./scripts/verify-cli.ps1` | All seven production-path checks passed; the normal read-only collection inside this check now uses the supervisor-backed path and is not timing calibration |
| Documentation/scripts/whitespace | 35 local Markdown targets resolved; 13 PowerShell scripts parsed; tracked diff and untracked whitespace checks passed |

The only real-machine evidence in this task is the harmless normal read-only collection performed by the existing CLI integration script through the new production path. No display mode, service, permission, driver, power, or hardware configuration was changed, and no hang was induced.

Production schema remains 0.2.0 and exit meanings remain `0/2/3/4/5`. Fatal host admission/deployment/cleanup failures stop preview/export; ordinary provider/timeout/protocol operation failures remain reportable incomplete results. Final Ctrl+C integration remains explicitly pending; the CLI currently has no new orderly interrupt handler.

## M4 final targeted Gate 1 corrections, 2026-09-20

This section records the final targeted corrections following the focused read-only re-review and supersedes only the earlier M4 correction test totals below. The first Gate 1 verdict remains FAIL until an independent final re-review; these local results are not a self-awarded pass.

The protocol trust-boundary semantic validator now treats the optional monitor friendly name as a special case only when the observation is exactly `unknown / null / missingValue`. Available names remain valid. Genuine target-name failures remain valid only with their appropriate failure state/reason and matching blocking `TargetName` issue: for example `failed / null / sessionAccessDenied` or `unsupported / null / notSupported`. Contradictory variants are rejected, including `failed / null / missingValue`, `unsupported / null / missingValue`, `unknown / null / notSupported`, and `failed / null / notSupported`. The rule is enforced in `ProtocolSemantics.MonitorName` at the protocol boundary and does not alter Core/M3 production behavior or generic `MissingValue` semantics.

Regression coverage was added at the direct decoder/validator, protocol sequence, and supervisor result-acceptance paths. The supervisor path rejects the contradictory result as a protocol failure rather than accepting it; direct decoder and sequence tests assert the intended `InvalidValue` semantic rejection. Legitimate target-name failure states are separately asserted to remain accepted.

The synthetic `delayed-completion` scenario now receives a deliberate 150 ms delay from `WorkerProcessTests`, and the test asserts that the result is observed after at least 100 ms before cleanup. This is a bounded synthetic timing check only; it is not timing calibration and does not route a real collector.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **188 total, 188 passed, 0 failed, 0 skipped** |
| Focused protocol/semantic/sequence/supervisor/synthetic-worker suite | 63 passed, 0 failed |
| M1–M3 preserved regressions | Passed within the 188-test run |
| Schema | Valid synthetic report accepted; contradictory observation and undeclared field rejected |
| M3 helper | 40 passed, 0 failed |
| M4 host-process admission probe | 1 passed, 0 failed |
| M4 deployment helper checks | 10 passed, 0 failed |
| Delayed completion | Nonzero 150 ms delayed child result observed after at least 100 ms and otherwise handled normally |

No real WMI or DisplayConfig worker collection, M4 hardware validation, timing calibration, Gate 2 work, schema change, CLI exit change, or production collector routing was performed.

## M4 Gate 1 targeted corrections, 2026-09-20

The first Gate 1 native/transport verdict was FAIL. F1-F13 corrections are now implemented and left unstaged for a separate read-only re-review. This section supersedes the original Phase 1 correctness claims below; historical passing tests did not establish the missing absolute-deadline, cancellation-ownership, recursive-closure or host-wide-poison guarantees. M1-M3 evidence below is unchanged.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` outside restricted command sandbox | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit | **176 total, 176 passed, 0 failed, 0 skipped** |
| Deadline/state tests | 7 deadline/I/O and 24 supervisor cases: slow prefix/payload, strict equality, delayed/unresolved completion, one cleanup deadline, retained accepted facts, disposal/cleanup failure, host cancellation, 22-second skip/reservations and saturating finite arithmetic |
| Native layouts/ownership | 2 layout and 9 ownership/token cases: critical offsets, initialization/first-update/second-update failure, list/value teardown, parent-context rejection and read-only token verification |
| Protocol | 7 parser/sequence and 10 semantic cases: null/missing/wrong-source observations for all operations, contradictory topology, retry markers, zero-attempt failure, optional-name issues, partial state and missing inventory identity |
| Deployment | 7 cases: changed/missing nested dependency, extra asset, mixed assembly, normalized duplicates/traversal/MVID mismatch, parent-loaded shared identity, recognized runtime and quoting |
| Synthetic process | 10 cases: creation-time job/token/Ready checks, blocked/partial/malformed/exited workers, accepted-result shutdown, actual CreateProcess failure, repeated cleanup and unrelated inheritable sentinel exclusion |
| Existing collector/topology/privacy regressions | All 100 preserved pre-M4 tests passed: collector 21, topology 56, model/privacy/export 23 |
| Schema | 3 checks passed: valid synthetic report; contradictory observation and undeclared field rejected |
| M3 helper | 40 passed, 0 failed; fingerprints now include nested worker runtime files; no historical fingerprints reconstructed |
| M4 helper | Isolated host-process default-admission probe 1 passed; dependency-closure/copy/path/stale-output checks 10 passed |
| `./scripts/verify-cli.ps1` | 7 checks passed on the unchanged production path: help, invalid format, unscoped yes, UNC refusal, preview schema/no file, redirected export refusal and overwrite preservation |
| Documentation/scripts/whitespace | 35 local Markdown targets resolved; all 12 PowerShell scripts parsed; tracked diff and all 64 changed/untracked files passed whitespace/LF checks |

Fresh logs and TRX are in ignored `artifacts/m4-gate1-corrections-21bd928718fe40b494e48c13bd1572be/`. The previous rolling TRX was copied into an ignored `m4-gate1-correction-prior-*` directory before test runs. Synthetic deployment fixtures and CLI artifacts remain ignored. No M4 hardware run, real worker collector routing, timing calibration or broad compatibility claim was added. The CLI integration checks did invoke the existing pre-M4 read-only collectors; they are not M4 worker validation.

During implementation, regressions caught an extreme-duration decimal overflow and a fixed-size TOKEN_ELEVATION buffer-length error (Win32 error 24). Both were corrected before the successful run above. Restricted execution denied private-pipe access; authorized execution outside that restriction passed. This does not justify changing DACLs, privileges or elevation.

Tooling side effect: one intermediate direct SDK diagnostic test command, issued outside `dev.ps1`, printed the SDK first-run message that an ASP.NET Core HTTPS development certificate was installed. No trust command was executed, and no certificate was removed or modified afterward. Subsequent/final validation used `dev.ps1` with telemetry/certificate generation disabled and the configured local CLI home. This side effect is not a runtime feature or a validation prerequisite.

### Re-review map

| Findings | Implementation | Regressions |
|---|---|---|
| F1-F3 | `Deadline.cs`, `WorkerSession.cs`, `CollectionSupervisor.cs`, `HostAdmission.cs`, native pipe/process ownership | `DeadlineTests`, `SupervisorTests`, `WorkerProcessTests`, `test-m4-admission.ps1` |
| F4/F11 | `Native/ProcessAttributes.cs`, `SafeNativeHandles.cs`, `WorkerProcess.cs` | `NativeOwnershipTests`, `NativeLayoutTests`, inherited-event and failed-launch process cases |
| F5/F9/F12 | `WorkerDeployment.cs`, manifest generator, `worker-runtime-closure.ps1`, copy/fingerprint tooling, Ready/Start sequence | `DeploymentTests`, Ready mismatch supervisor cases, M3/M4 deployment helpers |
| F6/F8 | `ParentSecurityContext.cs`, admission categories, `CollectionSupervisorStateMachine.cs` | Token/fault tests, fatal-admission, cancellation, skip/reservation, cleanup and arithmetic cases |
| F7/F10 | Protocol codec, semantic/sequence validators, topology Run/Start DTOs and stub worker | `ProtocolTests`, `ProtocolSemanticTests` |
| F13 | ADR 0007, architecture/security/privacy/status/roadmap/API records and this evidence | Fresh suite, local documentation targets, PowerShell parsing and whitespace checks |

The managed pipe choice is justified by retained ownership and independently bounded completion observation, not by assuming cancellation completes immediately. An unresolved session is deliberately retained until host exit and the process cannot admit another worker through its default supervisor. Native scheduling latency and synchronous OS/deployment operations are not proven hard real-time; synthetic tests establish the specified supervisor waiting/acceptance and cleanup ownership behavior. Production policy remains uncalibrated. Current-user pipe access and hash/MVID comparisons do not protect against a hostile same-user process or concurrent malicious replacement of deployment files.

## Milestone 4 Phase 1 — original evidence, 2026-09-15 (superseded by corrections above)

Phase 1 was implemented against the frozen M4 contract and stopped at GPT Review Gate 1. Real WMI and DisplayConfig collection was not routed through the supervisor; the existing production path remains active.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build 0 warnings, 0 errors |
| xUnit / TRX | 125 passed, 0 failed, 0 skipped |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared field rejected |
| M3/M4 helper checks | 39 passed, 0 failed; fingerprints now include parent and `worker/` runtime inputs with relative paths |
| Worker deployment | Embedded manifest generated; relative paths, lengths, SHA-256, duplicate/escape paths, missing files, and managed assembly identities validated |
| Native layouts | Process/job/security/startup/token structures and process-attribute identifiers checked for pointer-width layout |
| Synthetic process tests | Creation-time job membership, non-elevated token, inherited client-pipe handshake, timeout/partial-frame cleanup, complete-frame-then-alive shutdown, malformed frame, abnormal exit, and repeated launch/cleanup exercised |

Native checks cover `SECURITY_ATTRIBUTES`, `STARTUPINFO`, `STARTUPINFOEX`, `PROCESS_INFORMATION`, `JOBOBJECT_*`, token-elevation, and process-attribute identifiers. `PROC_THREAD_ATTRIBUTE_JOB_LIST` is supplied as a pointer to the job handle and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` as a pinned handle array; both raw inputs remain valid until the attribute list is destroyed. `CreateProcessW` uses creation-time job assignment with no create-then-assign fallback.

The private endpoint is created through `CreateNamedPipeW` with first-instance/one-instance byte mode, an explicit current-user DACL, and remote-client rejection. The natively opened overlapped server handle is wrapped in the framework asynchronous pipe stream so parent connect/read/write and cancellation complete through the supported overlapped implementation. The worker receives only the inherited client handle as standard input/output; the parent closes its extra copy after successful creation, and cleanup confirms process exit before allowing another worker.

Protocol v1 uses a four-byte little-endian length followed by strict UTF-8 JSON and the sequence request → ready → start → attemptStarted → result. Tests reject malformed/truncated UTF-8/JSON, duplicate/unknown properties, wrong version/kind/operation, invalid ordering, excessive depth/strings/collections, contradictory result state/payload combinations, and topology references to unknown GPU labels. Resource-bound rejections map to `ResourceLimit`; no truncation fallback was added.

The deadline state machine is deterministic and uses explicit monotonic timestamps in tests. It covers result just before deadline, equality selecting timeout, late results, late timeout callbacks, startup expiry, total-budget reservation, skipped starts, exactly-one terminal outcome, cancellation precedence, cleanup-budget exhaustion, poisoned admission, and preservation of an already completed operation when a later operation fails. Production timing values remain `UncalibratedProvisional` and are not used by the normal CLI path.

Named-pipe/child-process tests require host access outside the restricted command sandbox. Inside that restriction, endpoint creation returned `accessDenied`; the same tests passed outside the restriction. This is recorded as an execution-environment limitation. No M4 hardware run, production worker routing, timing calibration, final Ctrl+C integration, or broad compatibility claim was added.

## Authorized M3 local checkpoint, 2026-09-13

Final targeted read-only review passed; all three required pre-checkpoint findings are closed. Checkpoint preflight confirmed M2 HEAD `d3eaaea347f8c63d67cac493a9640e2baf2c1974`, unchanged M1/M2 history and baseline, no remote, an empty index, usable owner-configured Git identity, and exactly 18 reviewed changed paths. Source, tests, tooling, documentation, Git configuration, and all 21 historical M3 report/summary files matched final-review hashes. Only minimal checkpoint wording in `STATUS.md`, `ROADMAP.md`, and this section is changed during checkpoint execution.

This section belongs to M2's direct successor, `Milestone 3: harden topology validation and optional metadata handling`, which introduces `scripts/validate-m3.ps1`. The single local M3 checkpoint contains 18 changed paths and a 62-file repository snapshot; M1 and M2 remain unchanged. This identification avoids a self-referential commit hash or an extra bookkeeping commit.

| Fresh checkpoint check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| xUnit / TRX counters | 100 passed, 0 failed, 0 skipped; privacy/export and native layout/union regressions passed |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared field rejected |
| M3 helper regressions | 31 passed, 0 failed, including decoded privacy, diagnostic/reference, redaction, and application-fingerprint checks |
| `./scripts/verify-cli.ps1` | All seven integration checks passed; preview permits exit 0 or 3 and does not establish fresh hardware completeness |
| Documentation / PowerShell / whitespace | All 30 repository-local Markdown links resolved; four affected scripts parsed without errors; staged whitespace check passed |

Fresh logs and TRX are retained under ignored `artifacts/m3-checkpoint-e2081fb8fa9c441dae27eb1619376f76/`; the previous rolling TRX was copied there before the normal workflow refreshed it. Historical physical reports remain ignored and unchanged; the six-run protocol was not repeated for this checkpoint. Their provenance limitations below remain applicable, and no historical fingerprint was reconstructed. Real reports, local summaries/logs/TRX/fingerprints, SDK/cache files, and build output are excluded from the commit. No remote, publication, tag, release, or M4 work was introduced.

## M3 pre-checkpoint review corrections, 2026-09-13

The three required review findings were corrected only in validation tooling, synthetic tests, and associated documentation. Production source, collector severity, exact correlation, privacy projection, native declarations, schema 0.2.0, and CLI behavior were unchanged from correction preflight. `BlocksCompletion()`, recovery/shrink coverage, and exact rational-pair matching were left as reviewed.

Future `validate-m3.ps1` protocols discover application-owned runtime assemblies from the Release CLI dependency manifest: `wingpudoctor.dll`, `WinGPUDoctor.Core.dll`, and `WinGPUDoctor.Windows.dll`. SHA256 fingerprints include filename, project-library identity, and byte length. The summary persists the initial set, before/after sets and match results for every run, timestamps, protocol parameters, and the administrator-token/guard result. A mismatched pre-run build is not collected. Package/framework assemblies are not fingerprinted. These are future validation records, not retroactive evidence for older runs.

The helper validates decoded JSON strings with the documented dedicated-field GDI exception, requires the diagnostic for an unknown/missing name, rejects substantive or malformed diagnostics, requires each adapter reference to resolve to exactly one GPU, and rejects unexpected redaction for this known-laptop protocol. These stricter checks apply to the validation helper, not universal product behavior. Narrow shared functions are exercised by `scripts/test-m3-validation.ps1`, now included in `scripts/dev.ps1 -Action test`.

| Fresh check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore and Release build passed; 0 warnings, 0 errors |
| xUnit | 100 passed, 0 failed, 0 skipped; includes strengthened M3 export marker assertions and existing native layout/union checks |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared identity field rejected |
| M3 helper deterministic checks | 31 passed, 0 failed; schema-valid escaped PCI/USB/device/monitor identifiers, prohibited GDI placement, serial/LUID/EDID labels and UUID rejected; Unicode escaping also rejected |
| Diagnostic/reference negatives | Missing diagnostic/structure, substantive issue, native failure on the name diagnostic, duplicate GPU IDs, dangling source/target references, unavailable relationship, GPU/driver redaction and redaction warning rejected |
| Build identity checks | Exactly three application assemblies discovered; unchanged fingerprints accepted; separate CLI/Core/Windows hash changes and missing assemblies rejected |
| `./scripts/verify-cli.ps1` | Seven integration checks passed: help/argument/destination checks, preview/schema/no default file, export refusal, and existing-file protection; preview accepts complete or incomplete collection and is not a new hardware-completeness claim |
| Documentation | All 30 repository-local Markdown links resolved |
| PowerShell parsing | No parse errors in the live helper, shared checks, synthetic tests, or development script |
| Whitespace and preservation | Tracked diff and untracked trailing-whitespace checks passed; all 21 historical M3 report/summary files, production source, index, HEAD file, and Git configuration matched correction-preflight hashes |

Fresh build/test/CLI logs and a copy of the fresh TRX are retained under ignored `artifacts/m3-correction-041a6fab1bc940339a551ab9d6252a31/`. The normal workflow refreshed its rolling `artifacts/test-results/unit-tests.trx`. Existing M3 physical report/summary files were preserved byte-for-byte. Offline application of the corrected report checks accepted all six reports in each successful folder (`73d5...` and `c6aa...`) and rejected all six in the failed folder (`9867...`). This is reinspection of preserved data, not new physical collection. The six-run protocol was not repeated because production behavior did not change.

## Milestone 3 — initial single-laptop hardening results, 2026-09-13

M3 was authorized as bounded single-laptop validation and hardening. The only approved externally visible correction was the severity of an empty optional monitor friendly name: the name remains explicit `unknown/null/missingValue` with a `targetName/missingValue` diagnostic, but that condition alone no longer makes the display run partial, adds `CollectionIncomplete`, or changes CLI exit to `3`. Substantive source/target API, mode, adapter-correlation, target-availability, provider, retry-exhaustion, and other required collection failures remain incomplete (exit `3`). Export refusal remains exit `4`; export failure remains exit `5`. Privacy removal remains explicit redaction with `ValuesRedacted` and the removed-field count; it does not by itself change collection completeness.

Deterministic validation after the change:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build completed with 0 warnings and 0 errors |
| Deterministic tests | 100 passed, 0 failed, 0 skipped (previous 86 plus 14 M3 cases/theory cases) |
| Privacy/export regressions | Passed, including the missing-name unknown representation, no `collectionIncomplete`, no raw identifier leak, and multi-path relationship preservation |
| Native layout/union regressions | Passed |
| Synthetic schema | Valid example accepted; contradictory observation and undeclared identity field rejected |

The new deterministic coverage verifies the non-blocking name-only case; substantive mode, adapter, target-name API, and independent WMI failures still win; a later substantive issue remains the summary reason when the name diagnostic is first; retry growth, shrink, and exhaustion; mode/path count guards; repeated collector use without stale facts; per-path metadata isolation; and multi-path privacy references. The implementation adds no schema field or version change.

Live repeatability protocol:

```powershell
./scripts/validate-m3.ps1
```

The original helper performed six fresh CLI collections in two batches of three, paused 30 seconds between batches for ordinary desktop use, checked only the CLI assembly hash for changes, enforced a non-administrator guard, and retained reports plus `summary.json` under ignored `artifacts/`. Its final summary reported no CLI hash-change failures. It did not fingerprint Core/Windows or persist any hash/token result, so unchanged full-application content cannot be independently established from those records. Its schema, topology, rate, exit and warning checks supported the observations below, but review identified false negatives in serialized-text privacy and diagnostic/reference checks. The correction section above records the stronger checks; the old summaries were not altered.

Final live result: six of six runs exited `0`. Every run reported one available internal path, source and target matched the same inventory GPU, source resolution was 2560 × 1600, rotation was identity, scan ordering was progressive, and path/signal rates were `74321400/450432`. The friendly name was consistently absent (`unknown/missingValue`) and the only display issue was `targetName/missingValue`; there was no `collectionIncomplete`, retry, schema, privacy, or reference failure. Reports are under ignored `artifacts/m3-validation-73d5a793ae024f2c9799cf0e51e29497/`.

An earlier execution inside the reported restricted command context produced six runs with substantive WMI `accessDenied` failures and unmatched adapter joins. Those anomalous reports are preserved under ignored `artifacts/m3-validation-98677acd3c6247af8c126341acf58a6d/`. Later successful protocols are preserved under `artifacts/m3-validation-c6aa277f362c4cd085184be8602f8ae3/` and the final folder above. The evidence favors an execution-context restriction, but that explanation is plausible, not fully proven. A rebuild occurred between the initial failed and final successful protocols; identical executable content across them cannot be independently proven because application fingerprints were not preserved. The original helper's guard supports the reported non-admin execution, but its result was not persisted. No historical hash was reconstructed, and the failed runs remain failures rather than a product pass or broad compatibility result.

Not verified by M3: external, clone/extend, hot-plug, AMD, ARM64, RDP, virtual-adapter, or second-machine behavior; hard WMI cancellation; remote CI; interactive `EXPORT` entry; security audit; or broad hardware compatibility. The six-run result applies only to this unchanged laptop/internal-display setup.

## Authorized pre-M3 local Git checkpoint, 2026-09-13

Preflight confirmed usable owner-configured author/committer identity, unborn `main`, no remote, the unchanged 48-file M1 index, and no staged ignored files. `git diff --cached --exit-code refs/baselines/milestone-1 --` passed. Comparison with the saved M2 review index showed only the documented management integration: four modified documents plus `AGENTS.md` and `STATUS.md`.

M1 commit `7eefff9a995a0d3bfd6ef53e86e7a3b3db4c3bee` (`Milestone 1: validated inventory foundation`) was created from the existing index without additional staging. Its tree is exactly `fcc13aed94b20fab57acad9749fac1700ba789b3`, matching the preserved baseline. All 58 working source/project/documentation files were byte-identical across that commit; all M2 and management changes remained outside it.

The M2/management checkpoint is the commit that introduces `STATUS.md`, with message `Milestone 2: active topology and repository handoff`, directly after M1. Its 33 explicitly selected changed/added files complete the 58-file repository snapshot. This self-identification avoids embedding the commit's own hash or creating a third bookkeeping commit. Only `STATUS.md` and this dated section changed during checkpoint bookkeeping; the other 56 files, including implementation, tests, schemas, ADRs and tool version, were unchanged from preflight.

Fresh validation before the M2 commit:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| Deterministic suite / existing TRX counters | 86 passed, 0 failed, 0 skipped; includes privacy/export and native layout/union regressions |
| Existing synthetic schema checks | Valid example accepted; contradictory observation and undeclared field rejected |
| Execution token | Administrator role false; no elevation to administrator |
| Explicit staged manifest / `git diff --cached --check` | 33 intended M2/management files; whitespace check passed; no staged ignored files |
| Documentation links | All 27 repository-local Markdown links resolved |

Build/test output remains ignored; no real report, raw identifier dump, secret, SDK/cache, or generated validation artifact is part of either commit. Existing synthetic fixtures and historical validation summaries are retained. No hardware smoke, live CLI integration, M3 repetition protocol, graphics setting change, or fresh compatibility claim was added. M3, including the friendly-name severity correction, remains unstarted. No identity/configuration, baseline-ref, remote, publication, tag, or release change was made by the agent. Historical evidence below is preserved.

## Source-of-truth management checkpoint, 2026-09-10

Documentation integration at the completed M2 checkpoint; no M3 implementation or live hardware collection. Added the stable agent manual and one current status file, connected existing entry points, and aligned the future M3 roadmap with the owner's single-laptop constraint. Source, tests, schemas, scripts, dependencies, accepted ADRs, and historical M1/M2 evidence are preserved.

Verified locally in this checkpoint:

| Check | Result |
|---|---|
| `./scripts/dev.ps1 -Action test` | Locked restore passed; Release build passed with 0 warnings and 0 errors |
| Deterministic tests | 86 passed, 0 failed, 0 skipped; TRX counters confirmed in `artifacts/test-results/unit-tests.trx` (refreshed by the existing script) |
| Synthetic schema | Valid report accepted; contradictory failed-with-value observation and undeclared identity field rejected |
| Documentation | All 27 repository-local Markdown links resolved; focused `git diff --check` and new-file trailing-whitespace checks passed |
| Change scope | Exactly four existing documents changed and `AGENTS.md`/`STATUS.md` added; the other 52 original source/project files were byte-identical by SHA256, including source, tests, schemas, scripts, dependencies, and ADRs |
| Git/baseline integrity | SHA256 unchanged for the index, HEAD file, repository config, M1 baseline ref, and M1 archive; Git status preserved every existing entry and added only the two new untracked management files |

The build used the existing local SDK/cache and required no dependency or configuration changes. No live hardware/CLI integration run, interactive `EXPORT` check, external-display test, remote CI run, or broader compatibility/security validation was performed here. Previous live results below remain dated evidence, not refreshed observations. The optional-friendly-name partial/exit `3` behavior remains unchanged; its review belongs to the unstarted M3 scope. No test or code change was needed for this documentation task.

Git history limitation: `main` has no commit. The M1 reference is a tree object, with the M1 index and archive retained; M2 has no commit checkpoint. The existing local M2 review patch is historical and does not include this management integration. No staging, commit, identity/configuration, baseline-ref, remote, or publication change is authorized by this documentation checkpoint.

## Milestone 2 — current local results, 2026-09-10

Before changing M1, its documented workflow was rerun: Release build had 0 warnings/errors; all 41 tests and schema checks passed; local documentation links were valid. The only baseline whitespace findings were pre-existing extra blank lines at the end of `.editorconfig` and `.gitignore`; those files were preserved unchanged.

Git identity was not configured. Per the user's condition, no commit or identity change was made. The unchanged 48-file M1 state was staged and saved as tree `fcc13aed94b20fab57acad9749fac1700ba789b3`, with a local baseline reference and archive `artifacts/baseline/milestone-1.zip`. Archive SHA256: `B100BF433A8A4D850BAB263510BEB65E66B68BA298FB54C308357AB78B41589E`. M1 test evidence is retained in `artifacts/baseline/milestone-1-tests.trx`. M2 edits remain separate from the index; new files remain untracked. No remote or publication exists.

| Final check | Result |
|---|---|
| Documented locked restore / Release build | Passed; 0 warnings, 0 errors; no new package dependency |
| Deterministic tests | 86 passed, 0 failed, 0 skipped; all 41 M1 cases retained, plus 45 new cases |
| Native layout | Size/offset checks for CCD packets, union and SetupAPI records passed; independent binary union fixture decoded correctly |
| Matching/topology fixtures | One/two adapters, internal/external, multiple targets, clone/extend, separate source/target adapters, unmatched and ambiguous identities passed |
| Failure fixtures | Unsupported/missing API, session access denial, missing names, bad modes, buffer retries/exhaustion, excessive counts, unavailable target, and partial failures passed |
| Privacy fixtures | Native LUIDs, adapter/monitor paths, PnP IDs, EDID identifiers and injected unsafe labels/text excluded from JSON/Markdown; GDI alias accepted only in its dedicated field |
| Schema 0.2 | Live and synthetic reports accepted; contradictory observations and undeclared fields rejected |
| Final live collection | Non-administrator token (`False` for administrator role), active path collected and uniquely matched; exit `3` solely because friendly name was missing |
| CLI behavior | JSON-only preview, no default file creation, argument/UNC rejection, unconfirmed export refusal, and existing-file hash protection passed |

Only the M1 schema-version assertion changed to 0.2.0; no M1 test was removed. The first new privacy test used an over-broad substring check that matched the safe evidence label `exactSetupApiInstanceId`. It was corrected to prohibit actual instance-ID properties and values while retaining the evidence label. Generic native errors were also kept generic rather than classified as confirmed topology changes.

### Observed topology

On this Windows 11 x64 machine (numeric version `10.0.26200`), the final query reported:

| Fact | Observation |
|---|---|
| Video-controller inventory | NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics |
| Active paths | One, target available |
| Source and target adapter | Both matched Intel Graphics (`gpu-2` in that report) |
| Correlation evidence | Exact unique SetupAPI device-instance / WMI PnP-ID equality |
| Connector | `internal`, directly reported by DisplayConfig |
| Source resolution | 2560 × 1600 pixels |
| Path refresh | `74321400/450432` Hz, approximately 165 Hz |
| Target signal VSync | Same rational rate in this snapshot |
| Rotation / scan | Identity / progressive |
| Windows 11 refresh-boost flag | False |
| Monitor friendly name | Unknown (`missingValue`); no substitute name invented |
| Query | Active paths with virtual-mode and virtual-refresh awareness; one attempt |

This supports active internal display-path ownership by the correlated Intel adapter at collection time. It does not establish application rendering, iGPU/dGPU classification, hybrid/Optimus state, electrical MUX position, GPU workload/power, or why another adapter appears in inventory. No active path mapped to the NVIDIA inventory entry in this snapshot; that does not mean NVIDIA was idle or off.

The missing friendly name is a permitted API outcome, not proof of an interop defect or disconnected display. Native struct layouts and other returned fields passed checks. Names and correlation data are read separately from the successful path/mode query and may race with hot-plug. All live results and report files are local/ignored; published examples remain wholly synthetic. `artifacts/milestone-2-live.json` retains the first successful M2 topology run, with later checks under `artifacts/hardware-smoke-*.json`.

### Review and reproduction

```powershell
./scripts/dev.ps1 -Action test
# Explicit live read-only checks, separate from unit tests:
./scripts/hardware-smoke.ps1
./scripts/verify-cli.ps1
# Existing-file differences from the staged M1 baseline:
git diff
# Newly added M2 source/schema/ADR files:
git ls-files --others --exclude-standard
```

`artifacts/milestone-2.patch` is a complete review patch, including new M2 files, relative to the baseline tree. It is generated with a temporary Git index so the real index continues to hold M1. No global/local Git identity is invented. No M3 implementation has begun.

### Remaining gaps and manual checklist for a future validation milestone

Only the current local display configuration has been physically validated. Synthetic tests cover other shapes, not broad hardware compatibility. No external/clone/MST/RDP/ARM64/AMD/virtual-adapter hardware test, GPU power measurement, packet trace, global configuration audit, or security audit was performed. Standard-user success was verified locally; RDP/access-denied failures were simulated, not induced by modifying Windows access policies. DisplayConfig access failures can be session limitations; elevation is not an assumed remedy. WMI can still hang beyond its nominal timeout. Friendly-name redaction is not a guarantee of anonymity.

For later manual testing, only when the user chooses to do so:

1. Save a reviewed current report. Record the user's visible configuration without collecting names/serials from unrelated system data.
2. If an external monitor is available, have the user connect it and choose Extend, then collect. Check distinct endpoints and exact adapter evidence; do not expect an adapter based on port brand or enumeration order.
3. If desired, have the user choose Duplicate and collect. Check shared-source/clone relationships without forcing a one-source/one-target model.
4. Let the user disconnect/reconnect the display, then collect. Check bounded retry diagnostics, target availability, and preservation of other facts. Do not automate mode changes to force a race.
5. In an available remote session, collect once and record either supported output or explicit session/access limitation. Do not elevate to conceal the limitation.
6. If the user independently chooses a different graphics mode later, compare only the reported paths/identities; do not infer workload, MUX mechanism, or a cause from the difference alone.

### Material files changed

Windows: new `DisplayConfigNative.cs`, `SetupApiAdapterResolver.cs`, `DisplayTopologyCollector.cs`, and test visibility; `WindowsCollector.cs` composes the new collector. Core: topology facts/metadata, new `TopologyPrivacy.cs`, privacy projection and Markdown rendering. CLI: live composition, version/help, incomplete-status handling. Tests: new `TopologyTests.cs`, two composition tests, and the schema-version assertion. Schema/examples/scripts now exercise 0.2.0. README, architecture, privacy, feasibility, schema guide, roadmap, security/contribution notes, validation, and ADR 0005 record the final behavior. M1 dependencies/license and the two original whitespace-only files remain unchanged.

## Milestone 1 — historical evidence

Date: 2026-09-10. Results below are local observations, not a broad hardware compatibility claim. Source review references are in `API-FEASIBILITY.md`.

## Verified

| Check | Result |
|---|---|
| Release solution build | Passed, 0 warnings / 0 errors |
| Deterministic xUnit tests | 41 passed, 0 failed, 0 skipped |
| Synthetic JSON round-trip | Passed through the actual privacy boundary and writer |
| JSON Schema | Synthetic and live report accepted; contradictory failed-with-value observation and undeclared hostname rejected |
| Non-administrator live collection | Passed with process administrator-token check `False`; final collector exit `0` |
| Live GPU inventory | Two video controllers returned; names, PCI type IDs, matched providers/versions/dates available |
| Same-provider plumbing check | Reported GPU count agreed with subsequent narrow CIM query; not independent hardware confirmation |
| CLI help and invalid arguments | Passed; invalid format, standalone `--yes`, and direct UNC destination rejected |
| Default JSON preview | Valid report on stdout; no files created in the isolated check directory |
| Unconfirmed export with redirected stdin | Exit `4`, preview shown, no file created |
| Explicit local export | Same sanitized report model previewed and saved under ignored artifacts; schema passed |
| Existing-file protection | Exit `5`; existing synthetic target hash unchanged |
| Local Git foundation | Initialized `main`; no commits, remote, upload, or publication |
| Ignore rules | Project-local SDK and real reports under `.tools/` and `artifacts/` ignored |
| Narrow source inspection | No application network calls, account-name queries, provider method calls, configuration setters, or privilege enablement found in source; not a dynamic security audit |

The deterministic suite covers observation invariants and unavailable-state serialization, provenance, explicit empty versus failed inventory, conservative findings, exact driver matching, ambiguous/missing joins, independent query failure, missing fields, PCI parsing, date parsing, whole-field redaction, markup/control text, numeric version false positives, and preserving the original snapshot.

## Live-check scope

One Windows 11 x64 computer, numeric version `10.0.26200`, was checked in its current configuration. WMI reported an NVIDIA GeForce RTX 5060 Laptop GPU and Intel Graphics. This validates that these controller records and matched driver facts are retrievable here with a non-administrator process. It does **not** establish iGPU/dGPU roles, physical routing, current hybrid/MUX mode, GPU activity, electrical sleep, or actual application rendering. No graphics-mode/driver/power-setting changes were made to test scenarios.

The first live run exposed a real parser limitation: `Win32_PnPSignedDriver.DriverDate` returned DMTF strings with `******+***` in the subsecond/timezone positions. The initial strict parser returned `unknown/invalidValue`, causing a partial result. After inspecting only that field, date-only parsing was corrected to preserve known calendar dates while allowing unspecified time precision. Four regression cases were added. The final run had complete implemented fields. The original partial report remains in ignored artifacts as evidence, not as a publishable example.

## Toolchain and environment

The machine initially had .NET runtimes but no SDK. A portable official SDK 10.0.401 was downloaded into `.tools/dotnet`, with the Windows x64 archive checked against Microsoft's release-metadata SHA512:

```text
24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430
```

Source: `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`; SDK archive: `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip`. The runtime dependency is `System.Management` 10.0.12; lock files capture its transitive dependencies and test dependencies.

The first SDK invocation unexpectedly generated an ASP.NET development certificate in the current-user personal certificate store. Its exact thumbprint and creation time were matched to this SDK's first-use sentinel; only that certificate was removed and absence was verified. No certificate trust action was performed. Subsequent commands and the supplied script set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`. SDK telemetry was disabled for all SDK executions that performed restore/build/test. No system SDK installation or persistent PATH change was made.

The restricted execution environment stalled an initial multi-worker build. Only processes belonging to the project's portable SDK path were stopped. The final build used a single worker with shared compilation disabled outside that sandbox, retaining a non-administrator token. This was a build-environment limitation, separate from the one ordinary compile-name ambiguity fixed before successful builds.

## Reproduce

```powershell
./scripts/dev.ps1 -Action test
# The following are opt-in hardware checks, separate from the deterministic suite:
./scripts/hardware-smoke.ps1
./scripts/verify-cli.ps1
```

`artifacts/test-results/unit-tests.trx` holds local test results. Hardware reports and CLI results are in ignored artifacts subdirectories. `examples/report.example.json` and `.md` are entirely synthetic and use the same exporters as the CLI.

## Not verified / limitations

- GitHub Actions has been defined but has not run remotely; the project is not published.
- Interactive terminal entry of `EXPORT` was not manually exercised; explicit `--yes`, redirected-input refusal, same-snapshot rendering, and overwrite refusal were checked.
- No second physical machine, AMD GPU, eGPU, disabled/disconnected GPU, virtual/indirect display, headless system, RDP, clone/MST topology, Windows on ARM, or older Windows validation.
- No DisplayConfig/DXGI implementation or native topology test yet. Classification is intentionally unsupported.
- No independent Device Manager/SetupAPI driver-value confirmation, electrical power measurement, GPU wake-up experiment, packet trace, full configuration-diff audit, fuzzing campaign, or security audit.
- Expected WMI access/provider/timeout failures were tested with fakes, not induced by changing Windows services or access policy. There is no guaranteed total timeout for a hung provider.
- Regex filtering cannot guarantee anonymity of arbitrary OEM/provider strings; preview remains necessary. Fixed-drive output can still be redirected/synchronized by the filesystem or other software.

## Next bounded milestone

Follow `ROADMAP.md`: active display topology only, using DisplayConfig plus minimum DXGI adapter mapping, explicit unknowns, race handling, local identifiers, and standard-user tests. Application activity, vendor modules, GUI, and remediation remain out of scope.
