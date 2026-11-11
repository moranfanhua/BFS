using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Presentation;

public sealed record ForestNodeInfo(ForestNodeView View, string Status, string Description, string RouteText,
    string MovementText, bool CanMove, ForestTravelRoute? Route)
{
    public int RemainingActionPoints { get; init; }
    public bool IsCurrent { get; init; }
    public bool WarnsOfHunt { get; init; }
    public string? MovementName { get; init; }
    public string RulesText { get; init; } = "";
    public string TraitsText { get; init; } = "";
    public bool HasEnemyFormation { get; init; }
    public bool HasEnemyIntel => ForestNodeVisuals.Get(View.IconKey).HasEnemyIntel;
    public bool CanViewEnemyIntel => HasEnemyIntel && !View.IsMasked && HasEnemyFormation;
}

public static class ForestNodeInfoPresentation
{
    public static ForestNodeInfo Get(ForestMap map, ForestExplorationProgress progress, int nodeId,
        bool inspection = false, bool isTraveling = false, string? bossTitle = null, ForestEngineInventory? inventory = null)
    {
        var node = map.GetNode(nodeId);
        var view = ForestNodePresentation.Get(node, bossTitle: bossTitle, map: map);
        var occupied = ForestResidentRules.Occupant(map, node.Id) != null;
        var isCurrent = node.Id == map.PositionNodeId;
        ForestTravelRules.FindRoutes(map, inventory).TryGetValue(nodeId, out var route);
        var status = ForestText.Get("UI.STATUS." + (isCurrent ? "current" : node.Completed && !occupied ? "completed" : "unexplored"));
        if (node.Id == map.StartNodeId) status = ForestText.Get("UI.STATUS.start_format", ("Status", status));
        // All descriptive text uses the visible icon key, never the hidden Content.
        var description = ForestNodePresentation.DescriptionForNode(node, view);
        var rules = view.IsMasked ? "" : view.IconKey switch
        {
            ForestNodeVisuals.DOOR => ForestText.Get("MAP.DOOR.rules"),
            ForestNodeVisuals.LIGHT => ForestText.Get("MAP.LIGHT.rules",
                ("InitialRadius", ForestVantageRules.InitialRadius(map)), ("ArrivalRadius", ForestVantageRules.ArrivalRadius(map))),
            ForestNodeVisuals.BATTLE_SAVAGE => ForestText.Get("MAP.BATTLE_SAVAGE.rules"),
            ForestNodeVisuals.BATTLE_SAVAGE_BUOY => ForestText.Get("MAP.BATTLE_SAVAGE_BUOY.rules"),
            _ => ""
        };
        if (occupied && node.Content == ForestNodeContent.Light)
            rules += "\n" + ForestText.Get("MAP.LIGHT.occupied_effect", ("ArrivalRadius", ForestVantageRules.ArrivalRadius(map)));
        var canEnter = ForestResidentRules.CanEnter(map, node);
        if (!view.IsMasked && !canEnter)
            rules += "\n\n" + ForestText.Get("UI.SPECIAL_COMBAT.unavailable");
        var traits = new List<string>();
        if (!view.IsMasked && view.IconKey != ForestNodeVisuals.EMPTY)
        {
            if (node.Repeatable) traits.Add(ForestText.Get("UI.NODE_TRAIT.repeatable"));
            if (ForestNodeVisuals.Get(view.IconKey).AlwaysVisible) traits.Add(ForestText.Get("UI.NODE_TRAIT.always_visible"));
        }
        var routeText = route == null ? "" : ForestText.Get("UI.ROUTE.summary", ("Steps", route.Steps), ("Cost", route.ActionPointCost));
        var movementText = inspection ? ForestText.Get("UI.MOVE.inspection")
            : isTraveling ? ForestText.Get("UI.MOVE.traveling")
            : progress.PendingNodeId.HasValue ? ForestText.Get("UI.MOVE.pending")
            : isCurrent ? ForestText.Get("UI.MOVE.here")
            : inventory?.IsOverloaded == true ? ForestText.Get("UI.PARTS.overloaded")
            : route == null ? ForestText.Get("UI.MOVE.blocked")
            : progress.ActionPointsRemaining < route.ActionPointCost ? ForestText.Get("UI.MOVE.no_ap")
            : ForestText.Get("UI.MOVE.remaining", ("Amount", progress.ActionPointsRemaining - route.ActionPointCost
                + route.ArrivalActionPointGain
                + ForestVantageRules.ArrivalActionPointGain(map, nodeId)));
        var canMove = !inspection && !isTraveling && !progress.PendingNodeId.HasValue && !isCurrent && canEnter
            && inventory?.IsOverloaded != true
            && progress.HuntPhase == ForestHuntPhase.None
            && route != null && progress.ActionPointsRemaining >= route.ActionPointCost;
        return new(view, status, description, routeText, movementText, canMove, route)
        {
            RemainingActionPoints = progress.ActionPointsRemaining, IsCurrent = isCurrent,
            MovementName = inventory?.LoadedPart is { } part
                ? ForestText.Get(ForestText.PartKey(ForestShopParts.IconId(part.Kind)) + ".name") : null,
            RulesText = rules, TraitsText = string.Join(" · ", traits),
            HasEnemyFormation = ForestResidentRules.CombatSetupFor(map, node) != null,
            WarnsOfHunt = canMove && ForestHuntRules.WouldTriggerAfterMove(map, progress, route!)
        };
    }
}
