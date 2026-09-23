using WinGPUDoctor.Core;
using System.Text.Json.Serialization;

namespace WinGPUDoctor.Protocol;

internal sealed record DisplayTopologyIdentity(
    [property: JsonRequired] string Label,
    [property: JsonRequired] string? TransientInstanceId);
internal sealed record DisplayActiveTopologyRequest(
    [property: JsonRequired] IReadOnlyList<DisplayTopologyIdentity> Inventory);
internal sealed record WorkerRequestPayload(
    [property: JsonRequired] DisplayActiveTopologyRequest? DisplayActiveTopology);

internal sealed record WmiOperatingSystemResult(
    [property: JsonRequired] Observation<string> WindowsVersion,
    [property: JsonRequired] Observation<string> WindowsBuild);
internal sealed record WmiComputerSystemResult(
    [property: JsonRequired] Observation<string> Manufacturer,
    [property: JsonRequired] Observation<string> Model);
internal sealed record WmiVideoControllerFact(
    [property: JsonRequired] string? TransientInstanceId,
    [property: JsonRequired] Observation<string> Name,
    [property: JsonRequired] Observation<string> PciVendorId,
    [property: JsonRequired] Observation<string> PciDeviceId);
internal sealed record WmiVideoControllersResult(
    [property: JsonRequired] Observation<IReadOnlyList<WmiVideoControllerFact>> Controllers);
internal sealed record WmiDisplayDriverFact(
    [property: JsonRequired] string? TransientDeviceInstanceId,
    [property: JsonRequired] Observation<string> Provider,
    [property: JsonRequired] Observation<string> Version,
    [property: JsonRequired] Observation<string> Date);
internal sealed record WmiDisplayDriversResult(
    [property: JsonRequired] Observation<IReadOnlyList<WmiDisplayDriverFact>> Drivers);
internal sealed record DisplayActiveTopologyResult(
    [property: JsonRequired] Observation<IReadOnlyList<DisplayFacts>> Displays,
    [property: JsonRequired] CollectorRun Run);

internal sealed record WorkerResultPayload(
    [property: JsonRequired] WmiOperatingSystemResult? WmiOperatingSystem,
    [property: JsonRequired] WmiComputerSystemResult? WmiComputerSystem,
    [property: JsonRequired] WmiVideoControllersResult? WmiVideoControllers,
    [property: JsonRequired] WmiDisplayDriversResult? WmiDisplayDrivers,
    [property: JsonRequired] DisplayActiveTopologyResult? DisplayActiveTopology);
