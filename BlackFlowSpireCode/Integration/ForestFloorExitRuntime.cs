using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class ForestRuntime
{
    public static Task ResolveFloorExit(bool accept) => SubmitTeamAction(ForestTeamAction.FloorExit,
        accept ? 1 : 0, () => ResolveFloorExitCore(accept));

    internal static async Task ResolveFloorExitCore(bool accept)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress) return;
        var state = State!;
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        if (!ForestFloorExitRules.IsPending(map, progress)) return;
        var node = map.GetNode(map.PositionNodeId);
        var originalActionPoints = progress.ActionPointsRemaining;
        var accepted = false;
        try
        {
            IsTraveling = true;
            LastError = null;
            if (accept)
            {
                var final = node.Content == ForestNodeContent.Final;
                var transition = new ForestFloorTransition
                {
                    SourceActIndex = state.CurrentActIndex, NodeId = node.Id,
                    ActionPointBonus = final ? 0 : progress.ActionPointsRemaining,
                    Hope = final ? progress.ActionPointsRemaining : 0,
                    Reward = ForestFloorExitRules.Reward(map, node)
                };
                // Freeze the reward before authorizing the transition. It is
                // granted once on arrival, after old temporary parts expire.
                ForestGenerationService.SetFloorTransition(state, transition);
                if (final) progress.ActionPointsRemaining = 0;
                accepted = true;
            }
            node.Completed = true;
            progress.PendingNodeId = null;
            ForestGenerationService.MarkChanged(state, map, progress);
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            if (accept)
            {
                ForestGenerationService.SetFloorTransition(state, null);
                progress.ActionPointsRemaining = originalActionPoints;
                progress.PendingNodeId = node.Id;
                node.Completed = false;
                ForestGenerationService.MarkChanged(state, map, progress);
            }
            LastError = error.Message;
            Entry.Logger.Error($"[Forest] Floor exit choice failed: {error}");
            accepted = false;
        }
        finally { IsTraveling = false; }
        if (accepted) await AdvanceThroughExit();
        else if (NMapScreen.Instance is { } screen)
            await RefreshMapAfterTravel(state, map, progress, screen);
    }

    public static Task ConfirmHunt() => SubmitTeamAction(ForestTeamAction.Hunt, 0, ConfirmHuntCore);

    internal static async Task ConfirmHuntCore()
    {
        if (!CanContinueRun || State is not { } state || NMapScreen.Instance is not { } screen) return;
        var progress = ForestGenerationService.GetProgress(state);
        if (!ForestHuntRules.ConfirmNotice(progress)) return;
        ForestGenerationService.MarkChanged(state, ForestGenerationService.GetOrGenerate(state), progress);
        ForestHuntUi.DismissNotice(screen);
        ForestMapUi.Show(screen, state, false);
        ActiveScreenContext.Instance.Update();
        await SaveManager.Instance.SaveRun(null);
    }
}
