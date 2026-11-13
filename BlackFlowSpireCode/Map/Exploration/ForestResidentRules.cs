using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Exploration;

public static class ForestResidentRules
{
    public const int MaximumResidents = 3;
    public const int InitialRadius = 5;

    public static ForestRoamingResident? Occupant(ForestMap map, int nodeId) =>
        map.RoamingResidents.FirstOrDefault(resident => resident.NodeId == nodeId);

    public static bool IsSpecialCombat(ForestMap map, ForestNode node) =>
        node.Content == ForestNodeContent.BattleSavage || Occupant(map, node.Id) != null;

    public static ForestCombatSetup? CombatSetupFor(ForestMap map, ForestNode node) =>
        Occupant(map, node.Id) is { } resident ? resident.CombatSetup : node.CombatSetup;

    // Dedicated encounters require their fixed formation before entry.
    public static bool CanEnter(ForestMap map, ForestNode node) =>
        !IsSpecialCombat(map, node) || CombatSetupFor(map, node) != null;

    public static bool CanOccupy(ForestMap map, ForestNode node) =>
        node.Id != map.PositionNodeId && node.Id != map.BossNodeId
        && node.Kind is not (ForestNodeKind.Boss or ForestNodeKind.Exit) && !node.Repeatable
        && node.Content != ForestNodeContent.BattleSavage && !node.CreatedByCompletion;

    public static void Populate(ForestMap map)
    {
        var remaining = MaximumResidents - map.RoamingResidents.Count;
        if (remaining <= 0) return;
        var occupied = map.RoamingResidents.Select(resident => resident.NodeId).ToHashSet();
        var strongholds = map.Nodes.Where(node => node.Content == ForestNodeContent.BattleSavage && !node.Completed)
            .OrderBy(node => node.Id).ToArray();
        var candidates = map.Nodes.Where(node => node.Kind == ForestNodeKind.Empty && CanOccupy(map, node)
                && !occupied.Contains(node.Id))
            .Select(node => (Node: node, Home: strongholds.FirstOrDefault(home =>
                map.FindShortestPath(home.Id, node.Id).Count - 1 <= InitialRadius)))
            .Where(candidate => candidate.Home != null).OrderBy(candidate => candidate.Node.Id).ToList();
        var random = new StableRandom(map.Seed, map.ActIndex, "resident-spawns/v1");
        random.Shuffle(candidates);
        var nextId = map.RoamingResidents.Count == 0 ? 0 : map.RoamingResidents.Max(resident => resident.Id) + 1;
        foreach (var candidate in candidates.Take(remaining))
            map.RoamingResidents.Add(new()
            {
                Id = nextId++, StrongholdNodeId = candidate.Home!.Id, NodeId = candidate.Node.Id
            });
    }

    // Called once per confirmed journey, not once per road. Passage entry and teleport
    // are one journey: their tick is deferred until the passage choice is resolved.
    public static void AdvanceAfterMove(ForestMap map)
    {
        if (map.RoamingResidents.Count == 0) return;
        map.ResidentMovementCount++;
        var random = new StableRandom(map.Seed, map.ActIndex, $"resident-movement/v1/{map.ResidentMovementCount}");
        var occupied = map.RoamingResidents.Select(resident => resident.NodeId).ToHashSet();
        var destinations = new HashSet<int>();
        foreach (var resident in map.RoamingResidents.OrderBy(resident => resident.Id))
        {
            // An engaged resident stays until the current battle and rewards settle.
            if (resident.NodeId == map.PositionNodeId) continue;
            var choices = map.GetNode(resident.NodeId).Neighbors.Order()
                .Where(id => !occupied.Contains(id) && !destinations.Contains(id) && CanOccupy(map, map.GetNode(id)))
                .ToArray();
            if (choices.Length == 0) continue;
            resident.NodeId = choices[random.Next(choices.Length)];
            destinations.Add(resident.NodeId);
        }
    }

    public static bool CompleteCombat(ForestMap map, ForestNode node)
    {
        if (Occupant(map, node.Id) is { } resident)
        {
            map.RoamingResidents.Remove(resident);
            // Defeating an occupation destroys the node immediately, even when its base was empty.
            ForestNodeLifecycle.MakeEmpty(node, true);
            return true;
        }
        if (node.Content != ForestNodeContent.BattleSavage) return false;
        node.Completed = true;
        // Dispersal lifts overlays; it does not destroy the occupied underlying nodes.
        map.RoamingResidents.Clear();
        return true;
    }
}
