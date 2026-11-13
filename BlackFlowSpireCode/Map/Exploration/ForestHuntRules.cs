using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public enum ForestHuntPhase { None, NoticePending, AwaitingBattle, InBattle, Won }

public static class ForestHuntRules
{
    // Abandon/disconnect closes the map before marking the run abandoned.
    public static bool BlocksMapClose(ForestHuntPhase phase, bool animateOut, bool isTraveling, bool canContinueRun) =>
        (phase is ForestHuntPhase.NoticePending or ForestHuntPhase.AwaitingBattle)
        && animateOut && !isTraveling && canContinueRun;

    public static bool WouldTriggerAfterMove(ForestMap map, ForestExplorationProgress progress, ForestTravelRoute route) =>
        progress.HuntPhase == ForestHuntPhase.None && !progress.PendingNodeId.HasValue
        && route.ActionPointCost > 0
        && progress.ActionPointsRemaining - route.ActionPointCost
            + route.ArrivalActionPointGain
            + ForestVantageRules.ArrivalActionPointGain(map, route.NodeIds[^1]) == 0
        && map.GetNode(route.NodeIds[^1]).Kind is not (ForestNodeKind.Boss or ForestNodeKind.Exit);

    private static bool CanBegin(ForestMap map, ForestExplorationProgress progress, ForestEngineInventory? inventory)
    {
        var current = map.GetNode(map.PositionNodeId);
        return inventory?.IsOverloaded != true && progress.HuntPhase == ForestHuntPhase.None
            && !progress.PendingNodeId.HasValue && current.Completed && current.Kind is not (ForestNodeKind.Boss or ForestNodeKind.Exit);
    }

    public static bool CanExhaust(ForestMap map, ForestExplorationProgress progress, ForestEngineInventory inventory) =>
        progress.ActionPointsRemaining > 0 && CanBegin(map, progress, inventory);

    public static bool TryExhaust(ForestMap map, ForestExplorationProgress progress, ForestEngineInventory inventory)
    {
        if (!CanExhaust(map, progress, inventory)) return false;
        progress.ActionPointsRemaining = 0;
        progress.HuntPhase = ForestHuntPhase.NoticePending;
        return true;
    }

    public static bool TryBegin(ForestMap map, ForestExplorationProgress progress, ForestEngineInventory? inventory = null)
    {
        if (progress.ActionPointsRemaining != 0 || !CanBegin(map, progress, inventory)) return false;
        progress.HuntPhase = ForestHuntPhase.NoticePending;
        return true;
    }

    public static bool ConfirmNotice(ForestExplorationProgress progress)
    {
        if (progress.HuntPhase != ForestHuntPhase.NoticePending) return false;
        progress.HuntPhase = ForestHuntPhase.AwaitingBattle;
        return true;
    }

    public static bool StartBattle(ForestExplorationProgress progress, ForestEngineInventory inventory)
    {
        if (progress.HuntPhase != ForestHuntPhase.AwaitingBattle || inventory.IsOverloaded) return false;
        progress.HuntPhase = ForestHuntPhase.InBattle;
        return true;
    }

    public static bool WinBattle(ForestExplorationProgress progress)
    {
        if (progress.HuntPhase != ForestHuntPhase.InBattle) return false;
        progress.HuntPhase = ForestHuntPhase.Won;
        return true;
    }
}
