using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static Task ResolveExpedition(int choice) =>
        SubmitTeamAction(BlackFlowSpireTeamAction.Expedition, choice, () => ResolveExpeditionCore(choice));

    internal static async Task ResolveExpeditionCore(int choice)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress || State is not { CurrentRoom: MapRoom } state) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpireExpeditionRules.IsPending(map, progress)) return;
        try
        {
            IsTraveling = true;
            LastError = null;
            if (!BlackFlowSpireGenerationService.TryResolveExpedition(state, map, progress, BlackFlowSpireTeam.Owner(state), choice)) return;
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Expedition choice failed: {error}");
        }
        finally { IsTraveling = false; }

        if (!BlackFlowSpireExpeditionRules.IsPending(map, progress) && NMapScreen.Instance is { } screen)
        {
            BlackFlowSpireExpeditionUi.Dismiss(screen);
            await RefreshMapAfterTravel(state, map, progress, screen, focusCurrent: true);
            ActiveScreenContext.Instance.Update();
        }
    }
}
