using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Generation;

internal static class ForestVantageAssignments
{
    public static void Populate(ForestMap map)
    {
        if (!ForestFloorPlan.Get(map.ActIndex).AllowVantagePoints) return;
        var random = new StableRandom(map.Seed, map.ActIndex, "vantage-points/v1");
        if (random.Next(100) >= map.Options.VantagePointChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && (node.Kind == ForestNodeKind.Empty || node.Content == ForestNodeContent.Event))
            .OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        var vantage = candidates[random.Next(candidates.Length)];
        vantage.Kind = ForestNodeKind.Event;
        vantage.Content = ForestNodeContent.Light;
        vantage.Revealed = true;
    }
}
