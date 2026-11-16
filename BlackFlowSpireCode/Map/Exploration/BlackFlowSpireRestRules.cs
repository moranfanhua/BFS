using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Exploration;

// Identifiers match choice_ro6_rest_* in the original event data.
public enum BlackFlowSpireRestChoice { MaximumLife = 1, Hope = 3, ActionPoints = 5, PartsCapacity = 6 }

public static class BlackFlowSpireRestRules
{
    private static readonly BlackFlowSpireRestChoice[] Choices = Enum.GetValues<BlackFlowSpireRestChoice>();

    public static bool IsChoice(BlackFlowSpireMap map, BlackFlowSpireNode node) =>
        node.Content == BlackFlowSpireNodeContent.Rest && !node.Completed && BlackFlowSpireResidentRules.Occupant(map, node.Id) == null;

    public static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    public static bool TryLeave(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (!IsPending(map, progress) || !progress.RestChoice.HasValue) return false;
        map.GetNode(map.PositionNodeId).Completed = true;
        progress.PendingNodeId = null;
        progress.RestChoice = null;
        return true;
    }

    public static IReadOnlyList<BlackFlowSpireRestChoice> Options(BlackFlowSpireMap map, int nodeId)
    {
        var choices = Choices.ToList();
        new StableRandom(map.Seed, map.ActIndex, $"rest-choices/v1/{nodeId}").Shuffle(choices);
        return choices.Take(3).Order().ToArray();
    }

    internal static void Populate(BlackFlowSpireMap map)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, "rest/v1");
        if (random.Next(100) >= map.Options.RestChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Event).ToArray();
        if (candidates.Length > 0)
            candidates[random.Next(candidates.Length)].Content = BlackFlowSpireNodeContent.Rest;
    }
}
