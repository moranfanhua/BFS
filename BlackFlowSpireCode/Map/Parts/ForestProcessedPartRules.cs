using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Parts;

public static class ForestProcessedPartRules
{
    public static IReadOnlyDictionary<int, ForestTravelRoute> FindRoutes(ForestMap map, ForestEngineInventory inventory) =>
        inventory.LoadedPart is { } part && !inventory.IsOverloaded
            ? FindRoutes(map, part, inventory.Collectibles.Has(203)) : new Dictionary<int, ForestTravelRoute>();

    internal static IReadOnlyDictionary<int, ForestTravelRoute> FindRoutes(ForestMap map, ForestProcessedPart part, bool freeLastUse)
    {
        var definition = ForestProcessedParts.Get(part.Kind);
        // Random destinations are chosen at confirmation, never by hovering a map node.
        return definition.RandomDestination ? new Dictionary<int, ForestTravelRoute>()
            : EligibleTargets(map, definition).ToDictionary(node => node.Id, node => Route(map, part, definition, node.Id, freeLastUse));
    }

    public static int RandomTarget(ForestMap map, ForestExplorationProgress progress, ForestEngineInventory inventory)
    {
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        var definition = ForestProcessedParts.Get(part.Kind);
        if (!definition.RandomDestination) throw new InvalidOperationException("This part does not choose a random destination.");
        var choices = EligibleTargets(map, definition).OrderBy(node => node.Id).ToArray();
        if (choices.Length == 0) throw new InvalidOperationException("No valid random destination exists.");
        // A preview never advances RNG; confirmation and validation choose the same
        // destination, and a reload cannot reroll an already committed movement.
        var random = new StableRandom(map.Seed, map.ActIndex, $"processed-parts/{part.Id}/{part.RemainingUses}/{progress.StepsTaken}");
        return choices[random.Next(choices.Length)].Id;
    }

    public static ForestTravelRoute PlanMove(ForestMap map, ForestExplorationProgress progress,
        ForestEngineInventory inventory, int targetId)
    {
        if (inventory.IsOverloaded) throw new InvalidOperationException("The parts inventory is overloaded.");
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        var definition = ForestProcessedParts.Get(part.Kind);
        if (!EligibleTargets(map, definition).Any(node => node.Id == targetId)
            || definition.RandomDestination && RandomTarget(map, progress, inventory) != targetId)
            throw new ArgumentException("The destination is not valid for the loaded part.", nameof(targetId));
        if (!ForestResidentRules.CanEnter(map, map.GetNode(targetId)))
            throw new InvalidOperationException("This dedicated special encounter has not been configured yet.");
        var route = Route(map, part, definition, targetId, inventory.Collectibles.Has(203));
        if (progress.ActionPointsRemaining < route.ActionPointCost)
            throw new InvalidOperationException("Not enough exploration action points.");
        return route;
    }

    public static ForestMoveResult Move(ForestMap map, ForestExplorationProgress progress,
        ForestEngineInventory inventory, ForestTravelRoute route)
    {
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        if (route.ProcessedPartId != part.Id) throw new InvalidOperationException("The loaded part changed.");
        var definition = ForestProcessedParts.Get(part.Kind);
        var target = map.GetNode(route.NodeIds[^1]);
        var source = map.GetNode(map.PositionNodeId);
        var distance = Math.Abs(source.X - target.X) + Math.Abs(source.Y - target.Y);
        // The jump never visits, completes or consumes intervening nodes.
        ForestNodeLifecycle.Leave(map.GetNode(map.PositionNodeId));
        map.CurrentNodeId = target.Id;
        progress.StepsTaken++;
        progress.ActionPointsRemaining += definition.ActionPointGain - Cost(part, definition, inventory.Collectibles.Has(203));
        inventory.ConsumeLoadedPart();
        if (part.RemainingUses == 0 && inventory.Collectibles.Has(202))
        {
            var random = new StableRandom(map.Seed, map.ActIndex, $"recovered-part/{part.Id}/{progress.StepsTaken}");
            inventory.Add(ForestProcessedParts.All[random.Next(ForestProcessedParts.All.Count)].Kind);
        }
        if (definition.ConvertsIncidentToShop)
        {
            target.Kind = ForestNodeKind.Event;
            target.Content = ForestNodeContent.Shop;
            target.Details = null;
            target.CombatSetup = null;
            target.Completed = false;
        }
        target.Revealed = true;
        ForestNaturalRules.DistanceMoved(inventory, distance);
        ForestFog.RevealNearby(map, inventory);
        ForestVantageRules.OnArrival(map, progress, target.Id, inventory);
        var requiresRoom = ForestTravelRules.RequiresRoom(target, map);
        var pending = requiresRoom || ForestFloorExitRules.IsChoice(map, target);
        target.Completed = !pending;
        progress.PendingNodeId = pending ? target.Id : null;
        var conceptEffects = ForestConceptRules.OnProcessedArrival(inventory, target);
        if (conceptEffects.RevealAll) ForestFog.RevealFrom(map, target.Id, map.Nodes.Count, inventory);
        if (!ForestPassageRules.IsPending(map, progress)) ForestResidentRules.AdvanceAfterMove(map);
        return new(target.Id, requiresRoom, progress.ActionPointsRemaining)
        {
            HopeGain = definition.HopeGain, OriginiumGain = definition.OriginiumGain,
            NaturalPaymentIds = ForestNaturalRules.JourneyFinished(map, inventory), ConceptEffects = conceptEffects
        };
    }

    private static int Cost(ForestProcessedPart part, ForestProcessedPartDefinition definition, bool freeLastUse) =>
        freeLastUse && part.RemainingUses == 1 ? 0 : definition.ActionPointCost;

    private static ForestTravelRoute Route(ForestMap map, ForestProcessedPart part,
        ForestProcessedPartDefinition definition, int targetId, bool freeLastUse) =>
        new([map.PositionNodeId, targetId], Cost(part, definition, freeLastUse))
    {
        ProcessedPartId = part.Id, ArrivalActionPointGain = definition.ActionPointGain
    };

    private static IEnumerable<ForestNode> EligibleTargets(ForestMap map, ForestProcessedPartDefinition definition)
    {
        var source = map.GetNode(map.PositionNodeId);
        foreach (var node in map.Nodes)
        {
            if (node.Id == source.Id) continue;
            var dx = Math.Abs(source.X - node.X);
            var dy = Math.Abs(source.Y - node.Y);
            var inRange = definition.Range switch
            {
                ForestPartRange.Straight => (dx == 0 || dy == 0) && dx + dy <= definition.Distance,
                ForestPartRange.AdjacentEight => Math.Max(dx, dy) <= 1,
                // The twelve surrounding cells form a Manhattan-radius-two diamond.
                ForestPartRange.DiamondTwo => dx + dy <= 2,
                ForestPartRange.Anywhere => true,
                ForestPartRange.SameRowOrColumn => dx == 0 || dy == 0,
                _ => false
            };
            if (!inRange || definition.RequiresObservedTarget && ForestNodePresentation.Get(node, map: map).IsMasked) continue;
            var combat = node.Kind is ForestNodeKind.Combat or ForestNodeKind.Boss
                || ForestResidentRules.Occupant(map, node.Id) != null;
            var matches = definition.Destination switch
            {
                ForestPartDestination.Any => true,
                ForestPartDestination.NonCombat => !combat,
                ForestPartDestination.Combat => combat,
                ForestPartDestination.Trader => node.Content == ForestNodeContent.Shop && !combat,
                ForestPartDestination.Exit => node.Id == map.BossNodeId
                    || node.Details?.VisualId is ForestNodeVisuals.FINAL or ForestNodeVisuals.EVACUATE,
                ForestPartDestination.Incident => node.Content == ForestNodeContent.Event && !combat
                    && (node.Details?.VisualId == null || node.Details.VisualId == ForestNodeVisuals.INCIDENT),
                _ => false
            };
            if (matches) yield return node;
        }
    }
}
