using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Windows;
using Xunit;

namespace WinGPUDoctor.Tests;

public class CollectorTests
{
    private sealed class FakeReader : IWmiReader
    {
        public Dictionary<WmiQuery, WmiResult> Results { get; } = new()
        {
            [WmiQuery.OperatingSystem] = WmiResult.Success(Row(("Version", "10.0.26200"), ("BuildNumber", "26200"))),
            [WmiQuery.ComputerSystem] = WmiResult.Success(Row(("Manufacturer", "Example"), ("Model", "Example Laptop"))),
            [WmiQuery.VideoControllers] = WmiResult.Success(Row(("Name", "Example GPU"), ("PNPDeviceID", @"PCI\VEN_10DE&DEV_1234\PRIVATE_SUFFIX"))),
            [WmiQuery.DisplayDrivers] = WmiResult.Success(Row(("DeviceID", @"pci\ven_10de&dev_1234\private_suffix"),
                ("DriverProviderName", "Example Provider"), ("DriverVersion", "32.0.15.1234"), ("DriverDate", "20260821000000.000000+000")))
        };
        public WmiResult Read(WmiQuery query) => Results[query];
    }
    private static WmiRow Row(params (string Key, string? Value)[] values) => new(values.ToDictionary(x => x.Key, x => x.Value));

    [Fact] public void JoinsDriversByExactCaseInsensitiveInstanceIdAndDropsSuffix()
    {
        var report = new WindowsCollector(new FakeReader()).Collect();
        var gpu = Assert.Single(report.Facts.Gpus.Value!);
        Assert.Equal("10DE", gpu.PciVendorId.Value);
        Assert.Equal("1234", gpu.PciDeviceId.Value);
        Assert.Equal("Example Provider", gpu.Driver.Provider.Value);
        Assert.Equal("2026-08-21", gpu.Driver.Date.Value);
        Assert.DoesNotContain("PRIVATE_SUFFIX", ReportWriter.Json(PrivacyPolicy.Prepare(report, new(2026, 9, 10))));
    }
    [Fact] public void DuplicateDriverMatchIsUnknownNotAnArbitraryChoice()
    {
        var fake = new FakeReader();
        var row = fake.Results[WmiQuery.DisplayDrivers].Rows[0];
        fake.Results[WmiQuery.DisplayDrivers] = WmiResult.Success(row, row);
        var result = new WindowsCollector(fake).Collect();
        Assert.Equal(ReasonCode.AmbiguousDriver, result.Facts.Gpus.Value![0].Driver.Provider.Reason);
        Assert.Contains(result.Collection, c => c.Source == DataSource.WmiSignedDriver && c.Status == CollectorStatus.Partial);
    }
    [Fact] public void UnmatchedDriverDoesNotBorrowAnotherGpusVersion()
    {
        var fake = new FakeReader();
        fake.Results[WmiQuery.DisplayDrivers] = WmiResult.Success(Row(("DeviceID", "OTHER"), ("DriverVersion", "1.2.3.4")));
        Assert.Equal(ReasonCode.NoMatchingDriver, new WindowsCollector(fake).Collect().Facts.Gpus.Value![0].Driver.Version.Reason);
    }
    [Fact] public void DriverFailurePreservesGpuInventory()
    {
        var fake = new FakeReader();
        fake.Results[WmiQuery.DisplayDrivers] = new(DataState.Failed, [], ReasonCode.AccessDenied);
        var result = new WindowsCollector(fake).Collect();
        Assert.Equal(DataState.Available, result.Facts.Gpus.State);
        Assert.Equal(DataState.Failed, result.Facts.Gpus.Value![0].Driver.Provider.State);
    }
    [Fact] public void OsFailurePreservesOtherCollectors()
    {
        var fake = new FakeReader();
        fake.Results[WmiQuery.OperatingSystem] = new(DataState.Failed, [], ReasonCode.Timeout);
        var result = new WindowsCollector(fake).Collect();
        Assert.Equal(ReasonCode.Timeout, result.Facts.System.WindowsVersion.Reason);
        Assert.Equal(DataState.Available, result.Facts.Gpus.State);
    }
    [Fact] public void MissingModelIsExplicitAndPartial()
    {
        var fake = new FakeReader();
        fake.Results[WmiQuery.ComputerSystem] = WmiResult.Success(Row(("Manufacturer", "Example")));
        var result = new WindowsCollector(fake).Collect();
        Assert.Equal(DataState.Unknown, result.Facts.System.Model.State);
        Assert.Contains(result.Collection, c => c.Source == DataSource.WmiComputerSystem && c.Status == CollectorStatus.Partial);
    }
    [Theory]
    [InlineData(null, ReasonCode.MissingValue)]
    [InlineData("USB\\VID_1234&PID_5678\\SERIAL", ReasonCode.NonPciDevice)]
    [InlineData("PCI\\VEN_GGGG&DEV_1234", ReasonCode.InvalidValue)]
    [InlineData("PCI\\VEN_1234&DEV_56789", ReasonCode.InvalidValue)]
    public void UnusablePciIdsAreNotGuessed(string? input, ReasonCode reason)
    {
        var (vendor, device) = WindowsCollector.PciIds(input);
        Assert.Equal(reason, vendor.Reason); Assert.Null(device.Value);
    }
    [Theory]
    [InlineData("20260230000000.000000+000")]
    [InlineData("2026****000000.000000+***")]
    [InlineData("unknown")]
    public void InvalidDatesAreNotGuessed(string value) => Assert.Equal(ReasonCode.InvalidValue, WindowsCollector.DriverDate(value).Reason);

    [Theory]
    [InlineData("20260824000000.******+***", "2026-08-24")]
    [InlineData("20260513000000.******+***", "2026-05-13")]
    [InlineData("20260101******.******+***", "2026-01-01")]
    [InlineData("1-25-2001", "2001-01-25")]
    public void KnownCalendarDateSurvivesUnspecifiedTimePrecision(string value, string expected) =>
        Assert.Equal(expected, WindowsCollector.DriverDate(value).Value);

    [Fact] public void QueriesAreNarrowLocalDefinitions()
    {
        var properties = Enum.GetValues<WmiQuery>().SelectMany(q => WmiReader.Definition(q).Properties).ToArray();
        foreach (var forbidden in new[] { "*", "UserName", "SerialNumber", "SystemName", "InstallDate", "IPAddress", "MACAddress" })
            Assert.DoesNotContain(forbidden, properties);
        Assert.Equal("DeviceClass = 'DISPLAY'", WmiReader.Definition(WmiQuery.DisplayDrivers).Filter);
    }

    [Fact] public void InjectedDisplayFailurePreservesRealCollectorComposition()
    {
        var api = TopologyTests.Single(); api.SizeError = 5;
        var result = new WindowsCollector(new FakeReader(), new DisplayTopologyCollector(api, api, DisplayQueryMode.VirtualModeAndRefreshAware)).Collect();
        Assert.Equal(DataState.Available, result.Facts.Gpus.State);
        Assert.Equal(DataState.Available, result.Facts.System.WindowsBuild.State);
        Assert.Equal(ReasonCode.SessionAccessDenied, result.Facts.Displays.Reason);
    }
    [Fact] public void FailedWmiInventoryDoesNotStopActivePathCollection()
    {
        var api = TopologyTests.Single(); var wmi = new FakeReader();
        wmi.Results[WmiQuery.VideoControllers] = new(DataState.Failed, [], ReasonCode.AccessDenied);
        var result = new WindowsCollector(wmi, new DisplayTopologyCollector(api, api, DisplayQueryMode.VirtualModeAndRefreshAware)).Collect();
        Assert.Equal(DataState.Available, result.Facts.Displays.State);
        Assert.Equal(ReasonCode.UnmatchedAdapter, result.Facts.Displays.Value![0].SourceAdapter.Reason);
        Assert.Equal(DataState.Failed, result.Facts.Gpus.State);
    }
    [Fact] public void MissingFriendlyNameDoesNotHideIndependentWmiFailure()
    {
        var api = TopologyTests.Single(); api.EmptyFriendly = true;
        api.InstanceIds[TopologyTests.RawPathA] = @"PCI\VEN_10DE&DEV_1234\PRIVATE_SUFFIX";
        var wmi = new FakeReader();
        wmi.Results[WmiQuery.OperatingSystem] = new(DataState.Failed, [], ReasonCode.Timeout);
        var result = new WindowsCollector(wmi, new DisplayTopologyCollector(api, api, DisplayQueryMode.VirtualModeAndRefreshAware)).Collect();
        var displayRun = result.Collection.Single(c => c.Source == DataSource.DisplayConfig);
        Assert.Equal(CollectorStatus.Succeeded, displayRun.Status);
        Assert.False(displayRun.IsIncomplete());

        var report = JsonSerializer.Deserialize<DiagnosticReport>(
            ReportWriter.Json(PrivacyPolicy.Prepare(result, new(2026, 9, 13))), ReportWriter.JsonOptions)!;
        Assert.Contains(report.Collection, c => c.Source == DataSource.WmiOperatingSystem && c.IsIncomplete());
        Assert.Contains(WarningCode.CollectionIncomplete, report.Warnings);
    }
}
