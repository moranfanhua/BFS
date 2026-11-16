using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public static class BlackFlowSpirePassageRules
{
    public static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId
        && map.GetNode(map.PositionNodeId).Content == BlackFlowSpireNodeContent.Door;

    public static BlackFlowSpireMoveResult Resolve(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, bool usePassage,
        BlackFlowSpireEngineInventory? inventory = null)
    {
        BlackFlowSpireMapValidator.Validate(map);
        if (progress.HuntPhase != BlackFlowSpireHuntPhase.None || !IsPending(map, progress))
            throw new InvalidOperationException("No passage choice is pending at the current location.");
        var source = map.GetNode(map.PositionNodeId);
        var destination = map.GetNode(source.PairedNodeId!.Value);
        source.Completed = true;
        progress.PendingNodeId = null;
        if (usePassage)
        {
            map.PositionNodeId = destination.Id;
            // Record a positional transition for native floor history, without paying AP.
            progress.StepsTaken++;
            destination.Completed = true;
            BlackFlowSpireFog.RevealNearby(map, inventory);
            if (inventory != null) BlackFlowSpireNaturalRules.NodeEntered(map, inventory, destination);
        }
        BlackFlowSpireResidentRules.AdvanceAfterMove(map);
        // Arrival never prompts again: this resolves the pair once, without a teleport loop.
        return new(map.PositionNodeId, false, progress.ActionPointsRemaining);
    }
}
