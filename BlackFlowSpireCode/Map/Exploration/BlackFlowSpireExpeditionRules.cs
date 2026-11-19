using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public enum BlackFlowSpireExpeditionScene { Intro, Rested, Departed }

public sealed record BlackFlowSpireExpeditionReturn(int SourceActIndex, int SourceNodeId, string CardCategory, string CardEntry,
    bool Upgraded, BlackFlowSpireProcessedPartKind Reward);

internal static class BlackFlowSpireExpeditionRules
{
    public const int Rest = -1;
    public const int Leave = -2;
    public const int RestHope = 2;

    // The original rogue_6 item table grades these seven processed parts NORMAL.
    // Inventory contents do not restrict the draw.
    private static readonly BlackFlowSpireProcessedPartKind[] OrdinaryParts =
    [
        BlackFlowSpireProcessedPartKind.ScrapWheel,
        BlackFlowSpireProcessedPartKind.ScrapProsthetic,
        BlackFlowSpireProcessedPartKind.StandardEngine,
        BlackFlowSpireProcessedPartKind.AirCushionBase,
        BlackFlowSpireProcessedPartKind.PrototypeExoskeleton,
        BlackFlowSpireProcessedPartKind.XiaoBajie,
        BlackFlowSpireProcessedPartKind.Horn
    ];

    public static bool IsChoice(BlackFlowSpireMap map, BlackFlowSpireNode node) =>
        node.Content == BlackFlowSpireNodeContent.Expedition && !node.Completed
        && BlackFlowSpireResidentRules.Occupant(map, node.Id) == null;

    public static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    public static BlackFlowSpireProcessedPartKind DrawReward(BlackFlowSpireMap map, int nodeId)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, $"expedition-reward/v1/{nodeId}");
        return OrdinaryParts[random.Next(OrdinaryParts.Length)];
    }

    public static void Populate(BlackFlowSpireMap map)
    {
        if (map.ActIndex is < 1 or > 3) return;
        var random = new StableRandom(map.Seed, map.ActIndex, "expedition/v1");
        if (random.Next(100) >= map.Options.ExpeditionChancePercent) return;
        var exits = map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Evacuate)
            .Select(node => node.Id).Append(map.BossNodeId).ToArray();
        var candidates = map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Event
            && node.Details == null && exits.All(exit => map.FindShortestPath(node.Id, exit).Count >= 4))
            .OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        candidates[random.Next(candidates.Length)].Content = BlackFlowSpireNodeContent.Expedition;
    }
}
