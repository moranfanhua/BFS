using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Presentation;

public sealed record BlackFlowSpireNodeInfo(BlackFlowSpireNodeView View, string Status, string Description, string RouteText,
    string MovementText, bool CanMove, BlackFlowSpireTravelRoute? Route)
{
    public int RemainingActionPoints { get; init; }
    public bool IsCurrent { get; init; }
    public bool WarnsOfHunt { get; init; }
    public string? MovementName { get; init; }
    public string RulesText { get; init; } = "";
    public string TraitsText { get; init; } = "";
    public bool HasEnemyFormation { get; init; }
    public bool HasEnemyIntel => BlackFlowSpireNodeVisuals.Get(View.IconKey).HasEnemyIntel;
    public bool CanViewEnemyIntel => HasEnemyIntel && !View.IsMasked && HasEnemyFormation;
}

public static class BlackFlowSpireNodeInfoPresentation
{
    public static BlackFlowSpireNodeInfo Get(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, int nodeId,
        bool inspection = false, bool isTraveling = false, string? bossTitle = null, BlackFlowSpireEngineInventory? inventory = null)
    {
        var node = map.GetNode(nodeId);
        var view = BlackFlowSpireNodePresentation.Get(node, bossTitle: bossTitle, map: map);
        var occupied = BlackFlowSpireResidentRules.Occupant(map, node.Id) != null;
        var isCurrent = node.Id == map.PositionNodeId;
        BlackFlowSpireTravelRules.FindRoutes(map, inventory).TryGetValue(nodeId, out var route);
        var status = BlackFlowSpireText.Get("UI.STATUS." + (isCurrent ? "current" : node.Completed && !occupied ? "completed" : "unexplored"));
        if (node.Id == map.StartNodeId) status = BlackFlowSpireText.Get("UI.STATUS.start_format", ("Status", status));
        // All descriptive text uses the visible icon key, never the hidden Content.
        var description = BlackFlowSpireNodePresentation.DescriptionForNode(node, view);
        var rules = view.IsMasked ? "" : view.IconKey switch
        {
            BlackFlowSpireNodeVisuals.DOOR => BlackFlowSpireText.Get("MAP.DOOR.rules"),
            BlackFlowSpireNodeVisuals.LIGHT => BlackFlowSpireText.Get("MAP.LIGHT.rules",
                ("InitialRadius", BlackFlowSpireVantageRules.InitialRadius(map)), ("ArrivalRadius", BlackFlowSpireVantageRules.ArrivalRadius(map))),
            BlackFlowSpireNodeVisuals.BATTLE_SAVAGE => BlackFlowSpireText.Get("MAP.BATTLE_SAVAGE.rules"),
            BlackFlowSpireNodeVisuals.BATTLE_SAVAGE_BUOY => BlackFlowSpireText.Get("MAP.BATTLE_SAVAGE_BUOY.rules"),
            _ => ""
        };
        if (occupied && node.Content == BlackFlowSpireNodeContent.Light)
            rules += "\n" + BlackFlowSpireText.Get("MAP.LIGHT.occupied_effect", ("ArrivalRadius", BlackFlowSpireVantageRules.ArrivalRadius(map)));
        var canEnter = BlackFlowSpireResidentRules.CanEnter(map, node);
        if (!view.IsMasked && !canEnter)
            rules += "\n\n" + BlackFlowSpireText.Get("UI.SPECIAL_COMBAT.unavailable");
        var traits = new List<string>();
        if (!view.IsMasked && view.IconKey != BlackFlowSpireNodeVisuals.EMPTY)
        {
            if (node.Repeatable) traits.Add(BlackFlowSpireText.Get("UI.NODE_TRAIT.repeatable"));
            if (BlackFlowSpireNodeVisuals.Get(view.IconKey).AlwaysVisible) traits.Add(BlackFlowSpireText.Get("UI.NODE_TRAIT.always_visible"));
        }
        var routeText = route == null ? "" : BlackFlowSpireText.Get("UI.ROUTE.summary", ("Steps", route.Steps), ("Cost", route.ActionPointCost));
        var movementText = inspection ? BlackFlowSpireText.Get("UI.MOVE.inspection")
            : isTraveling ? BlackFlowSpireText.Get("UI.MOVE.traveling")
            : progress.PendingNodeId.HasValue ? BlackFlowSpireText.Get("UI.MOVE.pending")
            : isCurrent ? BlackFlowSpireText.Get("UI.MOVE.here")
            : inventory?.IsOverloaded == true ? BlackFlowSpireText.Get("UI.PARTS.overloaded")
            : route == null ? BlackFlowSpireText.Get("UI.MOVE.blocked")
            : progress.ActionPointsRemaining < route.ActionPointCost ? BlackFlowSpireText.Get("UI.MOVE.no_ap")
            : BlackFlowSpireText.Get("UI.MOVE.remaining", ("Amount", progress.ActionPointsRemaining - route.ActionPointCost
                + route.ArrivalActionPointGain
                + BlackFlowSpireVantageRules.ArrivalActionPointGain(map, nodeId)));
        var canMove = !inspection && !isTraveling && !progress.PendingNodeId.HasValue && !isCurrent && canEnter
            && inventory?.IsOverloaded != true
            && progress.HuntPhase == BlackFlowSpireHuntPhase.None
            && route != null && progress.ActionPointsRemaining >= route.ActionPointCost;
        return new(view, status, description, routeText, movementText, canMove, route)
        {
            RemainingActionPoints = progress.ActionPointsRemaining, IsCurrent = isCurrent,
            MovementName = inventory?.LoadedPart is { } part
                ? BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(BlackFlowSpireShopParts.IconId(part.Kind)) + ".name") : null,
            RulesText = rules, TraitsText = string.Join(" · ", traits),
            HasEnemyFormation = BlackFlowSpireResidentRules.CombatSetupFor(map, node) != null,
            WarnsOfHunt = canMove && BlackFlowSpireHuntRules.WouldTriggerAfterMove(map, progress, route!)
        };
    }
}
