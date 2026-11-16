using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public enum BlackFlowSpireHuntPhase { None, NoticePending, AwaitingBattle, InBattle, Won }

public static class BlackFlowSpireHuntRules
{
    // Abandon/disconnect closes the map before marking the run abandoned.
    public static bool BlocksMapClose(BlackFlowSpireHuntPhase phase, bool animateOut, bool isTraveling, bool canContinueRun) =>
        (phase is BlackFlowSpireHuntPhase.NoticePending or BlackFlowSpireHuntPhase.AwaitingBattle)
        && animateOut && !isTraveling && canContinueRun;

    public static bool WouldTriggerAfterMove(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireTravelRoute route) =>
        progress.HuntPhase == BlackFlowSpireHuntPhase.None && !progress.PendingNodeId.HasValue
        && route.ActionPointCost > 0
        && progress.ActionPointsRemaining - route.ActionPointCost
            + route.ArrivalActionPointGain
            + BlackFlowSpireVantageRules.ArrivalActionPointGain(map, route.NodeIds[^1]) == 0
        && map.GetNode(route.NodeIds[^1]).Kind is not (BlackFlowSpireNodeKind.Boss or BlackFlowSpireNodeKind.Exit);

    private static bool CanBegin(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory? inventory)
    {
        var current = map.GetNode(map.PositionNodeId);
        return inventory?.IsOverloaded != true && progress.HuntPhase == BlackFlowSpireHuntPhase.None
            && !progress.PendingNodeId.HasValue && current.Completed && current.Kind is not (BlackFlowSpireNodeKind.Boss or BlackFlowSpireNodeKind.Exit);
    }

    public static bool CanExhaust(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory inventory) =>
        progress.ActionPointsRemaining > 0 && CanBegin(map, progress, inventory);

    public static bool TryExhaust(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory inventory)
    {
        if (!CanExhaust(map, progress, inventory)) return false;
        progress.ActionPointsRemaining = 0;
        progress.HuntPhase = BlackFlowSpireHuntPhase.NoticePending;
        return true;
    }

    public static bool TryBegin(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory? inventory = null)
    {
        if (progress.ActionPointsRemaining != 0 || !CanBegin(map, progress, inventory)) return false;
        progress.HuntPhase = BlackFlowSpireHuntPhase.NoticePending;
        return true;
    }

    public static bool ConfirmNotice(BlackFlowSpireExplorationProgress progress)
    {
        if (progress.HuntPhase != BlackFlowSpireHuntPhase.NoticePending) return false;
        progress.HuntPhase = BlackFlowSpireHuntPhase.AwaitingBattle;
        return true;
    }

    public static bool StartBattle(BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory inventory)
    {
        if (progress.HuntPhase != BlackFlowSpireHuntPhase.AwaitingBattle || inventory.IsOverloaded) return false;
        progress.HuntPhase = BlackFlowSpireHuntPhase.InBattle;
        return true;
    }

    public static bool WinBattle(BlackFlowSpireExplorationProgress progress)
    {
        if (progress.HuntPhase != BlackFlowSpireHuntPhase.InBattle) return false;
        progress.HuntPhase = BlackFlowSpireHuntPhase.Won;
        return true;
    }
}
