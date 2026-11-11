using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Presentation;

public static class ForestRoadPresentation
{
    // An edge is passable only if a legal, affordable route can cross it from
    // either end. Two reachable encounter destinations cannot cross each other.
    public static bool IsPassable(ForestMap map, ForestExplorationProgress progress,
        ForestNode from, ForestNode to, IReadOnlyDictionary<int, ForestTravelRoute> routes, bool isTraveling)
    {
        if (isTraveling || progress.PendingNodeId.HasValue || progress.HuntPhase != ForestHuntPhase.None) return false;
        bool CanCrossFrom(ForestNode source, ForestNode destination)
        {
            if (!ForestResidentRules.CanEnter(map, destination)) return false;
            if (source.Id == map.PositionNodeId) return progress.ActionPointsRemaining >= map.Options.MoveCost;
            return ForestTravelRules.CanCross(source, map)
                && routes.TryGetValue(source.Id, out var route)
                && (long)route.ActionPointCost + map.Options.MoveCost <= progress.ActionPointsRemaining;
        }
        return CanCrossFrom(from, to) || CanCrossFrom(to, from);
    }
}
