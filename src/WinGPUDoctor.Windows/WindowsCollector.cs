using System.Globalization;
using System.Text.RegularExpressions;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Windows;

public sealed class WindowsCollector(IWmiReader reader) : IDiagnosticCollector
{
    private static Observation<string> Missing(DataSource source, ReasonCode reason = ReasonCode.MissingValue) =>
        Observation<string>.Absent(DataState.Unknown, source, reason);
    private static Observation<string> Text(WmiRow row, string name, DataSource source) =>
        string.IsNullOrWhiteSpace(row.Get(name)) ? Missing(source) : Observation<string>.Known(row.Get(name)!, source);
    private static Observation<string> Single(WmiResult result, string property, DataSource source)
    {
        if (result.State != DataState.Available) return Observation<string>.Absent(result.State, source, result.Reason);
        return result.Rows.Count == 1 ? Text(result.Rows[0], property, source) : Missing(source, result.Rows.Count == 0 ? ReasonCode.MissingValue : ReasonCode.InvalidValue);
    }

    public static (Observation<string> Vendor, Observation<string> Device) PciIds(string? instanceId)
    {
        const DataSource source = DataSource.WmiVideoController;
        if (string.IsNullOrWhiteSpace(instanceId)) return (Missing(source), Missing(source));
        if (!instanceId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase))
            return (Missing(source, ReasonCode.NonPciDevice), Missing(source, ReasonCode.NonPciDevice));
        // Match only numeric type identifiers. Never return the device-instance suffix.
        var match = Regex.Match(instanceId.Length <= 1024 ? instanceId : "", @"\APCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})(?:&|\\|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success
            ? (Observation<string>.Known(match.Groups[1].Value.ToUpperInvariant(), source), Observation<string>.Known(match.Groups[2].Value.ToUpperInvariant(), source))
            : (Missing(source, ReasonCode.InvalidValue), Missing(source, ReasonCode.InvalidValue));
    }

    public static Observation<string> DriverDate(string? value)
    {
        const DataSource source = DataSource.WmiSignedDriver;
        if (string.IsNullOrWhiteSpace(value)) return Missing(source);
        // Retain the provider calendar date without local timezone conversion or invented time.
        if (value.Length > 64) return Missing(source, ReasonCode.InvalidValue);
        // Providers can leave subsecond/timezone precision unspecified using DMTF wildcards.
        // Only the calendar date is exported, so unknown time components must not invalidate it.
        var dmtf = Regex.IsMatch(value, @"\A\d{8}(?:\d{2}|\*{2}){3}\.(?:\d{6}|\*{6})[+-](?:\d{3}|\*{3})\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (dmtf && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return Observation<string>.Known(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), source);
        // The legacy Microsoft class reference also documents a month-day-year string.
        if (DateOnly.TryParseExact(value, "M-d-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return Observation<string>.Known(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), source);
        return Missing(source, ReasonCode.InvalidValue);
    }

    private static DriverFacts Driver(string? instanceId, WmiResult result)
    {
        const DataSource source = DataSource.WmiSignedDriver;
        DriverFacts Absent(DataState state, ReasonCode reason)
        {
            var field = Observation<string>.Absent(state, source, reason);
            return new(field, field, field);
        }
        if (result.State != DataState.Available) return Absent(result.State, result.Reason);
        if (string.IsNullOrWhiteSpace(instanceId)) return Absent(DataState.Unknown, ReasonCode.NoMatchingDriver);
        var matches = result.Rows.Where(r => string.Equals(r.Get("DeviceID"), instanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) return Absent(DataState.Unknown, matches.Length == 0 ? ReasonCode.NoMatchingDriver : ReasonCode.AmbiguousDriver);
        return new(Text(matches[0], "DriverProviderName", source), Text(matches[0], "DriverVersion", source), DriverDate(matches[0].Get("DriverDate")));
    }

    public CollectionSnapshot Collect()
    {
        var os = reader.Read(WmiQuery.OperatingSystem);
        var machine = reader.Read(WmiQuery.ComputerSystem);
        var video = reader.Read(WmiQuery.VideoControllers);
        // No reason to enumerate display driver records when the GPU query failed or is empty.
        var drivers = video.State == DataState.Available && video.Rows.Count > 0 ? reader.Read(WmiQuery.DisplayDrivers) : null;
        var system = new SystemFacts(Single(os, "Version", DataSource.WmiOperatingSystem), Single(os, "BuildNumber", DataSource.WmiOperatingSystem),
            Single(machine, "Manufacturer", DataSource.WmiComputerSystem), Single(machine, "Model", DataSource.WmiComputerSystem));
        var gpuList = new List<GpuFacts>();
        if (video.State == DataState.Available)
            foreach (var row in video.Rows)
            {
                var (vendor, device) = PciIds(row.Get("PNPDeviceID"));
                gpuList.Add(new($"gpu-{gpuList.Count + 1}", Text(row, "Name", DataSource.WmiVideoController), vendor, device,
                    Observation<string>.Absent(DataState.Unsupported, DataSource.NotCollected, ReasonCode.NotImplemented),
                    Driver(row.Get("PNPDeviceID"), drivers!)));
            }
        var gpus = video.State == DataState.Available
            ? Observation<IReadOnlyList<GpuFacts>>.Known(gpuList.ToArray(), DataSource.WmiVideoController)
            : Observation<IReadOnlyList<GpuFacts>>.Absent(video.State, DataSource.WmiVideoController, video.Reason);
        var displays = Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Unsupported, DataSource.NotCollected, ReasonCode.NotImplemented);
        static CollectorRun Run(WmiResult result, DataSource source, bool partial) =>
            new(source, result.State == DataState.Available ? partial ? CollectorStatus.Partial : CollectorStatus.Succeeded : CollectorStatus.Failed,
                result.State == DataState.Available ? partial ? ReasonCode.MissingValue : ReasonCode.None : result.Reason);
        var collection = new List<CollectorRun>
        {
            Run(os, DataSource.WmiOperatingSystem, system.WindowsVersion.State != DataState.Available || system.WindowsBuild.State != DataState.Available),
            Run(machine, DataSource.WmiComputerSystem, system.Manufacturer.State != DataState.Available || system.Model.State != DataState.Available),
            Run(video, DataSource.WmiVideoController, gpuList.Any(g => g.Name.State != DataState.Available)),
            new(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.NotImplemented)
        };
        if (drivers is not null) collection.Add(Run(drivers, DataSource.WmiSignedDriver,
            gpuList.Any(g => g.Driver.Provider.State != DataState.Available || g.Driver.Version.State != DataState.Available || g.Driver.Date.State != DataState.Available)));
        return new(new(system, gpus, displays), collection.ToArray());
    }
}
