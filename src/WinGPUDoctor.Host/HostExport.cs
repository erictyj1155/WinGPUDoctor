using System.Text;

namespace WinGPUDoctor.Host;

public enum ExportDestinationStatus { Accepted, RejectedNamespace, NonFixedDrive, InvalidLocalDestination }

public sealed class ExportDestination
{
    public ExportDestinationStatus Status { get; }
    internal string? FullPath { get; }

    internal ExportDestination(ExportDestinationStatus status, string? fullPath = null)
    {
        Status = status;
        FullPath = fullPath;
    }
}

public static class HostExport
{
    public static ExportDestination ResolveDestination(string output)
    {
        try
        {
            var fullPath = Path.GetFullPath(output);
            // Match the CLI's direct network/device namespace and ADS rejection.
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || fullPath[2..].Contains(':'))
                return new(ExportDestinationStatus.RejectedNamespace);
            if (new DriveInfo(Path.GetPathRoot(fullPath)!).DriveType != DriveType.Fixed)
                return new(ExportDestinationStatus.NonFixedDrive);
            return new(ExportDestinationStatus.Accepted, fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return new(ExportDestinationStatus.InvalidLocalDestination);
        }
    }

    public static bool TryWriteNew(ExportDestination destination, string content)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Status != ExportDestinationStatus.Accepted || destination.FullPath is null)
            throw new ArgumentException("A validated local destination is required.", nameof(destination));
        try
        {
            using var stream = new FileStream(destination.FullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
