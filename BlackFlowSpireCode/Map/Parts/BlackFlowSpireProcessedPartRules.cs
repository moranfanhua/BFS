using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Parts;

public static class BlackFlowSpireProcessedPartRules
{
    public static IReadOnlyDictionary<int, BlackFlowSpireTravelRoute> FindRoutes(BlackFlowSpireMap map, BlackFlowSpireEngineInventory inventory) =>
        inventory.LoadedPart is { } part && !inventory.IsOverloaded
            ? FindRoutes(map, part, inventory.Collectibles.Has(203)) : new Dictionary<int, BlackFlowSpireTravelRoute>();

    internal static IReadOnlyDictionary<int, BlackFlowSpireTravelRoute> FindRoutes(BlackFlowSpireMap map, BlackFlowSpireProcessedPart part, bool freeLastUse)
    {
        var definition = BlackFlowSpireProcessedParts.Get(part.Kind);
        // Random destinations are chosen at confirmation, never by hovering a map node.
        return definition.RandomDestination ? new Dictionary<int, BlackFlowSpireTravelRoute>()
            : EligibleTargets(map, definition).ToDictionary(node => node.Id, node => Route(map, part, definition, node.Id, freeLastUse));
    }

    public static int RandomTarget(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory inventory)
    {
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        var definition = BlackFlowSpireProcessedParts.Get(part.Kind);
        if (!definition.RandomDestination) throw new InvalidOperationException("This part does not choose a random destination.");
        var choices = EligibleTargets(map, definition).OrderBy(node => node.Id).ToArray();
        if (choices.Length == 0) throw new InvalidOperationException("No valid random destination exists.");
        // A preview never advances RNG; confirmation and validation choose the same
        // destination, and a reload cannot reroll an already committed movement.
        var random = new StableRandom(map.Seed, map.ActIndex, $"processed-parts/{part.Id}/{part.RemainingUses}/{progress.StepsTaken}");
        return choices[random.Next(choices.Length)].Id;
    }

    public static BlackFlowSpireTravelRoute PlanMove(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireEngineInventory inventory, int targetId)
    {
        if (inventory.IsOverloaded) throw new InvalidOperationException("The parts inventory is overloaded.");
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        var definition = BlackFlowSpireProcessedParts.Get(part.Kind);
        if (!EligibleTargets(map, definition).Any(node => node.Id == targetId)
            || definition.RandomDestination && RandomTarget(map, progress, inventory) != targetId)
            throw new ArgumentException("The destination is not valid for the loaded part.", nameof(targetId));
        if (!BlackFlowSpireResidentRules.CanEnter(map, map.GetNode(targetId)))
            throw new InvalidOperationException("This dedicated special encounter has not been configured yet.");
        var route = Route(map, part, definition, targetId, inventory.Collectibles.Has(203));
        if (progress.ActionPointsRemaining < route.ActionPointCost)
            throw new InvalidOperationException("Not enough exploration action points.");
        return route;
    }

    public static BlackFlowSpireMoveResult Move(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireEngineInventory inventory, BlackFlowSpireTravelRoute route)
    {
        var part = inventory.LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        if (route.ProcessedPartId != part.Id) throw new InvalidOperationException("The loaded part changed.");
        var definition = BlackFlowSpireProcessedParts.Get(part.Kind);
        var target = map.GetNode(route.NodeIds[^1]);
        var source = map.GetNode(map.PositionNodeId);
        var distance = Math.Abs(source.X - target.X) + Math.Abs(source.Y - target.Y);
        // The jump never visits, completes or consumes intervening nodes.
        BlackFlowSpireNodeLifecycle.Leave(map.GetNode(map.PositionNodeId));
        map.PositionNodeId = target.Id;
        progress.StepsTaken++;
        progress.ActionPointsRemaining += definition.ActionPointGain - Cost(part, definition, inventory.Collectibles.Has(203));
        inventory.ConsumeLoadedPart();
        if (part.RemainingUses == 0 && inventory.Collectibles.Has(202))
        {
            var random = new StableRandom(map.Seed, map.ActIndex, $"recovered-part/{part.Id}/{progress.StepsTaken}");
            inventory.Add(BlackFlowSpireProcessedParts.All[random.Next(BlackFlowSpireProcessedParts.All.Count)].Kind);
        }
        if (definition.ConvertsIncidentToShop)
        {
            target.Kind = BlackFlowSpireNodeKind.Event;
            target.Content = BlackFlowSpireNodeContent.Shop;
            target.Details = null;
            target.CombatSetup = null;
            target.Completed = false;
        }
        target.Revealed = true;
        BlackFlowSpireNaturalRules.DistanceMoved(inventory, distance);
        BlackFlowSpireFog.RevealNearby(map, inventory);
        BlackFlowSpireVantageRules.OnArrival(map, progress, target.Id, inventory);
        var requiresRoom = BlackFlowSpireTravelRules.RequiresRoom(target, map);
        var pending = BlackFlowSpireTravelRules.RequiresSettlement(map, target, requiresRoom);
        target.Completed = !pending;
        progress.PendingNodeId = pending ? target.Id : null;
        var conceptEffects = BlackFlowSpireConceptRules.OnProcessedArrival(inventory, target);
        if (conceptEffects.RevealAll) BlackFlowSpireFog.RevealFrom(map, target.Id, map.Nodes.Count, inventory);
        if (!BlackFlowSpirePassageRules.IsPending(map, progress)) BlackFlowSpireResidentRules.AdvanceAfterMove(map);
        return new(target.Id, requiresRoom, progress.ActionPointsRemaining)
        {
            HopeGain = definition.HopeGain, OriginiumGain = definition.OriginiumGain,
            NaturalPaymentIds = BlackFlowSpireNaturalRules.JourneyFinished(map, inventory), ConceptEffects = conceptEffects
        };
    }

    private static int Cost(BlackFlowSpireProcessedPart part, BlackFlowSpireProcessedPartDefinition definition, bool freeLastUse) =>
        freeLastUse && part.RemainingUses == 1 ? 0 : definition.ActionPointCost;

    private static BlackFlowSpireTravelRoute Route(BlackFlowSpireMap map, BlackFlowSpireProcessedPart part,
        BlackFlowSpireProcessedPartDefinition definition, int targetId, bool freeLastUse) =>
        new([map.PositionNodeId, targetId], Cost(part, definition, freeLastUse))
    {
        ProcessedPartId = part.Id, ArrivalActionPointGain = definition.ActionPointGain
    };

    private static IEnumerable<BlackFlowSpireNode> EligibleTargets(BlackFlowSpireMap map, BlackFlowSpireProcessedPartDefinition definition)
    {
        var source = map.GetNode(map.PositionNodeId);
        foreach (var node in map.Nodes)
        {
            if (node.Id == source.Id) continue;
            var dx = Math.Abs(source.X - node.X);
            var dy = Math.Abs(source.Y - node.Y);
            var inRange = definition.Range switch
            {
                BlackFlowSpirePartRange.Straight => (dx == 0 || dy == 0) && dx + dy <= definition.Distance,
                BlackFlowSpirePartRange.AdjacentEight => Math.Max(dx, dy) <= 1,
                // The twelve surrounding cells form a Manhattan-radius-two diamond.
                BlackFlowSpirePartRange.DiamondTwo => dx + dy <= 2,
                BlackFlowSpirePartRange.Anywhere => true,
                BlackFlowSpirePartRange.SameRowOrColumn => dx == 0 || dy == 0,
                _ => false
            };
            if (!inRange || definition.RequiresObservedTarget && BlackFlowSpireNodePresentation.Get(node, map: map).IsMasked) continue;
            var combat = node.Kind is BlackFlowSpireNodeKind.Combat or BlackFlowSpireNodeKind.Boss
                || BlackFlowSpireResidentRules.Occupant(map, node.Id) != null;
            var matches = definition.Destination switch
            {
                BlackFlowSpirePartDestination.Any => true,
                BlackFlowSpirePartDestination.NonCombat => !combat,
                BlackFlowSpirePartDestination.Combat => combat,
                BlackFlowSpirePartDestination.Trader => node.Content == BlackFlowSpireNodeContent.Shop && !combat,
                BlackFlowSpirePartDestination.Exit => node.Id == map.BossNodeId
                    || node.Details?.VisualId is BlackFlowSpireNodeVisuals.FINAL or BlackFlowSpireNodeVisuals.EVACUATE,
                BlackFlowSpirePartDestination.Incident => node.Content == BlackFlowSpireNodeContent.Event && !combat
                    && (node.Details?.VisualId == null || node.Details.VisualId == BlackFlowSpireNodeVisuals.INCIDENT),
                _ => false
            };
            if (matches) yield return node;
        }
    }
}
