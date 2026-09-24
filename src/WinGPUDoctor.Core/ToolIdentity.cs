using System.Reflection;

namespace WinGPUDoctor.Core;

public static class ToolIdentity
{
    public static string Version { get; } = typeof(ToolIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Tool version metadata is missing.");
}
