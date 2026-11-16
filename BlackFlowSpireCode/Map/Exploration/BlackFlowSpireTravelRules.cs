using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Exploration;

public sealed class BlackFlowSpireExplorationProgress
{
    public int ActionPointsRemaining { get; set; }
    public int StepsTaken { get; set; }
    public int? PendingNodeId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlackFlowSpireRestChoice? RestChoice { get; set; }
    public BlackFlowSpireHuntPhase HuntPhase { get; set; }
    public Dictionary<int, int> HistoryIndexes { get; set; } = [];
    public Dictionary<int, BlackFlowSpireShopPartStock> ShopParts { get; set; } = [];
    public Dictionary<int, List<BlackFlowSpireShopOfferStock>> ShopOffers { get; set; } = [];
    public Dictionary<int, BlackFlowSpireSecretShopStock> SecretShops { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlackFlowSpirePartMovementReward? PendingPartReward { get; set; }
}

public sealed record BlackFlowSpirePartMovementReward(int ActIndex, int NodeId, int Hope, int Originium)
{
    public IReadOnlyList<int>? NaturalPaymentIds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlackFlowSpireConceptEffects? ConceptEffects { get; init; }
}

public readonly record struct BlackFlowSpireMoveResult(int NodeId, bool RequiresRoom, int ActionPointsRemaining)
{
    public int HopeGain { get; init; }
    public int OriginiumGain { get; init; }
    public IReadOnlyList<int>? NaturalPaymentIds { get; init; }
    public BlackFlowSpireConceptEffects? ConceptEffects { get; init; }
}

public sealed record BlackFlowSpireTravelRoute(IReadOnlyList<int> NodeIds, int ActionPointCost)
{
    public int Steps => NodeIds.Count - 1;
    public int? ProcessedPartId { get; init; }
    public int ArrivalActionPointGain { get; init; }
}

public static class BlackFlowSpireTravelRules
{
    public static BlackFlowSpireExplorationProgress CreateProgress(BlackFlowSpireMap map) => new()
    {
        ActionPointsRemaining = map.Options.InitialActionPoints
    };

    public static bool RequiresRoom(BlackFlowSpireNode node, BlackFlowSpireMap map) =>
        BlackFlowSpireResidentRules.Occupant(map, node.Id) != null
        || node.Kind != BlackFlowSpireNodeKind.Empty && node.Content is not (BlackFlowSpireNodeContent.Light
            or BlackFlowSpireNodeContent.Final or BlackFlowSpireNodeContent.Evacuate or BlackFlowSpireNodeContent.Rest)
            && (!node.Completed || node.Repeatable);

    public static bool CanCross(BlackFlowSpireNode node, BlackFlowSpireMap map) =>
        BlackFlowSpireResidentRules.Occupant(map, node.Id) == null
        && (node.CanPassThrough || node.Kind == BlackFlowSpireNodeKind.Empty || node.Completed && !node.Repeatable);

    // Repeatable rooms remain destinations unless they explicitly allow walking through.
    // Other completed rooms can be crossed and will be consumed when the route leaves them.
    // BFS finds the shortest legal detour, rather than checking a potentially blocked shortest path.
    public static IReadOnlyDictionary<int, BlackFlowSpireTravelRoute> FindRoutes(BlackFlowSpireMap map, BlackFlowSpireEngineInventory? inventory = null) =>
        FindRoutes(map, inventory?.LoadedPart, inventory?.IsOverloaded == true, inventory?.Collectibles.Has(203) == true);

    internal static IReadOnlyDictionary<int, BlackFlowSpireTravelRoute> FindRoutes(BlackFlowSpireMap map, BlackFlowSpireProcessedPart? part,
        bool isOverloaded, bool freeLastUse)
    {
        if (isOverloaded) return new Dictionary<int, BlackFlowSpireTravelRoute>();
        if (part != null) return BlackFlowSpireProcessedPartRules.FindRoutes(map, part, freeLastUse);
        var nodes = map.Nodes.ToDictionary(node => node.Id);
        var paths = new Dictionary<int, BlackFlowSpireTravelRoute>
        {
            [map.PositionNodeId] = new(new[] { map.PositionNodeId }, 0)
        };
        var pending = new Queue<int>();
        pending.Enqueue(map.PositionNodeId);
        while (pending.TryDequeue(out var id))
        {
            foreach (var neighborId in nodes[id].Neighbors.Order())
            {
                if (paths.ContainsKey(neighborId)) continue;
                var path = paths[id].NodeIds.Append(neighborId).ToArray();
                paths.Add(neighborId, new(path, (path.Length - 1) * map.Options.MoveCost));
                var neighbor = nodes[neighborId];
                if (CanCross(neighbor, map))
                    pending.Enqueue(neighborId);
            }
        }
        paths.Remove(map.PositionNodeId);
        return paths;
    }

    public static BlackFlowSpireTravelRoute PlanMove(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, int targetId,
        BlackFlowSpireEngineInventory? inventory = null)
    {
        if (progress.HuntPhase != BlackFlowSpireHuntPhase.None)
            throw new InvalidOperationException("Resolve the hunt before exploring further.");
        if (progress.PendingNodeId.HasValue)
            throw new InvalidOperationException("Finish the current encounter before moving.");
        if (inventory?.IsOverloaded == true) throw new InvalidOperationException("The parts inventory is overloaded.");
        if (inventory?.LoadedPart != null) return BlackFlowSpireProcessedPartRules.PlanMove(map, progress, inventory, targetId);
        if (!FindRoutes(map).TryGetValue(targetId, out var route))
            throw new ArgumentException("Target is blocked by an unfinished encounter or is not a different map node.", nameof(targetId));
        if (!BlackFlowSpireResidentRules.CanEnter(map, map.GetNode(targetId)))
            throw new InvalidOperationException("This dedicated special encounter has not been configured yet.");
        if (progress.ActionPointsRemaining < route.ActionPointCost)
            throw new InvalidOperationException("Not enough exploration action points.");
        return route;
    }

    public static BlackFlowSpireMoveResult Move(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, int targetId,
        bool enterDestination = true, BlackFlowSpireEngineInventory? inventory = null)
    {
        // Validate the whole route before changing anything, including AP and discovery.
        var route = PlanMove(map, progress, targetId, inventory);
        if (route.ProcessedPartId.HasValue)
        {
            if (!enterDestination) throw new InvalidOperationException("A part movement cannot stop at an intermediate node.");
            return BlackFlowSpireProcessedPartRules.Move(map, progress, inventory!, route);
        }
        if (!enterDestination && !CanCross(map.GetNode(targetId), map))
            throw new InvalidOperationException("The intermediate node cannot be crossed without entering its room.");
        foreach (var id in route.NodeIds.Skip(1))
        {
            var departed = map.GetNode(map.PositionNodeId);
            BlackFlowSpireFog.MoveToAdjacent(map, id, inventory);
            if (inventory != null) BlackFlowSpireNaturalRules.DistanceMoved(inventory, 1);
            BlackFlowSpireNodeLifecycle.Leave(departed);
            progress.ActionPointsRemaining -= map.Options.MoveCost;
            progress.StepsTaken++;
            var node = map.GetNode(id);
            if (node.Kind == BlackFlowSpireNodeKind.Empty) node.Completed = true;
        }
        var target = map.GetNode(targetId);
        if (enterDestination) BlackFlowSpireVantageRules.OnArrival(map, progress, targetId, inventory);
        var requiresRoom = enterDestination && RequiresRoom(target, map);
        if (enterDestination && RequiresSettlement(map, target, requiresRoom))
        {
            // Completion belongs to this visit, including another visit to a repeatable room.
            target.Completed = false;
            progress.PendingNodeId = targetId;
        }
        else if (enterDestination || target.Kind == BlackFlowSpireNodeKind.Empty) target.Completed = true;
        if (enterDestination && !BlackFlowSpirePassageRules.IsPending(map, progress))
            BlackFlowSpireResidentRules.AdvanceAfterMove(map);
        return new(targetId, requiresRoom, progress.ActionPointsRemaining)
        {
            NaturalPaymentIds = enterDestination && inventory != null ? BlackFlowSpireNaturalRules.JourneyFinished(map, inventory) : null
        };
    }

    internal static bool RequiresSettlement(BlackFlowSpireMap map, BlackFlowSpireNode node, bool requiresRoom) =>
        requiresRoom || BlackFlowSpireFloorExitRules.IsChoice(map, node) || BlackFlowSpireRestRules.IsChoice(map, node);

    public static bool CompleteEncounter(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (!progress.PendingNodeId.HasValue) return false;
        if (progress.PendingNodeId != map.PositionNodeId)
            throw new InvalidOperationException("Pending encounter does not match the current position.");
        var node = map.GetNode(map.PositionNodeId);
        if (!BlackFlowSpireResidentRules.CompleteCombat(map, node)) node.Completed = true;
        progress.PendingNodeId = null;
        return true;
    }
}
