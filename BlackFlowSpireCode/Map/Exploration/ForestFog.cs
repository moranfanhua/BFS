using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public readonly record struct ForestReachableNode(int NodeId, int Steps);

public static class ForestFog
{
    // Standard scouting follows existing roads. Coordinate-area effects use RevealInCoordinateRange.
    public static IReadOnlyList<ForestReachableNode> GetNodesInRange(ForestMap map) =>
        GetNodesInRange(map, map.PositionNodeId, map.Options.RevealRadius);

    public static IReadOnlyList<ForestReachableNode> GetNodesInRange(ForestMap map, int originNodeId, int radius)
    {
        ForestMapValidator.Validate(map);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        if (!nodes.ContainsKey(originNodeId))
            throw new ArgumentException("The reveal origin must be a map node.", nameof(originNodeId));
        var seen = new HashSet<int> { originNodeId };
        var pending = new Queue<ForestReachableNode>();
        var result = new List<ForestReachableNode>();
        pending.Enqueue(new(originNodeId, 0));
        while (pending.TryDequeue(out var current))
        {
            result.Add(current);
            if (current.Steps >= radius) continue;
            foreach (var id in nodes[current.NodeId].Neighbors)
                if (seen.Add(id)) pending.Enqueue(new(id, current.Steps + 1));
        }
        return result;
    }

    // Reveal is cumulative. Returned IDs let the future map UI refresh only changed nodes.
    public static IReadOnlyList<int> RevealNearby(ForestMap map, ForestEngineInventory? inventory = null) =>
        RevealFrom(map, map.PositionNodeId, map.Options.RevealRadius, inventory);

    public static IReadOnlyList<int> RevealFrom(ForestMap map, int originNodeId, int radius, ForestEngineInventory? inventory = null)
    {
        var inRange = GetNodesInRange(map, originNodeId, radius);
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        return RevealNodes(inRange.Select(reachable => nodes[reachable.NodeId]), inventory);
    }

    // Four-way coordinate distance: roads and ungenerated cells do not change this area.
    public static IReadOnlyList<int> RevealInCoordinateRange(ForestMap map, int originNodeId, int radius, ForestEngineInventory? inventory = null)
    {
        ForestMapValidator.Validate(map);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var origin = map.GetNode(originNodeId);
        return RevealNodes(map.Nodes.Where(node => Math.Abs(node.X - origin.X) + Math.Abs(node.Y - origin.Y) <= radius), inventory);
    }

    public static IReadOnlyList<int> RevealTraders(ForestMap map, ForestEngineInventory? inventory = null) =>
        RevealNodes(map.Nodes.Where(node => node.Content == ForestNodeContent.Shop), inventory);

    public static IReadOnlyList<int> RevealAll(ForestMap map, ForestEngineInventory? inventory = null) =>
        RevealNodes(map.Nodes, inventory);

    private static IReadOnlyList<int> RevealNodes(IEnumerable<ForestNode> nodes, ForestEngineInventory? inventory)
    {
        var newlyRevealed = new List<int>();
        var newInformation = 0;
        foreach (var node in nodes)
        {
            if (node.Revealed) continue;
            node.Revealed = true;
            if (node.Kind != ForestNodeKind.Empty) newInformation++;
            newlyRevealed.Add(node.Id);
        }
        if (inventory != null) ForestNaturalRules.NodesRevealed(inventory, newInformation);
        return newlyRevealed;
    }

    // Position/discovery state only: room resolution and AP payment belong to the future travel flow.
    public static IReadOnlyList<int> MoveToAdjacent(ForestMap map, int targetNodeId, ForestEngineInventory? inventory = null)
    {
        ForestMapValidator.Validate(map);
        var current = map.GetNode(map.PositionNodeId);
        if (targetNodeId != current.Id && !current.Neighbors.Contains(targetNodeId))
            throw new ArgumentException("Target must be the current node or a directly connected neighbor.", nameof(targetNodeId));
        map.CurrentNodeId = targetNodeId;
        return RevealNearby(map, inventory);
    }
}
