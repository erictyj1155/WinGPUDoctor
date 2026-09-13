# ADR 0005: Active CCD paths and exact SetupAPI instance correlation

Status: accepted, 2026-09-10. Extends ADRs 0001–0003 without changing their core/privacy/CLI boundaries. Supersedes the provisional requirement to introduce DXGI for M2. ADR 0006 supersedes only the later missing-friendly-name severity consequence below.

Context: M2 asks which Windows display paths are active and which adapters own their source/target endpoints. It does not ask which GPU renders applications. The M1 reserved display shape could not express clone relationships or distinct path/signal timing.

Decision: use active DisplayConfig/CCD queries and a small reviewed Unicode P/Invoke surface in the Windows assembly. Use virtual-mode-aware queries on Windows 10 and additionally virtual-refresh-aware queries on Windows 11 build 22000+. Keep distinct source/target relationships and raw rational rates; export neutral labels. Allow at most three sizing/query attempts; validate counts, union types, endpoint identities, and indexes. Names/adapter metadata can race after the path query.

Correlation: `DISPLAYCONFIG_ADAPTER_NAME` gives an interface path; SetupAPI opens that exact interface in a temporary device-information set, retrieves its devnode information, and reads its instance ID. Compare that full ID case-insensitively with WMI controller IDs. A single match yields exact identity evidence; none/multiple remain unmatched/ambiguous. No path parsing, order, name, or PCI-only join. The direct bridge worked locally, so DXGI and new dependencies were unnecessary.

Privacy: native LUIDs/paths/instance IDs never enter Core facts. Target-name packets include monitor/EDID identifiers; discard those fields. Only a filtered friendly name and validated GDI alias are permitted. Regenerate report keys and remap GPU references at export. No raw exceptions or stable hashes.

Consequences: schema/tool/privacy versions become 0.2.0/0.2.0-poc/0.2. Retain the old schema as a historical contract. All M1 test cases remain; the version assertion follows the new schema. Missing friendly names produce partial results without invalidating available routing evidence. Exact matching supports path ownership only, not roles, workload, power, MUX/hybrid mode, or broad compatibility. Future native changes require regression evidence and layout review.
