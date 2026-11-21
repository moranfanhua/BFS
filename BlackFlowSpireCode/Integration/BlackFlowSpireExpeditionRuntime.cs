using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Networking.ManagedActions;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static Task ResolveExpedition(int choice)
    {
        if (choice == BlackFlowSpireExpeditionRules.Leave)
            return SubmitTeamAction(BlackFlowSpireTeamAction.Expedition, choice, () => ResolveExpeditionCore(choice));
        if (!CanContinueRun || IsTraveling || State is not { CurrentRoom: MapRoom } state || LocalContext.GetMe(state) is not { } player)
            return Task.CompletedTask;
        if (choice != BlackFlowSpireExpeditionRules.Rest && (choice < 0 || choice >= player.Deck.Cards.Count)) return Task.CompletedTask;
        var selection = new BlackFlowSpireExpeditionSelection(choice, choice >= 0
            ? BlackFlowSpireGenerationService.CaptureExpeditionCard(player.Deck.Cards[choice]) : null);
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        if (BlackFlowSpireTeam.IsOnline)
        {
            BlackFlowSpireTeam.SubmitExpeditionPick(map.ActIndex, map.PositionNodeId, selection);
            return Task.CompletedTask;
        }
        return BlackFlowSpireGenerationService.RecordExpeditionChoice(state, player, map.ActIndex, map.PositionNodeId, selection)
            ? ResolveExpeditionCore(BlackFlowSpireExpeditionRules.ConfirmParty) : Task.CompletedTask;
    }

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
            if (!BlackFlowSpireGenerationService.TryResolveExpedition(state, map, progress, choice)) return;
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Expedition choice failed: {error}");
            if (BlackFlowSpireTeam.InCommand(state)) throw;
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

internal static partial class BlackFlowSpireTeam
{
    private sealed record ExpeditionPickMessage(int Act, int Node, BlackFlowSpireExpeditionSelection Selection);

    private static readonly RitsuLibManagedNetActionDescriptor<ExpeditionPickMessage> ExpeditionPick = new(
        Entry.ModId, "BlackFlowSpire-expedition-pick/v1", Encode, Decode<ExpeditionPickMessage>, OnExpeditionPick, GameActionType.NonCombat);

    internal static void SubmitExpeditionPick(int act, int node, BlackFlowSpireExpeditionSelection pick) =>
        RitsuLibManagedNetActions.Request(RunManager.Instance, ExpeditionPick, new ExpeditionPickMessage(act, node, pick));

    private static async Task OnExpeditionPick(RitsuLibManagedNetActionContext<ExpeditionPickMessage> context)
    {
        if (!IsHost || !BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { CurrentRoom: MapRoom } state) return;
        var msg = context.Message;
        if (!BlackFlowSpireGenerationService.RecordExpeditionChoice(state, context.Player, msg.Act, msg.Node, msg.Selection)) return;
        Publish(state);
        await SaveManager.Instance.SaveRun(null);
    }

    internal static void ConfirmExpeditionIfReady(RunState state)
    {
        if (IsHost && BlackFlowSpireGenerationService.ExpeditionReady(state))
            QueueAutomatic(state, BlackFlowSpireTeamAction.Expedition, BlackFlowSpireExpeditionRules.ConfirmParty);
    }
}
