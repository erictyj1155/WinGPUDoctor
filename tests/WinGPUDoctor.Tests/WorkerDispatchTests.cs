using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Windows;
using WinGPUDoctor.Worker;
using Xunit;

namespace WinGPUDoctor.Tests;

public class WorkerDispatchTests
{
    private static WmiRow Row(params (string Key, string? Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value));

    [Fact]
    public void OperatingSystemAndComputerSystemPreserveObservationsAndFailures()
    {
        var reader = new FakeReader
        {
            [WmiQuery.OperatingSystem] = WmiResult.Success(Row(("Version", "10.0.26200"), ("BuildNumber", "26200"))),
            [WmiQuery.ComputerSystem] = WmiResult.Success(Row(("Manufacturer", "Example OEM"), ("Model", "Example Model")))
        };
        var dispatcher = new WorkerOperationDispatcher(reader, (_, _) => throw new InvalidOperationException());

        var os = dispatcher.Dispatch(WorkerOperation.WmiOperatingSystem, new WorkerRequestPayload(null));
        var system = dispatcher.Dispatch(WorkerOperation.WmiComputerSystem, new WorkerRequestPayload(null));

        Assert.Equal("10.0.26200", os.Frame.Payload!.WmiOperatingSystem!.WindowsVersion.Value);
        Assert.Equal("26200", os.Frame.Payload.WmiOperatingSystem.WindowsBuild.Value);
        Assert.Equal("Example OEM", system.Frame.Payload!.WmiComputerSystem!.Manufacturer.Value);
        Assert.Equal("Example Model", system.Frame.Payload.WmiComputerSystem.Model.Value);

        reader[WmiQuery.OperatingSystem] = new(DataState.Failed, [], ReasonCode.AccessDenied);
        var failure = dispatcher.Dispatch(WorkerOperation.WmiOperatingSystem, new WorkerRequestPayload(null));
        Assert.Equal(WorkerResultState.Failed, failure.Frame.State);
        Assert.Equal(ReasonCode.AccessDenied, failure.Frame.Reason);
        Assert.Null(failure.Frame.Payload);
    }

    [Fact]
    public void VideoControllersPreserveOrderPciTypeIdsAndTransientIdentity()
    {
        var reader = new FakeReader
        {
            [WmiQuery.VideoControllers] = WmiResult.Success(
                Row(("Name", "GPU A"), ("PNPDeviceID", @"PCI\VEN_10DE&DEV_1234\PRIVATE_A")),
                Row(("Name", "GPU B"), ("PNPDeviceID", @"USB\VID_1234&PID_5678\PRIVATE_B")))
        };
        var dispatcher = new WorkerOperationDispatcher(reader, (_, _) => throw new InvalidOperationException());
        var outcome = dispatcher.Dispatch(WorkerOperation.WmiVideoControllers, new WorkerRequestPayload(null));
        var controllers = outcome.Frame.Payload!.WmiVideoControllers!.Controllers.Value!;

        Assert.Equal(2, controllers.Count);
        Assert.Equal("GPU A", controllers[0].Name.Value);
        Assert.Equal(@"PCI\VEN_10DE&DEV_1234\PRIVATE_A", controllers[0].TransientInstanceId);
        Assert.Equal("10DE", controllers[0].PciVendorId.Value);
        Assert.Equal("1234", controllers[0].PciDeviceId.Value);
        Assert.Equal("GPU B", controllers[1].Name.Value);
        Assert.Equal(ReasonCode.NonPciDevice, controllers[1].PciVendorId.Reason);
    }

    [Fact]
    public void DisplayDriversPreserveTransientDeviceIdAndNormalizedDate()
    {
        var reader = new FakeReader
        {
            [WmiQuery.DisplayDrivers] = WmiResult.Success(Row(
                ("DeviceID", @"PCI\VEN_10DE&DEV_1234\PRIVATE_A"),
                ("DriverProviderName", "Provider"),
                ("DriverVersion", "2.0"),
                ("DriverDate", "20260821000000.******+***")))
        };
        var dispatcher = new WorkerOperationDispatcher(reader, (_, _) => throw new InvalidOperationException());
        var outcome = dispatcher.Dispatch(WorkerOperation.WmiDisplayDrivers, new WorkerRequestPayload(null));
        var driver = Assert.Single(outcome.Frame.Payload!.WmiDisplayDrivers!.Drivers.Value!);

        Assert.Equal(@"PCI\VEN_10DE&DEV_1234\PRIVATE_A", driver.TransientDeviceInstanceId);
        Assert.Equal("Provider", driver.Provider.Value);
        Assert.Equal("2.0", driver.Version.Value);
        Assert.Equal("2026-08-21", driver.Date.Value);
    }

    [Fact]
    public void TopologyDispatchPassesTransientInputAndAttemptCount()
    {
        IReadOnlyList<GpuCorrelationIdentity>? seen = null;
        var runner = new WorkerOperationDispatcher(new FakeReader(), (inventory, _) =>
        {
            seen = inventory;
            return new TopologyResult(Observation<IReadOnlyList<DisplayFacts>>.Known(
                [ProtocolTests.Display("gpu-1")], DataSource.DisplayConfig),
                ProtocolTests.Run() with
                {
                    Attempts = 2,
                    Issues = [new(CollectionOperation.QueryPaths, ReasonCode.TopologyChanged, 122),
                        new(CollectionOperation.TargetName, ReasonCode.MissingValue, null)]
                });
        });
        var start = new WorkerRequestPayload(new DisplayActiveTopologyRequest(
            [new("gpu-1", "PRIVATE_A"), new("gpu-2", null)]));

        var outcome = runner.Dispatch(WorkerOperation.DisplayActiveTopology, start);

        Assert.Equal(2, outcome.Attempts);
        Assert.NotNull(seen);
        Assert.Equal(["gpu-1", "gpu-2"], seen!.Select(item => item.GpuId));
        Assert.Equal("PRIVATE_A", seen[0].InstanceId);
        Assert.Null(seen[1].InstanceId);
    }

    [Fact]
    public void TopologyPreQueryFailureUsesZeroAttempts()
    {
        var run = new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.ApiUnavailable)
        {
            Attempts = 0,
            Issues = [new(CollectionOperation.QueryPaths, ReasonCode.ApiUnavailable, null)]
        };
        var runner = new WorkerOperationDispatcher(new FakeReader(), (_, _) => new TopologyResult(
            Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Unsupported, DataSource.DisplayConfig, ReasonCode.ApiUnavailable), run));
        var outcome = runner.Dispatch(WorkerOperation.DisplayActiveTopology,
            new WorkerRequestPayload(new DisplayActiveTopologyRequest([])));

        Assert.Equal(0, outcome.Attempts);
        Assert.Equal(WorkerResultState.Unsupported, outcome.Frame.State);
        Assert.Equal(ReasonCode.ApiUnavailable, outcome.Frame.Reason);
        Assert.NotNull(outcome.Frame.Payload?.DisplayActiveTopology);
    }

    [Fact]
    public void TopologyWithoutStartPayloadIsRejected()
    {
        var dispatcher = new WorkerOperationDispatcher(new FakeReader(), (_, _) => throw new InvalidOperationException());
        Assert.Throws<ProtocolValidationException>(() =>
            dispatcher.Dispatch(WorkerOperation.DisplayActiveTopology, new WorkerRequestPayload(null)));
    }

    private sealed class FakeReader : Dictionary<WmiQuery, WmiResult>, IWmiReader
    {
        public WmiResult Read(WmiQuery query) => TryGetValue(query, out var result)
            ? result : new(DataState.Failed, [], ReasonCode.ProviderUnavailable);
    }
}
