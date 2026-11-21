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
    private sealed record OperatorRequest(long Request, BlackFlowSpireTeamCommand Command);
    private sealed record OperatorReply(long Request, bool Accepted);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<OperatorRequest> OperatorRequestMessage = new(
        Entry.ModId, "operator-request/v1", Encode, Decode<OperatorRequest>, OnOperatorRequest, ShouldBuffer: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<OperatorReply> OperatorReplyMessage = new(
        Entry.ModId, "operator-reply/v1", Encode, Decode<OperatorReply>, OnOperatorReply, ShouldBuffer: false);

    internal static bool CanOperate => !IsGuest;

    internal static async Task<bool> Operate(RunState state, BlackFlowSpireTeamAction action, int argument = 0,
        int[]? items = null, long ticket = 0, string? key = null)
    {
        var session = SessionFor(state);
        if (!IsOnline || !Net.IsConnected || action != BlackFlowSpireTeamAction.ClaimOffer && !CanOperate) return false;
        var command = new BlackFlowSpireTeamCommand(action, argument, session.Completed + 1, BlackFlowSpireRoomKey.From(state),
            Net.NetId, items, ticket, key);
        var request = new OperatorRequest(++session.RequestId, command);
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
        if (cmd.Id != session.Completed + 1 || sender != Net.NetId || state.CurrentRoom is not MerchantRoom
            || NMapScreen.Instance is { IsOpen: true } open && !BlackFlowSpireMapUi.IsInspection(open)) return false;
        if (cmd.Action is not (BlackFlowSpireTeamAction.ShopOffer or BlackFlowSpireTeamAction.ShopPart
            or BlackFlowSpireTeamAction.ShopCollectible or BlackFlowSpireTeamAction.ShopRefresh or BlackFlowSpireTeamAction.SellParts
            or BlackFlowSpireTeamAction.Cultivate or BlackFlowSpireTeamAction.Harvest or BlackFlowSpireTeamAction.LeaveNode)) return false;
        if (cmd.Action == BlackFlowSpireTeamAction.LeaveNode && (data.Progress[state.CurrentActIndex].PendingNodeId == null
            || data.ShopTickets.Any(t => RunManager.Instance.RunLobby!.PlayerIds.Contains(t.Player)))) return false;
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
        switch (cmd.Action)
        {
            case BlackFlowSpireTeamAction.SellParts:
                int[] ids = cmd.Items ?? [];
                var held = BlackFlowSpireShopSaleItem.Get(state).Select(p => p.Id).ToHashSet();
                if (ids.Length == 0 || ids.Any(id => !held.Contains(id))) return false;
                await BlackFlowSpirePartService.SellBatch(ids);
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
