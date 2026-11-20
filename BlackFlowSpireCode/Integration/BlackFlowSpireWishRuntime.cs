using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static Task ResolveWish(int choice) =>
        SubmitTeamAction(BlackFlowSpireTeamAction.Wish, choice, () => ResolveWishCore(choice));

    internal static async Task ResolveWishCore(int choice)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress || NMapScreen.Instance is not { } screen) return;
        var state = State!;
        if (state.CurrentRoom is not MapRoom) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpireWishRules.IsPending(map, progress)) return;
        var engine = BlackFlowSpireGenerationService.GetEngine(state);

        try
        {
            IsTraveling = true;
            LastError = null;
            if (choice == BlackFlowSpireWishRules.Refresh)
            {
                var price = BlackFlowSpireWishRules.RefreshPrice * BlackFlowSpirePartService.GoldPerOriginium;
                if (BlackFlowSpireTeam.Owner(state) is not { } player || player.Gold < price
                    || !BlackFlowSpireWishRules.CanRefresh(map, progress, engine)) return;
                await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
                BlackFlowSpireTeam.RecordGold(state, -price);
                progress.Wish = new(Refreshed: true);
            }
            else if (choice == BlackFlowSpireWishRules.Leave)
            {
                if (!BlackFlowSpireWishRules.TryLeave(map, progress, engine)) return;
                ((BlackFlowSpireActMap)state.Map).RefreshPointTypes();
            }
            else
            {
                if (BlackFlowSpireWishRules.Claimable(map, progress, engine, choice) is not { } item) return;
                BlackFlowSpireGenerationService.GrantCollectible(state, map, item);
                progress.Wish = new(progress.Wish?.Refreshed == true, item.Id);
            }

            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            await BlackFlowSpireCollectibleService.SettleGold(state, map, replicated: BlackFlowSpireTeam.IsOnline);
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Wish choice failed: {error}");
        }
        finally { IsTraveling = false; }

        if (!BlackFlowSpireWishRules.IsPending(map, progress))
        {
            BlackFlowSpireWishUi.Dismiss(screen);
            await RefreshMapAfterTravel(state, map, progress, screen, focusCurrent: true);
            ActiveScreenContext.Instance.Update();
        }
    }
}
