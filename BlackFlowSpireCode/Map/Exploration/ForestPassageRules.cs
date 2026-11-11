using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public static class ForestPassageRules
{
    public static bool IsPending(ForestMap map, ForestExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId
        && map.GetNode(map.PositionNodeId).Content == ForestNodeContent.Door;

    public static ForestMoveResult Resolve(ForestMap map, ForestExplorationProgress progress, bool usePassage,
        ForestEngineInventory? inventory = null)
    {
        ForestMapValidator.Validate(map);
        if (progress.HuntPhase != ForestHuntPhase.None || !IsPending(map, progress))
            throw new InvalidOperationException("No passage choice is pending at the current location.");
        var source = map.GetNode(map.PositionNodeId);
        var destination = map.GetNode(source.PairedNodeId!.Value);
        source.Completed = true;
        progress.PendingNodeId = null;
        if (usePassage)
        {
            map.CurrentNodeId = destination.Id;
            // Record a positional transition for native floor history, without paying AP.
            progress.StepsTaken++;
            destination.Completed = true;
            ForestFog.RevealNearby(map, inventory);
            if (inventory != null) ForestNaturalRules.NodeEntered(map, inventory, destination);
        }
        ForestResidentRules.AdvanceAfterMove(map);
        // Arrival never prompts again: this resolves the pair once, without a teleport loop.
        return new(map.PositionNodeId, false, progress.ActionPointsRemaining);
    }
}
