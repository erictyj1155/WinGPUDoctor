# Validation evidence

Use [STATUS.md](../STATUS.md) for the current checkpoint and [AGENTS.md](../AGENTS.md) for the operating workflow. Dated sections below describe their own checked snapshot; historical next-step proposals do not override current scope or accepted superseding ADRs.

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
