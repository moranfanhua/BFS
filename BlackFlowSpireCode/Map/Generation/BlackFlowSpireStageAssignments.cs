using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;

namespace BlackFlowSpire.Map.Generation;

public sealed record BlackFlowSpireStageAssignment(string EncounterId, ulong Seed, BlackFlowSpireStageSnapshot Stage);

public static class BlackFlowSpireStageAssignments
{
    public static IReadOnlyDictionary<int, BlackFlowSpireStageAssignment> Build(BlackFlowSpireMap map, string region, string bossEncounterId)
    {
        var pool = BlackFlowSpireStageCatalog.ForRegion(region);
        if (pool.Count == 0) throw new ArgumentException("No stages are configured for this region.", nameof(region));
        var boss = BlackFlowSpireStageCatalog.FindBossByEncounter(bossEncounterId);
        if (boss is not { IsBoss: true }) throw new ArgumentException("The selected boss has no dedicated stage.", nameof(bossEncounterId));
        var random = new StableRandom(map.Seed, map.ActIndex, "stage-assignments/v1");
        var bags = new Dictionary<BlackFlowSpireBattleVariant, Queue<BlackFlowSpireStageDefinition>>();
        var result = new Dictionary<int, BlackFlowSpireStageAssignment>();
        foreach (var node in map.Nodes.Where(node => node.Content is BlackFlowSpireNodeContent.NormalCombat
                     or BlackFlowSpireNodeContent.EliteCombat or BlackFlowSpireNodeContent.Boss).OrderBy(node => node.Id))
        {
            var variant = node.Content switch
            {
                BlackFlowSpireNodeContent.NormalCombat => BlackFlowSpireBattleVariant.Normal,
                BlackFlowSpireNodeContent.EliteCombat => BlackFlowSpireBattleVariant.Emergency,
                BlackFlowSpireNodeContent.Boss => BlackFlowSpireBattleVariant.Boss,
                _ => throw new InvalidDataException("Combat node has non-combat content.")
            };
            BlackFlowSpireStageDefinition definition;
            if (variant == BlackFlowSpireBattleVariant.Boss) definition = boss;
            else
            {
                if (!bags.TryGetValue(variant, out var bag) || bag.Count == 0)
                {
                    var shuffled = pool.ToList();
                    random.Shuffle(shuffled);
                    bags[variant] = bag = new Queue<BlackFlowSpireStageDefinition>(shuffled);
                }
                definition = bag.Dequeue();
            }
            var seed = ((ulong)(uint)random.Next(int.MaxValue) << 32) | (uint)random.Next(int.MaxValue);
            var snapshot = definition.CreateSnapshot(variant);
            result.Add(node.Id, new(snapshot.SourceEncounterId, seed, snapshot));
        }
        return result;
    }
}
