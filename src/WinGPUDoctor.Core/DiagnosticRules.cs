namespace WinGPUDoctor.Core;

public static class DiagnosticRules
{
    public static IReadOnlyList<DiagnosticFinding> Evaluate(CollectedFacts facts)
    {
        if (facts.Gpus.State == DataState.Available && facts.Gpus.Value!.Count > 1)
            return [new("inventory.multiple-adapters", "information",
                "Windows reports multiple video controllers. This alone does not establish hybrid mode, display routing, GPU power state, or which GPU an application uses.",
                ["facts.gpus"])];
        if (facts.Gpus.State == DataState.Available && facts.Gpus.Value!.Count == 0)
            return [new("inventory.empty", "information",
                "The provider returned no video controllers. This is not proof that the computer has no GPU.", ["facts.gpus"])];
        return [];
    }
}
