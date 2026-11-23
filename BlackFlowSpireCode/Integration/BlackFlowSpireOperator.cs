using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.Sidecar;

namespace BlackFlowSpire.Integration;

internal static partial class BlackFlowSpireTeam
{
    private sealed record OperatorRequest(long Request, long Turn, BlackFlowSpireTeamCommand Command);
    private sealed record OperatorReply(long Request, bool Accepted);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<OperatorRequest> OperatorRequestMessage = new(
        Entry.ModId, "operator-request/v1", Encode, Decode<OperatorRequest>, OnOperatorRequest, ShouldBuffer: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<OperatorReply> OperatorReplyMessage = new(
        Entry.ModId, "operator-reply/v1", Encode, Decode<OperatorReply>, OnOperatorReply, ShouldBuffer: false);

    internal static bool IsOperator(RunState state) => !IsOnline || BlackFlowSpireGenerationService.TeamData(state).Turn.PlayerId == Net.NetId;
    internal static bool CanOperate => BlackFlowSpireRuntime.State is { } state && IsOperator(state);

    private static void Redraw(RunState state, BlackFlowSpireOperatorTurn turn)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        turn.Draw(map.Seed, map.ActIndex, RunManager.Instance.RunLobby?.PlayerIds ?? state.Players.Select(p => p.NetId));
        BlackFlowSpireGenerationService.SaveTeamChange(state);
    }

    internal static void UpdateTurn(RunState state, BlackFlowSpireExplorationProgress progress)
    {
        if (!IsHost) return;
        var data = BlackFlowSpireGenerationService.TeamData(state);
        var turn = data.Turn;
        if (turn.PlayerId == null || RunManager.Instance.RunLobby is { } lobby && !lobby.PlayerIds.Contains(turn.PlayerId.Value))
            Redraw(state, turn);
        else if (turn.EnteredNode && progress.PendingNodeId == null && data.PendingFloorTransition == null
            && progress.HuntPhase is BlackFlowSpireHuntPhase.None or BlackFlowSpireHuntPhase.Won)
        {
            turn.EnteredNode = false;
            Redraw(state, turn);
        }
    }

    internal static async Task<bool> Operate(RunState state, BlackFlowSpireTeamAction action, int argument = 0,
        int[]? items = null, long ticket = 0, string? key = null)
    {
        var session = SessionFor(state);
        if (!IsOnline || !Net.IsConnected || action != BlackFlowSpireTeamAction.ClaimOffer && !IsOperator(state)) return false;
        var command = new BlackFlowSpireTeamCommand(action, argument, session.Completed + 1, BlackFlowSpireRoomKey.From(state),
            Net.NetId, items, ticket, key);
        var request = new OperatorRequest(++session.RequestId, BlackFlowSpireGenerationService.TeamData(state).Turn.Number, command);
        if (IsHost) return await Accept(state, Net.NetId, request);
        var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Replies.Add(request.Request, reply);
        try { return RitsuLibSidecarSyncMessages.SendToHost(Net, OperatorRequestMessage, request) && await reply.Task; }
        finally { session.Replies.Remove(request.Request); }
    }

    private static async Task OnOperatorRequest(RitsuLibSidecarSyncMessageContext<OperatorRequest> context)
    {
        if (!context.IsHostIngest || !IsHost || !ReferenceEquals(Net, context.NetService)
            || BlackFlowSpireRuntime.State is not { } state) return;
        var accepted = await Accept(state, context.SenderNetId, context.Message);
        if (ReferenceEquals(context.NetService, Net) && Net.IsConnected)
            RitsuLibSidecarSyncMessages.SendToPeer(Net, context.SenderNetId, OperatorReplyMessage, new(context.Message.Request, accepted));
    }

    private static Task OnOperatorReply(RitsuLibSidecarSyncMessageContext<OperatorReply> context)
    {
        if (Net is NetClientGameService { IsConnected: true } client && context.SenderNetId == client.HostNetId
            && ReferenceEquals(Net, context.NetService) && BlackFlowSpireRuntime.State is { } state)
            SessionFor(state).Replies.GetValueOrDefault(context.Message.Request)?.TrySetResult(context.Message.Accepted);
        return Task.CompletedTask;
    }

    private static async Task<bool> Accept(RunState state, ulong sender, OperatorRequest request)
    {
        var cmd = request.Command;
        var session = SessionFor(state);
        if (cmd.Action == BlackFlowSpireTeamAction.ClaimOffer)
        {
            // Concurrent personal claims can arrive against the same command ID.
            // Serialize them on the host and recheck the saved receipt after the preceding transaction.
            while (session.CommandQueued && session.Operation is { } previous) await previous.Task;
        }
        if (!ReferenceEquals(state, BlackFlowSpireRuntime.State) || !BlackFlowSpireRuntime.CanContinueRun || session.CommandQueued || RunManager.Instance.IsChangingRooms
            || CombatManager.Instance.IsInProgress || cmd.Actor != sender
            || cmd.Room != BlackFlowSpireRoomKey.From(state) || RunManager.Instance.RunLobby?.PlayerIds.Contains(sender) != true)
            return false;
        var data = BlackFlowSpireGenerationService.TeamData(state);
        if (cmd.Action == BlackFlowSpireTeamAction.ClaimOffer)
            return data.ShopTickets.Any(t => t.Purchase == cmd.Ticket && t.Player == sender)
                && await Queue(state, cmd with { Id = session.Completed + 1 });
        if (cmd.Id != session.Completed + 1 || !Enum.IsDefined(cmd.Action) || cmd.Action is BlackFlowSpireTeamAction.RestoreRoom or BlackFlowSpireTeamAction.ResumeExit
            || cmd.Action == BlackFlowSpireTeamAction.Expedition && cmd.Argument != BlackFlowSpireExpeditionRules.Leave
            || data.Turn.Number != request.Turn || data.Turn.PlayerId != sender) return false;
        bool shop = cmd.Action is BlackFlowSpireTeamAction.ShopOffer or BlackFlowSpireTeamAction.ShopPart
            or BlackFlowSpireTeamAction.ShopCollectible or BlackFlowSpireTeamAction.ShopRefresh or BlackFlowSpireTeamAction.SellParts
            or BlackFlowSpireTeamAction.Cultivate or BlackFlowSpireTeamAction.Harvest;
        if (cmd.Action == BlackFlowSpireTeamAction.LeaveNode)
        {
            if (data.Progress[state.CurrentActIndex].PendingNodeId == null) return false;
            if (state.CurrentRoom is EventRoom { CanonicalEvent.IsShared: true })
            {
                if (RunManager.Instance.EventSynchronizer.Events.Any(e => !e.IsFinished)) return false;
            }
            else if (state.CurrentRoom is not MerchantRoom || data.ShopTickets.Any(t => RunManager.Instance.RunLobby!.PlayerIds.Contains(t.Player))) return false;
        }
        else if (shop)
        {
            if (state.CurrentRoom is not MerchantRoom || NMapScreen.Instance is { IsOpen: true } open && !BlackFlowSpireMapUi.IsInspection(open)) return false;
        }
        else if (cmd.Action is not (BlackFlowSpireTeamAction.Equip or BlackFlowSpireTeamAction.Discard)
            && (NMapScreen.Instance is not { IsOpen: true } screen || BlackFlowSpireMapUi.IsInspection(screen) || !TeamReady(state)))
            return false;
        if (cmd.Action == BlackFlowSpireTeamAction.Move)
        {
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            if (!map.Nodes.Any(n => n.Id == cmd.Argument)) return false;
            try { BlackFlowSpireTravelRules.PlanMove(map, BlackFlowSpireGenerationService.GetProgress(state), cmd.Argument, data.Engine); }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        return await Queue(state, cmd);
    }

    internal static void ReturnToMap()
    {
        if (NMapScreen.Instance is not { } screen) return;
        if (screen.IsOpen && BlackFlowSpireMapUi.IsInspection(screen)) BlackFlowSpireRuntime.CloseMap(screen, false);
        screen.Open(false);
    }

    private static async Task<bool> ExecuteUtility(RunState state, BlackFlowSpireTeamCommand cmd)
    {
        var player = state.Players.Single(p => p.NetId == cmd.Actor);
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        switch (cmd.Action)
        {
            case BlackFlowSpireTeamAction.Equip:
                if (cmd.Argument >= 0 && !engine.Parts.Any(p => p.Id == cmd.Argument && p.RemainingUses > 0)) return false;
                await BlackFlowSpirePartService.Load(cmd.Argument < 0 ? null : cmd.Argument);
                break;
            case BlackFlowSpireTeamAction.Exhaust:
                await BlackFlowSpirePartService.ExhaustActionPoints(state);
                break;
            case BlackFlowSpireTeamAction.Discard:
            case BlackFlowSpireTeamAction.SellParts:
                int[] ids = cmd.Action == BlackFlowSpireTeamAction.Discard ? [cmd.Argument] : cmd.Items ?? [];
                var held = BlackFlowSpireShopSaleItem.Get(state).Select(p => p.Id).ToHashSet();
                if (ids.Length == 0 || ids.Any(id => !held.Contains(id))) return false;
                if (cmd.Action == BlackFlowSpireTeamAction.Discard)
                {
                    if (engine.NaturalParts.Any(p => p.Id == cmd.Argument)) await BlackFlowSpireNaturalService.Discard(cmd.Argument);
                    else if (engine.ConceptParts.Any(p => p.Id == cmd.Argument)) await BlackFlowSpireConceptService.Discard(cmd.Argument);
                    else await BlackFlowSpirePartService.Discard(cmd.Argument);
                }
                else await BlackFlowSpirePartService.SellBatch(ids);
                break;
            case BlackFlowSpireTeamAction.Cultivate:
                if (!BlackFlowSpireSecretShop.IsCurrent(state) || cmd.Argument <= 0 || cmd.Argument > BlackFlowSpireSecretShop.SeedCount(state)
                    || BlackFlowSpireSecretShop.GetStock(state).PendingHarvest.Count > 0) return false;
                await BlackFlowSpireSecretShop.Cultivate(state, cmd.Argument);
                break;
            case BlackFlowSpireTeamAction.Harvest:
                if (!BlackFlowSpireSecretShop.IsCurrent(state)) return false;
                await BlackFlowSpireSecretShop.AcknowledgeHarvest(state);
                break;
            case BlackFlowSpireTeamAction.LeaveNode:
                ReturnToMap();
                break;
            case BlackFlowSpireTeamAction.ShopPart:
            case BlackFlowSpireTeamAction.ShopCollectible:
            case BlackFlowSpireTeamAction.ShopRefresh:
                var inventory = RunManager.Instance.MerchantSynchronizer.GetInventoryFor(player);
                var stock = BlackFlowSpireShopInventory.Prepare(inventory);
                stock.ReadSharedStock();
                if (cmd.Action == BlackFlowSpireTeamAction.ShopRefresh)
                {
                    if (!stock.CanRefresh || player.Gold < stock.RefreshGold) return false;
                    var price = stock.RefreshGold;
                    await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
                    RecordGold(state, -price, player);
                    stock.RefreshStock(true);
                }
                else
                {
                    MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry? entry = cmd.Action == BlackFlowSpireTeamAction.ShopCollectible
                        ? stock.Collectibles.FirstOrDefault(c => c.Item.Number == cmd.Argument)
                        : stock.Part ?? stock.SecretParts.ElementAtOrDefault(cmd.Argument);
                    if (entry == null || entry is BlackFlowSpireShopPartEntry part && part.Part.Id != cmd.Key
                        || !await entry.OnTryPurchaseUnsynchronized(inventory)) return false;
                }
                if (cmd.Action == BlackFlowSpireTeamAction.ShopRefresh) await BlackFlowSpirePartService.SaveAndRefresh(state);
                break;
            default: return false;
        }
        return true;
    }
}
