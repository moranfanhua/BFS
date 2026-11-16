namespace BlackFlowSpire.Map.Models;

public sealed record BlackFlowSpireFloorDefinition(int NodeCount, int SourceActIndex, BlackFlowSpireNodeContent ExitContent,
    bool AllowPassages = true, bool AllowVantagePoints = true);

// Floor indices are zero-based; source acts refer to the original three-act run.
public static class BlackFlowSpireFloorPlan
{
    public static IReadOnlyList<BlackFlowSpireFloorDefinition> Floors { get; } = Array.AsReadOnly<BlackFlowSpireFloorDefinition>(
    [
        new(25, 0, BlackFlowSpireNodeContent.Final, AllowPassages: false, AllowVantagePoints: false),
        new(35, 0, BlackFlowSpireNodeContent.Boss),
        new(45, 1, BlackFlowSpireNodeContent.Final),
        new(49, 1, BlackFlowSpireNodeContent.Boss),
        new(49, 2, BlackFlowSpireNodeContent.Boss)
    ]);

    public static BlackFlowSpireFloorDefinition Get(int actIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actIndex);
        if (actIndex >= Floors.Count) throw new ArgumentOutOfRangeException(nameof(actIndex));
        return Floors[actIndex];
    }

    public static BlackFlowSpireGenerationOptions GenerationOptions(int actIndex) => new() { NodeCount = Get(actIndex).NodeCount };
}
