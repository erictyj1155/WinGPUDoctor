# ADR 0002: WMI baseline; defer topology implementation

Status: accepted, 2026-09-10.

Context: a small proof of concept must demonstrate reliable basic data without inventing topology, application activity, or hybrid mode.

Decision: collect four narrow local WMI projections, matching video-controller PnP IDs to display-driver device IDs exactly in transient memory. Extract only PCI vendor/device type IDs. Do not use controller mode fields as monitor topology, AdapterRAM as modern VRAM truth, or brand names as classification. Duplicate/missing joins remain unknown. Record provider date format limitations.

Next: DisplayConfig active paths plus minimal DXGI LUID mapping, with native layout tests, hot-plug retries, rational refresh, remote-session failures, and privacy handling. That is a technical spike, not an implemented capability. SetupAPI present-device/driver-property validation is a later comparison.

Consequences: M1 inventory can be incomplete or stale; inactive/disabled/virtual devices are not certified present physical GPUs. Display and classification fields explicitly say unsupported. No current MUX state, rendering choice, dGPU power, or causal findings are asserted.
