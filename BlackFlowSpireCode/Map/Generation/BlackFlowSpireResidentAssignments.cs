using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Generation;

internal static class BlackFlowSpireResidentAssignments
{
    public static void Populate(BlackFlowSpireMap map)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, "resident-stronghold/v1");
        if (random.Next(100) >= map.Options.ResidentStrongholdChancePercent) return;
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && (node.Kind == BlackFlowSpireNodeKind.Empty || node.Content == BlackFlowSpireNodeContent.Event))
            .OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        var stronghold = candidates[random.Next(candidates.Length)];
        stronghold.Kind = BlackFlowSpireNodeKind.Combat;
        stronghold.Content = BlackFlowSpireNodeContent.BattleSavage;
        BlackFlowSpireResidentRules.Populate(map);
    }
}
