using WinGPUDoctor.Core;

namespace WinGPUDoctor.Protocol;

internal static class ProtocolConstants
{
    internal const int Version = 1;
    internal const int MaxControlFrameBytes = 4 * 1024;
    internal const int MaxDataFrameBytes = 1024 * 1024;
    internal const int MaxDecodedStringChars = 16 * 1024;
    internal const int MaxJsonDepth = 32;
    internal const int MaxParseDepth = 512;
    internal const int MaxCollectionItems = 512;
    internal const int MaxFrameCount = 7;
    internal const string WmiOperatingSystem = "wmi.operatingSystem";
    internal const string WmiComputerSystem = "wmi.computerSystem";
    internal const string WmiVideoControllers = "wmi.videoControllers";
    internal const string WmiDisplayDrivers = "wmi.displayDrivers";
    internal const string DisplayActiveTopology = "display.activeTopology";
}

internal enum ProtocolFrameKind { Request, Ready, Start, AttemptStarted, Result, Failure }

internal enum WorkerOperation
{
    WmiOperatingSystem,
    WmiComputerSystem,
    WmiVideoControllers,
    WmiDisplayDrivers,
    DisplayActiveTopology
}

internal enum WorkerResultState { Succeeded, Partial, Failed, Unsupported }

internal static class ProtocolNames
{
    internal static string ToWire(this WorkerOperation operation) => operation switch
    {
        WorkerOperation.WmiOperatingSystem => ProtocolConstants.WmiOperatingSystem,
        WorkerOperation.WmiComputerSystem => ProtocolConstants.WmiComputerSystem,
        WorkerOperation.WmiVideoControllers => ProtocolConstants.WmiVideoControllers,
        WorkerOperation.WmiDisplayDrivers => ProtocolConstants.WmiDisplayDrivers,
        WorkerOperation.DisplayActiveTopology => ProtocolConstants.DisplayActiveTopology,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    internal static bool TryParseOperation(string? value, out WorkerOperation operation)
    {
        operation = value switch
        {
            ProtocolConstants.WmiOperatingSystem => WorkerOperation.WmiOperatingSystem,
            ProtocolConstants.WmiComputerSystem => WorkerOperation.WmiComputerSystem,
            ProtocolConstants.WmiVideoControllers => WorkerOperation.WmiVideoControllers,
            ProtocolConstants.WmiDisplayDrivers => WorkerOperation.WmiDisplayDrivers,
            ProtocolConstants.DisplayActiveTopology => WorkerOperation.DisplayActiveTopology,
            _ => default
        };
        return value is ProtocolConstants.WmiOperatingSystem or ProtocolConstants.WmiComputerSystem or
            ProtocolConstants.WmiVideoControllers or ProtocolConstants.WmiDisplayDrivers or
            ProtocolConstants.DisplayActiveTopology;
    }

    internal static string ToWire(this ProtocolFrameKind kind) => kind switch
    {
        ProtocolFrameKind.Request => "request",
        ProtocolFrameKind.Ready => "ready",
        ProtocolFrameKind.Start => "start",
        ProtocolFrameKind.AttemptStarted => "attemptStarted",
        ProtocolFrameKind.Result => "result",
        ProtocolFrameKind.Failure => "failure",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static bool TryParseFrameKind(string? value, out ProtocolFrameKind kind)
    {
        kind = value switch
        {
            "request" => ProtocolFrameKind.Request,
            "ready" => ProtocolFrameKind.Ready,
            "start" => ProtocolFrameKind.Start,
            "attemptStarted" => ProtocolFrameKind.AttemptStarted,
            "result" => ProtocolFrameKind.Result,
            "failure" => ProtocolFrameKind.Failure,
            _ => default
        };
        return value is "request" or "ready" or "start" or "attemptStarted" or "result" or "failure";
    }

    internal static string ToWire(this WorkerResultState state) => state switch
    {
        WorkerResultState.Succeeded => "succeeded",
        WorkerResultState.Partial => "partial",
        WorkerResultState.Failed => "failed",
        WorkerResultState.Unsupported => "unsupported",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    internal static bool TryParseResultState(string? value, out WorkerResultState state)
    {
        state = value switch
        {
            "succeeded" => WorkerResultState.Succeeded,
            "partial" => WorkerResultState.Partial,
            "failed" => WorkerResultState.Failed,
            "unsupported" => WorkerResultState.Unsupported,
            _ => default
        };
        return value is "succeeded" or "partial" or "failed" or "unsupported";
    }
}

internal sealed class ProtocolValidationException(ReasonCode reason, string message) : Exception(message)
{
    internal ReasonCode Reason { get; } = reason;
}
