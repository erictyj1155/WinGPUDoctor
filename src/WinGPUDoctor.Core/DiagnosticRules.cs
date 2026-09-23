using System.Globalization;
using System.Text.Json;

namespace WinGPUDoctor.Core;

public static class DiagnosticRules
{
    public static IReadOnlyList<DiagnosticFinding> Evaluate(CollectedFacts facts)
    {
        var findings = new List<DiagnosticFinding>();
        if (facts.Gpus.State == DataState.Available && facts.Gpus.Value!.Count > 1)
            findings.Add(new("inventory.multiple-adapters", "information",
                "Windows reports multiple video controllers. This alone does not establish hybrid mode, display routing, GPU power state, or which GPU an application uses.",
                ["facts.gpus"]));
        if (facts.Gpus.State == DataState.Available && facts.Gpus.Value!.Count == 0)
            findings.Add(new("inventory.empty", "information",
                "The provider returned no video controllers. This is not proof that the computer has no GPU.", ["facts.gpus"]));

        if (facts.Displays.State != DataState.Available) return findings;
        var displays = facts.Displays.Value!;
        if (displays.Count == 0)
        {
            findings.Add(new("topology.no-active-paths", "information",
                "No active display paths are represented in this report's available topology result. This does not establish that GPUs, monitors, or display hardware are absent.",
                ["facts.displays"]));
            return findings;
        }

        const string limitation = "These associations do not establish application rendering, workload ownership, electrical routing, GPU preference, power state, graphics mode, health, or driver correctness.";
        for (var i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            if (!display.PathActive) continue;
            var path = "facts.displays.value[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            var associationEvidence = new[] { path + ".id", path + ".pathActive", path + ".sourceAdapter", path + ".targetAdapter" };
            // Available matches in the shareable report have already passed the exact projection invariant.
            if (display.SourceAdapter.State == DataState.Available && display.TargetAdapter.State == DataState.Available)
                findings.Add(new("topology.endpoint-adapter-association", "information",
                    $"For {display.Id}, the source endpoint is exactly associated with {display.SourceAdapter.Value!.GpuId}, and the target endpoint is exactly associated with {display.TargetAdapter.Value!.GpuId}, through identity correlation in this report. {limitation}",
                    associationEvidence));
            else
                findings.Add(new("topology.correlation-unresolved", "information",
                    $"For {display.Id}: source endpoint {Endpoint(display.SourceAdapter)}; target endpoint {Endpoint(display.TargetAdapter)}. {limitation}",
                    associationEvidence));

            if (!display.TargetAvailable)
                findings.Add(new("topology.active-path-target-unavailable", "information",
                    $"For {display.Id}, the path is recorded as active, while its target is recorded as unavailable. This observation does not establish a cause.",
                    [path + ".id", path + ".pathActive", path + ".targetAvailable"]));
        }
        return findings;
    }

    private static string Endpoint(Observation<AdapterMatch> match) => match.State == DataState.Available
        ? $"is exactly associated with {match.Value!.GpuId} through identity correlation in this report"
        : $"association was not established in this report (state: {Token(match.State)}; source: {Token(match.Source)}; reason: {Token(match.Reason)})";

    private static string Token<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
