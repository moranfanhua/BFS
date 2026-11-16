using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public static class BlackFlowSpireVantageRules
{
    public static bool HasEnhancedRange(BlackFlowSpireMap map) =>
        map.Options.GameOfLifeSplitUnlocked && map.Options.SecrecyLevel >= 3;

    public static int InitialRadius(BlackFlowSpireMap map) => HasEnhancedRange(map) ? 12 : 4;
    public static int ArrivalRadius(BlackFlowSpireMap map) => HasEnhancedRange(map) ? 24 : 12;

    public static IReadOnlyList<int> RevealOnAppearance(BlackFlowSpireMap map, BlackFlowSpireNode node) =>
        node.Content == BlackFlowSpireNodeContent.Light
            ? BlackFlowSpireFog.RevealInCoordinateRange(map, node.Id, InitialRadius(map)) : [];

    // Read the underlying node, including when an occupation changes its displayed information.
    public static int ArrivalActionPointGain(BlackFlowSpireMap map, int targetId)
    {
        var node = map.GetNode(targetId);
        return node.Content == BlackFlowSpireNodeContent.Light && !node.VantageActivated ? 1 : 0;
    }

    public static bool OnArrival(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, int targetId,
        BlackFlowSpireEngineInventory? inventory = null)
    {
        var gain = ArrivalActionPointGain(map, targetId);
        if (gain == 0) return false;
        if (map.PositionNodeId != targetId)
            throw new InvalidOperationException("A vantage point can only activate after actual arrival.");
        var node = map.GetNode(targetId);
        BlackFlowSpireFog.RevealInCoordinateRange(map, node.Id, ArrivalRadius(map), inventory);
        node.VantageActivated = true;
        progress.ActionPointsRemaining += gain;
        return true;
    }
}
