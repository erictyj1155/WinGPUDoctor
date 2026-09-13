# ADR 0001: C#/.NET 10 and separate core

Status: accepted, 2026-09-10.

Context: Windows-friendly maintainability and a reusable diagnostic core are more important than GUI design or lowest-level native implementation in M1.

Decision: use .NET 10 LTS and C#. Keep only System.Management in the Windows assembly; the core contains types, rules, privacy, and writers. The CLI is the composition/export layer. Pin SDK and package versions with lock files. Use xUnit and fixed synthetic fixtures.

Alternatives: C++ offers direct SDK access but more manual lifetime/build work; Rust is feasible but introduces an unneeded toolchain choice. A GUI host, dependency-injection framework, plugin discovery, and a native helper are premature.

Consequences: WMI is Windows-only and cannot be assumed available in every environment. Future interop needs reviewed bindings and resource management. M1 is framework-dependent and initially validated only on Windows 11 x64; publishing/signing and broader support remain future work.
