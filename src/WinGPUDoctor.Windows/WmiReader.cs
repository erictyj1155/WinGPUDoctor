using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Windows;

public enum WmiQuery { OperatingSystem, ComputerSystem, VideoControllers, DisplayDrivers }
public sealed record WmiRow(IReadOnlyDictionary<string, string?> Values)
{
    public string? Get(string name) => Values.TryGetValue(name, out var value) ? value : null;
}
public sealed record WmiResult(DataState State, IReadOnlyList<WmiRow> Rows, ReasonCode Reason)
{
    public static WmiResult Success(params WmiRow[] rows) => new(DataState.Available, rows, ReasonCode.None);
}
public interface IWmiReader { WmiResult Read(WmiQuery query); }

public sealed class WmiReader : IWmiReader
{
    // Fixed local queries only. Do not add SELECT *, remote scopes, or provider methods.
    public static (string ClassName, string[] Properties, string? Filter) Definition(WmiQuery query) => query switch
    {
        WmiQuery.OperatingSystem => ("Win32_OperatingSystem", ["Version", "BuildNumber"], null),
        WmiQuery.ComputerSystem => ("Win32_ComputerSystem", ["Manufacturer", "Model"], null),
        WmiQuery.VideoControllers => ("Win32_VideoController", ["Name", "PNPDeviceID"], null),
        WmiQuery.DisplayDrivers => ("Win32_PnPSignedDriver", ["DeviceID", "DriverProviderName", "DriverVersion", "DriverDate"], "DeviceClass = 'DISPLAY'"),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };

    public WmiResult Read(WmiQuery query)
    {
        var definition = Definition(query);
        try
        {
            var scope = new ManagementScope(@"\\.\root\cimv2", new ConnectionOptions
            {
                EnablePrivileges = false, Timeout = TimeSpan.FromSeconds(10)
            });
            var options = new System.Management.EnumerationOptions
            {
                ReturnImmediately = true, Rewindable = false, BlockSize = 16, Timeout = TimeSpan.FromSeconds(10)
            };
            var wql = $"SELECT {string.Join(", ", definition.Properties)} FROM {definition.ClassName}";
            if (definition.Filter is not null) wql += " WHERE " + definition.Filter;
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql), options);
            using var results = searcher.Get();
            var rows = new List<WmiRow>();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    // A buggy provider must not produce an unbounded report.
                    if (rows.Count >= 128) return new(DataState.Failed, [], ReasonCode.InvalidValue);
                    var fields = new Dictionary<string, string?>();
                    foreach (var property in definition.Properties)
                        fields[property] = Convert.ToString(item[property], CultureInfo.InvariantCulture);
                    rows.Add(new(fields));
                }
            }
            return new(DataState.Available, rows.ToArray(), ReasonCode.None);
        }
        catch (UnauthorizedAccessException) { return Failure(ReasonCode.AccessDenied); }
        catch (ManagementException ex)
        {
            return Failure(ex.ErrorCode switch
            {
                ManagementStatus.AccessDenied => ReasonCode.AccessDenied,
                ManagementStatus.Timedout => ReasonCode.Timeout,
                ManagementStatus.InvalidClass or ManagementStatus.InvalidNamespace => ReasonCode.ProviderUnavailable,
                _ => ReasonCode.QueryFailed
            });
        }
        catch (COMException ex)
        {
            return Failure(ex.HResult == unchecked((int)0x80070005) ? ReasonCode.AccessDenied : ReasonCode.QueryFailed);
        }
        // No exception messages, WMI object paths, stack traces, or raw objects leave this boundary.
    }
    private static WmiResult Failure(ReasonCode reason) => new(DataState.Failed, [], reason);
}
