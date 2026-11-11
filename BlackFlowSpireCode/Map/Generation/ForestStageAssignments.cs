using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;

namespace BlackFlowSpire.Map.Generation;

public sealed record ForestStageAssignment(string EncounterId, ulong Seed, ForestStageSnapshot Stage);

public static class ForestStageAssignments
{
    public static IReadOnlyDictionary<int, ForestStageAssignment> Build(ForestMap map, string region, string bossEncounterId)
    {
        var pool = ForestStageCatalog.ForRegion(region);
        if (pool.Count == 0) throw new ArgumentException("No stages are configured for this region.", nameof(region));
        var boss = ForestStageCatalog.FindBossByEncounter(bossEncounterId);
        if (boss is not { IsBoss: true }) throw new ArgumentException("The selected boss has no dedicated stage.", nameof(bossEncounterId));
        var random = new StableRandom(map.Seed, map.ActIndex, "stage-assignments/v1");
        var bags = new Dictionary<ForestBattleVariant, Queue<ForestStageDefinition>>();
        var result = new Dictionary<int, ForestStageAssignment>();
        foreach (var node in map.Nodes.Where(node => node.Content is ForestNodeContent.NormalCombat
                     or ForestNodeContent.EliteCombat or ForestNodeContent.Boss).OrderBy(node => node.Id))
        {
            var variant = node.Content switch
            {
                ForestNodeContent.NormalCombat => ForestBattleVariant.Normal,
                ForestNodeContent.EliteCombat => ForestBattleVariant.Emergency,
                ForestNodeContent.Boss => ForestBattleVariant.Boss,
                _ => throw new InvalidDataException("Combat node has non-combat content.")
            };
            ForestStageDefinition definition;
            if (variant == ForestBattleVariant.Boss) definition = boss;
            else
            {
                if (!bags.TryGetValue(variant, out var bag) || bag.Count == 0)
                {
                    var shuffled = pool.ToList();
                    random.Shuffle(shuffled);
                    bags[variant] = bag = new Queue<ForestStageDefinition>(shuffled);
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
