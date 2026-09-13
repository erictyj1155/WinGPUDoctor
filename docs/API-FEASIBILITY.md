# Windows API feasibility investigation

Reviewed 2026-09-10. This is a source-backed API assessment, not a claim that every candidate has been implemented or tested. Local M1 execution evidence is in [VALIDATION.md](VALIDATION.md). Documentation describes an API contract; OEM firmware, driver quality, access policies, and session context can still limit results.

## Selected and candidate interfaces

| Requested information | Preferred documented interface | Reliability and meaning | Privilege / M1 decision |
|---|---|---|---|
| Windows version/build | `Win32_OperatingSystem.Version`, `BuildNumber` | Numeric OS identity; do not infer marketing edition from major version 10 | Local WMI, standard-user target; implemented |
| Computer manufacturer/model | `Win32_ComputerSystem.Manufacturer`, `Model` | Firmware/OEM-reported descriptions; may be generic, customized, or virtual | No elevation intended; implemented; no BIOS/UUID/serial query |
| GPU inventory | M1 `Win32_VideoController`; next compare DXGI and SetupAPI display class with `DIGCF_PRESENT` | WMI controller inventory is not a guaranteed complete list of present physical/render-capable GPUs | No elevation intended; WMI implemented; native cross-check deferred |
| Safe GPU hardware IDs | Extract PCI `VEN`/`DEV` only; DXGI numeric IDs later | Identifies a device type, not a unique instance; non-PCI adapters stay unknown | Implemented extraction; full join IDs are transient |
| Driver provider/version/date | M1 display-class `Win32_PnPSignedDriver`; future SetupAPI `DEVPKEY_Device_DriverProvider`, `DriverVersion`, `DriverDate` | Match exact device instance; version is the Windows driver version, not necessarily vendor marketing version; date is not installation time or a freshness verdict | WMI implemented; SetupAPI comparison deferred |
| Attached/active displays | DisplayConfig active paths + target device info | Active desktop paths are tractable; inactive, head-mounted, indirect, and disconnected devices require separate semantics | Standard console session target; deferred |
| Resolution/refresh | DisplayConfig source/target modes and rational refresh | Keep desktop/source dimensions separate from signal/target timing; virtual refresh and dynamic/variable refresh are not interchangeable | Deferred with virtual-mode-aware implementation |
| Display-to-adapter topology | DisplayConfig source/target adapter LUID + DXGI LUID; SetupAPI bridge where needed | Describes the OS display path at the collection instant; does not identify an application's rendering GPU or prove electrical MUX position | Deferred; no name/order/PCI-only join |
| Integrated/discrete classification | D3DKMT adapter-type flags; optionally D3D12 architecture/UMA evidence | Explicit hybrid flags provide evidence when set; absent flags do not prove the opposite; UMA describes memory architecture | Hardware/driver-dependent; all classifications unsupported in M1 |
| Hybrid-graphics context | Combine inventory, active paths, and explicitly supported capability evidence | Multiple GPUs alone are insufficient. Windows GPU preference ordering is not measurement of a running application | Deferred derived findings |
| Actual application GPU use, dGPU idle/sleep, MUX mode | Separate future measurement/vendor investigations | Static inventory cannot answer these questions reliably | Out of M1; do not claim an answer |

### System and driver inventory

Microsoft documents the requested numeric OS fields and manufacturer/model properties in [Win32_OperatingSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-operatingsystem) and [Win32_ComputerSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystem). M1 does not query caption, registered owner, computer name, or unrelated properties. These are WMI observations, not verified firmware truth.

[Win32_VideoController](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller) exposes controller identity, PnP ID, and driver/mode fields, but documents inaccurate values on non-WDDM hardware. Its 32-bit `AdapterRAM` is unsuitable as a general modern VRAM measurement; its current-resolution/refresh fields are not a topology model. M1 deliberately omits them.

[Win32_PnPSignedDriver](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/aa394354(v=vs.85)) supplies device ID, driver provider, version, and manufacturer build date. M1 limits the query to display-class records and performs an exact case-insensitive device-instance join. Missing or multiple matches stay unknown. Its legacy reference gives a month-day-year date example; current local WMI returned DMTF strings with unspecified fractional/timezone components. The parser retains a known calendar date and never guesses missing date components. WMI/CIM wrappers can transform representations; this was verified during the local smoke check.

For a stronger present-device inventory, [SetupDiGetClassDevsW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw) supports `DIGCF_PRESENT` and class filtering. Use [SetupDiGetDevicePropertyW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetdevicepropertyw) for typed properties, handle missing values and buffer sizes, and release the device-information set. This is a documented read path; do not add device installation/property setters.

The driver property references define [provider](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-driverprovider), [version](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-driverversion), and [date](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-driverdate). Version/date are supplied through driver INF metadata. Comparing these with WMI would help identify provider discrepancies, but M1 has not done that comparison.

### Display topology and DXGI

[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig) returns path/mode arrays; buffer sizing can race with topology changes. The next spike should query active paths, bound `ERROR_INSUFFICIENT_BUFFER` retries, use compatible virtual-mode/refresh flags, and validate returned mode indexes. It must distinguish an unsupported WDDM path from inaccessible console/remote-session results. Do not use `QDC_ALL_PATHS` by default, and do not call `SetDisplayConfig`.

[DisplayConfigGetDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo) supplies source/target/adapter information. Names and device paths require separate privacy handling; raw monitor paths and EDID must not be exported. A source path and a target path are related objects, not necessarily one monitor per unique source in clone mode.

[DXGI enumeration](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgifactory-enumadapters) and [DXGI_ADAPTER_DESC3](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/ns-dxgi1_6-dxgi_adapter_desc3) provide graphics-adapter descriptions, numeric vendor/device IDs, memory information, flags, and a local adapter LUID. LUID is suitable for in-session joins, not a persistent report identity. DXGI visibility can differ from PnP inventory; avoid deduplicating by model name or PCI IDs, as identical GPUs can coexist. Creating a rendering device is unnecessary for basic enumeration. The actual wake-up effects of enumeration remain unmeasured here.

### Classification and hybrid interpretation

[D3DKMT_ADAPTERTYPE](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adaptertype) includes `HybridDiscrete`, `HybridIntegrated`, software, indirect-display, and other capability flags; [D3DKMTQueryAdapterInfo](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/nf-d3dkmthk-d3dkmtqueryadapterinfo) is a documented query interface. Treat these as reported capabilities and test driver/session coverage before using them. They do not expose a universal current OEM MUX-switch state.

[D3D12_FEATURE_DATA_ARCHITECTURE1](https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_feature_data_architecture1) describes memory architecture including UMA. This is supporting evidence, not an unconditional retail iGPU/dGPU classifier; querying it requires D3D12 device support and adds activity to a diagnostic meant to understand idle behavior. Defer it until that tradeoff is tested.

[EnumAdapterByGpuPreference](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgifactory6-enumadapterbygpupreference) orders enumeration by a preference. It is not evidence of which adapter an already-running game is using. Vendor-name rules such as “Intel means integrated” or “NVIDIA means discrete” are not acceptable classification logic.

## What needs vendor-specific work or stays unresolved

- **Vendor/OEM-specific candidates:** explicit firmware graphics-mode labels, vendor control-panel state, vendor power/clock/utilization counters. There is no implemented vendor module; NVAPI/NVML, AMD ADL/ADLX, Intel interfaces, and OEM interfaces require a separate documented capability/license/privilege review before selection. Do not promise equal support across them.
- **Hardware/session-dependent:** physical port wiring, dock/MST behavior, eGPU removal, indirect/virtual adapters, sleep transitions, hybrid capability flags, accurate provider descriptions, and display access over RDP.
- **No universal answer established:** dGPU electrically asleep, why it woke, whether a game chose the user's intended GPU, or the effect of a graphics-mode change. These need measurements and correlated evidence, not static inventory or assumed vendor behavior.
- **Elevation:** none of the chosen M1 operations intentionally requests it; local success was verified with a non-administrator token. WMI namespace policy/provider permissions may deny access. Standard display queries can fail due to session access, which is not a general instruction to elevate. Any future privileged trace/provider feature needs its own documented reason and separate consent/scope.
- **Deferred packaging:** ARM64/x86, older Windows, self-contained distribution, signing, hardware matrix, hard cancellation, and vendor redistribution have not been validated.

## Feasibility conclusion

The small WMI inventory/report path is viable on the local Windows 11 x64 machine. The documented native APIs justify a focused display-topology spike in C#; they do not yet justify complete cross-hardware diagnostic claims. Keep WMI as an explicitly sourced inventory baseline while native collectors establish the additional semantics they can actually support.
