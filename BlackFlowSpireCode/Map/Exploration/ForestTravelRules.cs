using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Exploration;

public sealed class ForestExplorationProgress
{
    public int ActionPointsRemaining { get; set; }
    public int StepsTaken { get; set; }
    public int? PendingNodeId { get; set; }
    public ForestHuntPhase HuntPhase { get; set; }
    public Dictionary<int, int> HistoryIndexes { get; set; } = [];
    public Dictionary<int, ForestShopPartStock> ShopParts { get; set; } = [];
    public Dictionary<int, List<ForestShopOfferStock>> ShopOffers { get; set; } = [];
    public Dictionary<int, ForestSecretShopStock> SecretShops { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestPartMovementReward? PendingPartReward { get; set; }
}

public sealed record ForestPartMovementReward(int ActIndex, int NodeId, int Hope, int Originium)
{
    public IReadOnlyList<int>? NaturalPaymentIds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ForestConceptEffects? ConceptEffects { get; init; }
}

public readonly record struct ForestMoveResult(int NodeId, bool RequiresRoom, int ActionPointsRemaining)
{
    public int HopeGain { get; init; }
    public int OriginiumGain { get; init; }
    public IReadOnlyList<int>? NaturalPaymentIds { get; init; }
    public ForestConceptEffects? ConceptEffects { get; init; }
}

public sealed record ForestTravelRoute(IReadOnlyList<int> NodeIds, int ActionPointCost)
{
    public int Steps => NodeIds.Count - 1;
    public int? ProcessedPartId { get; init; }
    public int ArrivalActionPointGain { get; init; }
}

public static class ForestTravelRules
{
    public static ForestExplorationProgress CreateProgress(ForestMap map) => new()
    {
        ActionPointsRemaining = map.Options.InitialActionPoints
    };

    public static bool RequiresRoom(ForestNode node, ForestMap map) =>
        ForestResidentRules.Occupant(map, node.Id) != null
        || node.Kind != ForestNodeKind.Empty && node.Content is not (ForestNodeContent.Light or ForestNodeContent.Final or ForestNodeContent.Evacuate)
            && (!node.Completed || node.Repeatable);

    public static bool CanCross(ForestNode node, ForestMap map) =>
        ForestResidentRules.Occupant(map, node.Id) == null
        && (node.CanPassThrough || node.Kind == ForestNodeKind.Empty || node.Completed && !node.Repeatable);

    // Repeatable rooms remain destinations unless they explicitly allow walking through.
    // Other completed rooms can be crossed and will be consumed when the route leaves them.
    // BFS finds the shortest legal detour, rather than checking a potentially blocked shortest path.
    public static IReadOnlyDictionary<int, ForestTravelRoute> FindRoutes(ForestMap map, ForestEngineInventory? inventory = null) =>
        FindRoutes(map, inventory?.LoadedPart, inventory?.IsOverloaded == true);

    internal static IReadOnlyDictionary<int, ForestTravelRoute> FindRoutes(ForestMap map, ForestProcessedPart? part, bool isOverloaded)
    {
        if (isOverloaded) return new Dictionary<int, ForestTravelRoute>();
        if (part != null) return ForestProcessedPartRules.FindRoutes(map, part);
        var nodes = map.Nodes.ToDictionary(node => node.Id);
        var paths = new Dictionary<int, ForestTravelRoute>
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

    public static ForestTravelRoute PlanMove(ForestMap map, ForestExplorationProgress progress, int targetId,
        ForestEngineInventory? inventory = null)
    {
        if (progress.HuntPhase != ForestHuntPhase.None)
            throw new InvalidOperationException("Resolve the hunt before exploring further.");
        if (progress.PendingNodeId.HasValue)
            throw new InvalidOperationException("Finish the current encounter before moving.");
        if (inventory?.IsOverloaded == true) throw new InvalidOperationException("The parts inventory is overloaded.");
        if (inventory?.LoadedPart != null) return ForestProcessedPartRules.PlanMove(map, progress, inventory, targetId);
        if (!FindRoutes(map).TryGetValue(targetId, out var route))
            throw new ArgumentException("Target is blocked by an unfinished encounter or is not a different map node.", nameof(targetId));
        if (!ForestResidentRules.CanEnter(map, map.GetNode(targetId)))
            throw new InvalidOperationException("This dedicated special encounter has not been configured yet.");
        if (progress.ActionPointsRemaining < route.ActionPointCost)
            throw new InvalidOperationException("Not enough exploration action points.");
        return route;
    }

    public static ForestMoveResult Move(ForestMap map, ForestExplorationProgress progress, int targetId,
        bool enterDestination = true, ForestEngineInventory? inventory = null)
    {
        // Validate the whole route before changing anything, including AP and discovery.
        var route = PlanMove(map, progress, targetId, inventory);
        if (route.ProcessedPartId.HasValue)
        {
            if (!enterDestination) throw new InvalidOperationException("A part movement cannot stop at an intermediate node.");
            return ForestProcessedPartRules.Move(map, progress, inventory!, route);
        }
        if (!enterDestination && !CanCross(map.GetNode(targetId), map))
            throw new InvalidOperationException("The intermediate node cannot be crossed without entering its room.");
        foreach (var id in route.NodeIds.Skip(1))
        {
            var departed = map.GetNode(map.PositionNodeId);
            ForestFog.MoveToAdjacent(map, id, inventory);
            if (inventory != null) ForestNaturalRules.DistanceMoved(inventory, 1);
            ForestNodeLifecycle.Leave(departed);
            progress.ActionPointsRemaining -= map.Options.MoveCost;
            progress.StepsTaken++;
            var node = map.GetNode(id);
            if (node.Kind == ForestNodeKind.Empty) node.Completed = true;
        }
        var target = map.GetNode(targetId);
        if (enterDestination) ForestVantageRules.OnArrival(map, progress, targetId, inventory);
        var requiresRoom = enterDestination && RequiresRoom(target, map);
        if (requiresRoom || enterDestination && ForestFloorExitRules.IsChoice(map, target))
        {
            // Completion belongs to this visit, including another visit to a repeatable room.
            target.Completed = false;
            progress.PendingNodeId = targetId;
        }
        else if (enterDestination || target.Kind == ForestNodeKind.Empty) target.Completed = true;
        if (enterDestination && !ForestPassageRules.IsPending(map, progress))
            ForestResidentRules.AdvanceAfterMove(map);
        return new(targetId, requiresRoom, progress.ActionPointsRemaining)
        {
            NaturalPaymentIds = enterDestination && inventory != null ? ForestNaturalRules.JourneyFinished(map, inventory) : null
        };
    }

    public static bool CompleteEncounter(ForestMap map, ForestExplorationProgress progress)
    {
        if (!progress.PendingNodeId.HasValue) return false;
        if (progress.PendingNodeId != map.PositionNodeId)
            throw new InvalidOperationException("Pending encounter does not match the current position.");
        var node = map.GetNode(map.PositionNodeId);
        if (!ForestResidentRules.CompleteCombat(map, node)) node.Completed = true;
        progress.PendingNodeId = null;
        return true;
    }
}
