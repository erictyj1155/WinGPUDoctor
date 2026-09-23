# WinGPUDoctor diagnostic report

Schema: 0.2.0 | Tool: 0.2.0-poc | UTC date: 2026-09-10

## Collected facts

- Windows version: 10\.0\.26200 — source: WmiVideoController
- Windows build: 26200 — source: WmiVideoController
- Manufacturer: Example OEM — source: WmiVideoController
- Model: Example Model — source: WmiVideoController

Reported video controllers: 2

### gpu-1

- Name: Example GPU — source: WmiVideoController
- PCI vendor ID: 10DE — source: WmiVideoController
- PCI device ID: 1234 — source: WmiVideoController
- Classification: Unsupported (NotImplemented) — source: NotCollected
- Driver provider: Example Vendor — source: WmiVideoController
- Driver version: 1\.2\.3\.4 — source: WmiVideoController
- Driver date (provider-reported, not installation date): 2026\-09\-01 — source: WmiVideoController

### gpu-2

- Name: Example GPU — source: WmiVideoController
- PCI vendor ID: 10DE — source: WmiVideoController
- PCI device ID: 1234 — source: WmiVideoController
- Classification: Unsupported (NotImplemented) — source: NotCollected
- Driver provider: Example Vendor — source: WmiVideoController
- Driver version: 1\.2\.3\.4 — source: WmiVideoController
- Driver date (provider-reported, not installation date): 2026\-09\-01 — source: WmiVideoController

## Active display paths

These facts describe Windows display paths, not application rendering, utilization, or power state.

Active paths reported: 1

### display-1

- Monitor friendly name: Example Panel — source: DisplayConfig
- Source / target: source-1 / target-1
- Source GDI name (not an internal-panel label): \\\\\.\\DISPLAY8 — source: DisplayConfig
- Display source adapter: adapter-1; gpu-2 (Example GPU); evidence: ExactSetupApiInstanceId; confidence: Exact
- Display target adapter: adapter-1; gpu-2 (Example GPU); evidence: ExactSetupApiInstanceId; confidence: Exact
- Output technology: displayPortEmbedded — source: DisplayConfig
- Source resolution: 2560 × 1600 pixels
- Path refresh (virtual-aware): 165 Hz (165/1)
- Target signal vertical-sync rate: 165 Hz (165000/1000)
- Scan-line ordering: progressive — source: DisplayConfig
- Target rotation: identity — source: DisplayConfig
- Path active: True; target available: True
- Refresh boost flag: False
- Reported clone group (when source mode is absent): Unknown (MissingValue) — source: DisplayConfig
- Query mode: VirtualModeAndRefreshAware

## Interpreted findings

- **inventory\.multiple\-adapters** (information): Windows reports multiple video controllers\. This alone does not establish hybrid mode, display routing, GPU power state, or which GPU an application uses\. Evidence: facts\.gpus
- **topology\.endpoint\-adapter\-association** (information): For display\-1, the source endpoint is exactly associated with gpu\-2, and the target endpoint is exactly associated with gpu\-2, through identity correlation in this report\. These associations do not establish application rendering, workload ownership, electrical routing, GPU preference, power state, graphics mode, health, or driver correctness\. Evidence: facts\.displays\.value\[0\]\.id, facts\.displays\.value\[0\]\.pathActive, facts\.displays\.value\[0\]\.sourceAdapter, facts\.displays\.value\[0\]\.targetAdapter

## Warnings

- InventoryOnly: Inventory does not measure application GPU use, power state, or prove hybrid mode.
- ProviderReportedValues: Firmware and WMI values may be missing, stale, virtual, or inaccurate; driver dates do not establish driver freshness.
- TopologyIsNotRendering: DisplayConfig describes active paths and configured timing, not application GPU selection, utilization, power state, or a confirmed graphics mode. Friendly names are queried after the path snapshot and may race with device changes.
- ReviewBeforeSharing: Review before sharing. Model and device descriptions can be distinctive or customized; automatic filtering cannot guarantee anonymity.

## Collection metadata

- WmiVideoController: Succeeded (None); attempts: 1; query mode: NotQueried
- DisplayConfig: Succeeded (None); attempts: 1; query mode: VirtualModeAndRefreshAware

Privacy policy: 0.2; redacted fields: 0.
