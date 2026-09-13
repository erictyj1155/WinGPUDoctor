# Milestone 1 validation evidence

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
