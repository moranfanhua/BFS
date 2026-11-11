using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public static class ForestVantageRules
{
    public static bool HasEnhancedRange(ForestMap map) =>
        map.Options.GameOfLifeSplitUnlocked && map.Options.SecrecyLevel >= 3;

    public static int InitialRadius(ForestMap map) => HasEnhancedRange(map) ? 12 : 4;
    public static int ArrivalRadius(ForestMap map) => HasEnhancedRange(map) ? 24 : 12;

    public static IReadOnlyList<int> RevealOnAppearance(ForestMap map, ForestNode node) =>
        node.Content == ForestNodeContent.Light
            ? ForestFog.RevealInCoordinateRange(map, node.Id, InitialRadius(map)) : [];

    // Read the underlying node, including when an occupation changes its displayed information.
    public static int ArrivalActionPointGain(ForestMap map, int targetId)
    {
        var node = map.GetNode(targetId);
        return node.Content == ForestNodeContent.Light && !node.VantageActivated ? 1 : 0;
    }

    public static bool OnArrival(ForestMap map, ForestExplorationProgress progress, int targetId,
        ForestEngineInventory? inventory = null)
    {
        var gain = ArrivalActionPointGain(map, targetId);
        if (gain == 0) return false;
        if (map.PositionNodeId != targetId)
            throw new InvalidOperationException("A vantage point can only activate after actual arrival.");
        var node = map.GetNode(targetId);
        ForestFog.RevealInCoordinateRange(map, node.Id, ArrivalRadius(map), inventory);
        node.VantageActivated = true;
        progress.ActionPointsRemaining += gain;
        return true;
    }
}
