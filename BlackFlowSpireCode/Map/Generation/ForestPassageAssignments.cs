using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Generation;

internal static class ForestPassageAssignments
{
    public static void Populate(ForestMap map)
    {
        if (!ForestFloorPlan.Get(map.ActIndex).AllowPassages) return;
        var random = new StableRandom(map.Seed, map.ActIndex, "passage-pairs/v1");
        if (random.Next(100) >= map.Options.DoorPairChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && (node.Kind == ForestNodeKind.Empty || node.Content == ForestNodeContent.Event))
            .OrderBy(node => node.Id).ToList();
        if (candidates.Count < 2) return;
        random.Shuffle(candidates);
        var first = candidates[0];
        candidates.RemoveAt(0);
        // A distant partner makes a passage useful; shuffled ties stay deterministic.
        var second = candidates.MaxBy(node => map.FindShortestPath(first.Id, node.Id).Count)!;
        SetPassage(first, second.Id);
        SetPassage(second, first.Id);
    }

    private static void SetPassage(ForestNode node, int pairedId)
    {
        node.Kind = ForestNodeKind.Event;
        node.Content = ForestNodeContent.Door;
        node.Repeatable = true;
        node.CanPassThrough = true;
        node.PairedNodeId = pairedId;
        node.Revealed = true;
    }
}
