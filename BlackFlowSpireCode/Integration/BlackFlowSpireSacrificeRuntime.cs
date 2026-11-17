using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static Task ResolveSacrifice(BlackFlowSpireSacrificeAction action) =>
        SubmitTeamAction(BlackFlowSpireTeamAction.Sacrifice, (int)action, () => ResolveSacrificeCore((int)action, exchange: false));

    internal static Task ExchangeSacrificeItem(int item) =>
        SubmitTeamAction(BlackFlowSpireTeamAction.SacrificeItem, item, () => ResolveSacrificeCore(item, exchange: true));

    internal static async Task ResolveSacrificeCore(int argument, bool exchange)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress || NMapScreen.Instance is not { } screen) return;
        var state = State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpireSacrificeRules.IsPending(map, progress)) return;

        try
        {
            IsTraveling = true;
            LastError = null;
            if (!BlackFlowSpireGenerationService.TryResolveSacrifice(state, map, progress, argument, exchange)) return;
            await BlackFlowSpireCollectibleService.SettleGold(state, map, replicated: BlackFlowSpireTeam.IsOnline);
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Sacrifice choice failed: {error}");
        }
        finally { IsTraveling = false; }

        if (!BlackFlowSpireSacrificeRules.IsPending(map, progress))
        {
            BlackFlowSpireSacrificeUi.Dismiss(screen);
            await RefreshMapAfterTravel(state, map, progress, screen, focusCurrent: true);
            ActiveScreenContext.Instance.Update();
        }
    }
}
