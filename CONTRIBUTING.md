# Contributing

WinGPUDoctor is an early MIT-licensed project. Contributions should support **Read → Explain → Report**.

1. Follow the model-independent workflow in [AGENTS.md](AGENTS.md): read it, read [STATUS.md](STATUS.md), inspect Git status/diff/history (including untracked files), then inspect relevant code/tests. Read `ARCHITECTURE.md`, `PRIVACY.md`, the API feasibility matrix, and the relevant ADR before changing behavior.
2. Use the SDK in `global.json`. Run `scripts/dev.ps1 -Action test`, which restores locked dependencies and runs deterministic tests. Dependency changes require an intentional lock-file update and review.
3. Keep Windows API access in the Windows assembly. Core rules, privacy, and writers must remain independent of WMI, hardware, or GUI frameworks. Native topology changes require SDK layout/union review and deterministic failure/matching/privacy tests. Do not replace exact instance correlation with order, names, or PCI-type heuristics.
4. Use synthetic fixtures. Do not commit real machine reports, serials, paths, usernames, hostnames, secrets, or raw diagnostic dumps. Hardware checks are opt-in and separate from CI.
5. Add tests for new unavailable/failure states, risky text, interpretation boundaries, and serialization changes. New exported fields require a stated diagnostic purpose and privacy review.
6. Explain what changed, the source API contract, tests actually performed, and untested hardware scenarios. Do not claim a fix, optimum configuration, current GPU activity, or confirmed hybrid mode without matching evidence.
7. After the authorized task and appropriate build/tests, update `STATUS.md` if project state changed and record dated validation in `docs/VALIDATION.md`. Preserve the owner's staging/commit policy and unfinished work. Reuse existing records; do not create parallel handoff/memory/progress files or start a new milestone automatically.

No signing certificate, GitHub token, or administrator account is required for the documented local development workflow. Do not add third-party runtime code, generated interop, or vendor SDKs without a concrete need and license/security review. Avoid unrelated reformatting and over-engineering.

Once a public GitHub upstream is available, use its Issues and Pull Requests for contributions. Until then, there is no public submission route. Follow [SECURITY.md](SECURITY.md) for vulnerabilities and sensitive machine reports; do not put those details in public Issues.
