using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Generation;

internal static class ForestResidentAssignments
{
    public static void Populate(ForestMap map)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, "resident-stronghold/v1");
        if (random.Next(100) >= map.Options.ResidentStrongholdChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && (node.Kind == ForestNodeKind.Empty || node.Content == ForestNodeContent.Event))
            .OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        var stronghold = candidates[random.Next(candidates.Length)];
        stronghold.Kind = ForestNodeKind.Combat;
        stronghold.Content = ForestNodeContent.BattleSavage;
        ForestResidentRules.Populate(map);
    }
}
