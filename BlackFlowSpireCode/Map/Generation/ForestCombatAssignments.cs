using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Generation;

public sealed record ForestEncounterAssignment(string EncounterId, ulong Seed);

public static class ForestCombatAssignments
{
    public static IReadOnlyDictionary<int, ForestEncounterAssignment> Build(ForestMap map,
        IReadOnlyList<string> normal, IReadOnlyList<string> elite, string boss)
    {
        if (normal.Count == 0 || elite.Count == 0 || string.IsNullOrWhiteSpace(boss))
            throw new ArgumentException("Encounter pools must not be empty.");
        var nodes = map.Nodes.Where(node => node.Content is ForestNodeContent.NormalCombat
            or ForestNodeContent.EliteCombat or ForestNodeContent.Boss).ToList();
        var random = new StableRandom(map.Seed, map.ActIndex, "combat-assignments");
        random.Shuffle(nodes);
        // Native encounter queues put the weak fights first. Assign those close to the center.
        nodes = nodes.OrderBy(node => map.FindShortestPath(map.StartNodeId, node.Id).Count).ToList();
        var normalIndex = 0;
        var eliteIndex = 0;
        var result = new Dictionary<int, ForestEncounterAssignment>();
        foreach (var node in nodes)
        {
            var id = node.Content switch
            {
                ForestNodeContent.NormalCombat => normal[normalIndex++ % normal.Count],
                ForestNodeContent.EliteCombat => elite[eliteIndex++ % elite.Count],
                ForestNodeContent.Boss => boss,
                _ => throw new InvalidDataException("Combat node has non-combat content.")
            };
            var seed = ((ulong)(uint)random.Next(int.MaxValue) << 32) | (uint)random.Next(int.MaxValue);
            result.Add(node.Id, new(id, seed));
        }
        return result;
    }
}
