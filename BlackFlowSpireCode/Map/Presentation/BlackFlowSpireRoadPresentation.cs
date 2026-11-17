using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Presentation;

public static class BlackFlowSpireRoadPresentation
{
    // An edge is passable only if a legal, affordable route can cross it from
    // either end. Two reachable encounter destinations cannot cross each other.
    public static bool IsPassable(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireNode from, BlackFlowSpireNode to, IReadOnlyDictionary<int, BlackFlowSpireTravelRoute> routes, bool isTraveling)
    {
        if (isTraveling || map.IgnoreMovementRange || progress.PendingNodeId.HasValue
            || progress.HuntPhase != BlackFlowSpireHuntPhase.None) return false;
        bool CanCrossFrom(BlackFlowSpireNode source, BlackFlowSpireNode destination)
        {
            if (!BlackFlowSpireResidentRules.CanEnter(map, destination)) return false;
            if (source.Id == map.PositionNodeId) return progress.ActionPointsRemaining >= map.Options.MoveCost;
            return BlackFlowSpireTravelRules.CanCross(source, map)
                && routes.TryGetValue(source.Id, out var route)
                && (long)route.ActionPointCost + map.Options.MoveCost <= progress.ActionPointsRemaining;
        }
        return CanCrossFrom(from, to) || CanCrossFrom(to, from);
    }
}
