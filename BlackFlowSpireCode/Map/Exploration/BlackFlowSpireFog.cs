using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public readonly record struct BlackFlowSpireReachableNode(int NodeId, int Steps);

public static class BlackFlowSpireFog
{
    // Standard scouting follows existing roads. Coordinate-area effects use RevealInCoordinateRange.
    public static IReadOnlyList<BlackFlowSpireReachableNode> GetNodesInRange(BlackFlowSpireMap map) =>
        GetNodesInRange(map, map.PositionNodeId, map.Options.RevealRadius);

    public static IReadOnlyList<BlackFlowSpireReachableNode> GetNodesInRange(BlackFlowSpireMap map, int originNodeId, int radius)
    {
        BlackFlowSpireMapValidator.Validate(map);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        if (!nodes.ContainsKey(originNodeId))
            throw new ArgumentException("The reveal origin must be a map node.", nameof(originNodeId));
        var seen = new HashSet<int> { originNodeId };
        var pending = new Queue<BlackFlowSpireReachableNode>();
        var result = new List<BlackFlowSpireReachableNode>();
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
    public static IReadOnlyList<int> RevealNearby(BlackFlowSpireMap map, BlackFlowSpireEngineInventory? inventory = null) =>
        RevealFrom(map, map.PositionNodeId, map.Options.RevealRadius, inventory);

    public static IReadOnlyList<int> RevealFrom(BlackFlowSpireMap map, int originNodeId, int radius, BlackFlowSpireEngineInventory? inventory = null)
    {
        var inRange = GetNodesInRange(map, originNodeId, radius);
        var nodes = map.Nodes.ToDictionary(n => n.Id);
        return RevealNodes(inRange.Select(reachable => nodes[reachable.NodeId]), inventory);
    }

    // Four-way coordinate distance: roads and ungenerated cells do not change this area.
    public static IReadOnlyList<int> RevealInCoordinateRange(BlackFlowSpireMap map, int originNodeId, int radius, BlackFlowSpireEngineInventory? inventory = null)
    {
        BlackFlowSpireMapValidator.Validate(map);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var origin = map.GetNode(originNodeId);
        return RevealNodes(map.Nodes.Where(node => Math.Abs(node.X - origin.X) + Math.Abs(node.Y - origin.Y) <= radius), inventory);
    }

    public static IReadOnlyList<int> RevealTraders(BlackFlowSpireMap map, BlackFlowSpireEngineInventory? inventory = null) =>
        RevealNodes(map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.Shop), inventory);

    public static IReadOnlyList<int> RevealAll(BlackFlowSpireMap map, BlackFlowSpireEngineInventory? inventory = null) =>
        RevealNodes(map.Nodes, inventory);

    private static IReadOnlyList<int> RevealNodes(IEnumerable<BlackFlowSpireNode> nodes, BlackFlowSpireEngineInventory? inventory)
    {
        var newlyRevealed = new List<int>();
        var newInformation = 0;
        foreach (var node in nodes)
        {
            if (node.Revealed) continue;
            node.Revealed = true;
            if (node.Kind != BlackFlowSpireNodeKind.Empty) newInformation++;
            newlyRevealed.Add(node.Id);
        }
        if (inventory != null) BlackFlowSpireNaturalRules.NodesRevealed(inventory, newInformation);
        return newlyRevealed;
    }

    // Position/discovery state only: room resolution and AP payment belong to the future travel flow.
    public static IReadOnlyList<int> MoveToAdjacent(BlackFlowSpireMap map, int targetNodeId, BlackFlowSpireEngineInventory? inventory = null)
    {
        BlackFlowSpireMapValidator.Validate(map);
        var current = map.GetNode(map.PositionNodeId);
        if (targetNodeId != current.Id && !current.Neighbors.Contains(targetNodeId))
            throw new ArgumentException("Target must be the current node or a directly connected neighbor.", nameof(targetNodeId));
        map.PositionNodeId = targetNodeId;
        return RevealNearby(map, inventory);
    }
}
