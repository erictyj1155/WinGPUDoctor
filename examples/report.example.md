# WinGPUDoctor diagnostic report

Schema: 0.1.0 | Tool: 0.1.0-poc | UTC date: 2026-09-10

## Collected facts

- Windows version: 10\.0\.26100 — source: WmiOperatingSystem
- Windows build: 26100 — source: WmiOperatingSystem
- Manufacturer: Example Manufacturer — source: WmiComputerSystem
- Model: Example Laptop — source: WmiComputerSystem

Reported video controllers: 1

### gpu-1

- Name: Example GPU A — source: WmiVideoController
- PCI vendor ID: 1234 — source: WmiVideoController
- PCI device ID: 5678 — source: WmiVideoController
- Classification: Unsupported (NotImplemented) — source: NotCollected
- Driver provider: Example Driver Provider — source: WmiSignedDriver
- Driver version: 1\.2\.3\.4 — source: WmiSignedDriver
- Driver date (provider-reported, not installation date): 2026\-09\-01 — source: WmiSignedDriver

Displays and topology: Unsupported (NotImplemented)

## Interpreted findings

No rules produced a finding; this is not a health verdict.

## Warnings

- InventoryOnly: Inventory does not measure application GPU use, power state, or prove hybrid mode.
- ProviderReportedValues: Firmware and WMI values may be missing, stale, virtual, or inaccurate; driver dates do not establish driver freshness.
- TopologyNotCollected: Display enumeration, modes, and routing are deferred in this proof of concept.
- ReviewBeforeSharing: Review before sharing. Model and device descriptions can be distinctive or customized; automatic filtering cannot guarantee anonymity.

## Collection metadata

- WmiOperatingSystem: Succeeded (None)
- WmiComputerSystem: Succeeded (None)
- WmiVideoController: Succeeded (None)
- WmiSignedDriver: Succeeded (None)
- DisplayConfig: Unsupported (NotImplemented)

Privacy policy: 0.1; redacted fields: 0.
