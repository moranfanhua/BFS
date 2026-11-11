namespace BlackFlowSpire.Map.Models;

public sealed record ForestFloorDefinition(int NodeCount, int SourceActIndex, ForestNodeContent ExitContent,
    bool AllowPassages = true, bool AllowVantagePoints = true);

// Floor indices are zero-based; source acts refer to the original three-act run.
public static class ForestFloorPlan
{
    public static IReadOnlyList<ForestFloorDefinition> Floors { get; } = Array.AsReadOnly<ForestFloorDefinition>(
    [
        new(25, 0, ForestNodeContent.Final, AllowPassages: false, AllowVantagePoints: false),
        new(35, 0, ForestNodeContent.Boss),
        new(45, 1, ForestNodeContent.Final),
        new(49, 1, ForestNodeContent.Boss),
        new(49, 2, ForestNodeContent.Boss)
    ]);

    public static ForestFloorDefinition Get(int actIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actIndex);
        if (actIndex >= Floors.Count) throw new ArgumentOutOfRangeException(nameof(actIndex));
        return Floors[actIndex];
    }

    public static ForestGenerationOptions GenerationOptions(int actIndex) => new() { NodeCount = Get(actIndex).NodeCount };
}
