# Validation evidence

Use [STATUS.md](../STATUS.md) for the current checkpoint and [AGENTS.md](../AGENTS.md) for the operating workflow. Dated sections below describe their own checked snapshot; historical next-step proposals do not override current scope or accepted superseding ADRs.

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
