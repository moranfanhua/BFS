using System.Runtime.CompilerServices;
using System.Text.Json;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Networking.Sidecar;
using STS2RitsuLib.RunData;

namespace BlackFlowSpire.Integration;

internal enum BlackFlowSpireTeamAction { Move, Passage, FloorExit, Hunt, ShopOffer, HuntBattle, Rest, Sacrifice, SacrificeItem, Wish, Expedition, ResumeExit, RestoreRoom, ShopPart, ShopCollectible, ShopRefresh, SellParts, Equip, Discard, Exhaust, Cultivate, Harvest, LeaveNode, ClaimOffer }

internal sealed record BlackFlowSpireRoomKey(int Act, int? Column, int? Row, int Visits, int History, RoomType? Room, int? RoomId)
{
    internal static BlackFlowSpireRoomKey From(RunState state) => new(state.CurrentActIndex, state.CurrentMapCoord?.col,
        state.CurrentMapCoord?.row, state.VisitedMapCoords.Count, state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex)?.Count ?? 0,
        state.CurrentRoom?.RoomType, state.CurrentRoom?.Id);
}
internal sealed record BlackFlowSpireTeamReady(BlackFlowSpireRoomKey Room, long Completed, int Node, int Steps, bool Ready);
internal sealed record BlackFlowSpireHopePayment(ulong PlayerId, uint ChoiceId, bool Accepted);
internal sealed record BlackFlowSpireTeamSnapshot(BlackFlowSpireRoomKey Room, long Revision, long Completed, BlackFlowSpireRunData Forest);
internal sealed record BlackFlowSpireTeamCommand(BlackFlowSpireTeamAction Action, int Argument, long Id, BlackFlowSpireRoomKey Room, ulong Actor = 0, int[]? Items = null, long Ticket = 0, string? Key = null);

internal static partial class BlackFlowSpireTeam
{
    private sealed class Session
    {
        public Dictionary<ulong, BlackFlowSpireTeamReady> Ready { get; } = [];
        public long Completed;
        public long Active;
        public int Sequence;
        public bool NativePurchase;
        public required BlackFlowSpireFrameInbox<BlackFlowSpireTeamFrame> Frames { get; init; }
        public readonly List<BlackFlowSpireGoldChange> Gold = [];
        public BlackFlowSpireExpeditionDispatch? Dispatched;
        public BlackFlowSpireExpeditionDispatch? Returned;
        public long Revision { get; set; }
        public bool CommandQueued { get; set; }
        public bool RosterChanged;
        public bool AttachedToLobby { get; set; }
        public BlackFlowSpireTeamSnapshot? DeferredSnapshot { get; set; }
        public ForestEventMessage? EventView { get; set; }
        public bool EventViewSent { get; set; }
        public TaskCompletionSource<bool>? Operation { get; set; }
        public Dictionary<long, TaskCompletionSource<bool>> Replies { get; } = [];
        public long RequestId;
        public bool ExecutingUtility;
        public Dictionary<(ulong PlayerId, uint ChoiceId), TaskCompletionSource<bool>> HopePayments { get; } = [];
    }

    private static readonly ConditionalWeakTable<RunState, Session> Sessions = new();
    private static readonly ConditionalWeakTable<INetGameService, BlackFlowSpireFrameInbox<BlackFlowSpireTeamFrame>> Connections = new();
    private static Session SessionFor(RunState state) => Sessions.GetValue(state, static run =>
        new Session { Completed = BlackFlowSpireGenerationService.GetCommandId(run), Frames = IsGuest ? Connections.GetOrCreateValue(Net) : new() });
    private static readonly RitsuLibManagedNetActionDescriptor<BlackFlowSpireTeamCommand> Command = new(
        Entry.ModId, "BlackFlowSpire-team-command/v3", Encode, Decode<BlackFlowSpireTeamCommand>, Execute, GameActionType.NonCombat);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireTeamReady> ReadyMessage = new(
        Entry.ModId, "BlackFlowSpire-team-ready/v2", Encode, Decode<BlackFlowSpireTeamReady>, OnReady, false, false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireTeamSnapshot> SnapshotMessage = new(
        Entry.ModId, "BlackFlowSpire-team-state/v3", Encode, Decode<BlackFlowSpireTeamSnapshot>, OnSnapshot,
        DispatchLocalOnBroadcast: false, ShouldBuffer: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireHopePayment> HopePaymentMessage = new(
        Entry.ModId, "BlackFlowSpire-hope-payment/v1", Encode, Decode<BlackFlowSpireHopePayment>, OnHopePayment,
        LocationTargeted: true, DispatchLocalOnBroadcast: false, ShouldBuffer: false);

    internal static void Register()
    {
        RitsuLibManagedNetActions.Register(Command);
        RitsuLibManagedNetActions.Register(ExpeditionPick);
        RitsuLibSidecarSyncMessages.Register(ReadyMessage);
        RitsuLibSidecarSyncMessages.Register(SnapshotMessage);
        RitsuLibSidecarSyncMessages.Register(FrameMessage);
        RitsuLibSidecarSyncMessages.Register(EventMessage);
        RitsuLibSidecarSyncMessages.Register(OperatorRequestMessage);
        RitsuLibSidecarSyncMessages.Register(OperatorReplyMessage);
        RitsuLibSidecarSyncMessages.Register(HopePaymentMessage);
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(e =>
        {
            if (e.IsMultiplayer && BlackFlowSpireGenerationService.IsEnabled(e.RunState))
                BlackFlowSpireCampaignService.ConfigureNewRun(e.RunState);
        }, replayCurrentState: false);
    }

    private static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);
    private static T Decode<T>(ReadOnlySpan<byte> data) where T : class =>
        JsonSerializer.Deserialize<T>(data) ?? throw new InvalidDataException("Missing forest team message.");

    private static INetGameService Net => RunManager.Instance.NetService;
    internal static bool IsGuest => Net?.Type == NetGameType.Client;
    internal static bool IsHost => Net?.Type == NetGameType.Host;
    internal static bool IsOnline => IsGuest || IsHost;
    internal static bool CanManage(RunState state)
    {
        var session = SessionFor(state);
        if (IsGuest) return session.ExecutingUtility && session.NativePurchase;
        return !session.CommandQueued || session.ExecutingUtility;
    }

    internal static void AttachLobby(RunState state)
    {
        if (!IsOnline || !BlackFlowSpireGenerationService.IsEnabled(state) || RunManager.Instance.RunLobby is not { } lobby) return;
        var session = SessionFor(state);
        if (session.AttachedToLobby) return;
        session.AttachedToLobby = true;
        lobby.RemotePlayerDisconnected += id => MembershipChanged(id, disconnected: true);
        lobby.PlayerRejoined += peer => MembershipChanged(peer.id, disconnected: false);

        void MembershipChanged(ulong id, bool disconnected)
        {
            if (!ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun) return;
            session.Ready.Remove(id);
            var turn = BlackFlowSpireGenerationService.TeamData(state).Turn;
            if (IsHost && disconnected && turn.PlayerId == id) Redraw(state, turn);
            if (session.CommandQueued) { session.RosterChanged = true; return; }
            RefreshViews(state);
            if (IsHost)
            {
                Publish(state);
                if (!RunManager.Instance.IsChangingRooms) TaskHelper.RunSafely(SaveManager.Instance.SaveRun(null));
            }
        }
    }

    internal static void EndSession(RunState state)
    {
        if (!Sessions.TryGetValue(state, out var session)) return;
        session.Operation?.TrySetResult(false);
        foreach (var reply in session.Replies.Values) reply.TrySetResult(false);
        session.Frames.Cancel();
        if (IsGuest) Connections.Remove(Net);
        foreach (var payment in session.HopePayments.Values) payment.TrySetResult(false);
        Sessions.Remove(state);
    }

    internal static async Task<bool> TryPayHope(RunState state, Player player, uint choiceId, int cost)
    {
        if (cost == 0) return true;
        if (!IsOnline) return BlackFlowSpireGenerationService.TryChangeHope(state, player.NetId, -cost);
        if (IsHost)
        {
            var accepted = BlackFlowSpireGenerationService.TryChangeHope(state, player.NetId, -cost, replicated: true);
            if (!RitsuLibSidecarSyncMessages.Broadcast(Net, HopePaymentMessage, new BlackFlowSpireHopePayment(player.NetId, choiceId, accepted)))
            {
                if (accepted) BlackFlowSpireGenerationService.RefundHope(state, player.NetId, cost);
                Entry.Logger.Warn("[BlackFlowSpire] Could not synchronize the card pack's Hope payment.");
                return false;
            }
            if (accepted) Publish(state);
            return accepted;
        }

        var payments = SessionFor(state).HopePayments;
        var key = (player.NetId, choiceId);
        var payment = GetHopePayment(payments, key);
        try { return await payment.Task; }
        finally { payments.Remove(key); }
    }

    private static Task OnHopePayment(RitsuLibSidecarSyncMessageContext<BlackFlowSpireHopePayment> context)
    {
        if (Net is NetClientGameService { IsConnected: true } client && context.SenderNetId == client.HostNetId
            && ReferenceEquals(context.NetService, Net) && BlackFlowSpireRuntime.State is { } state)
        {
            var payment = context.Message;
            var payments = SessionFor(state).HopePayments;
            GetHopePayment(payments, (payment.PlayerId, payment.ChoiceId)).TrySetResult(payment.Accepted);
        }
        return Task.CompletedTask;
    }

    private static TaskCompletionSource<bool> GetHopePayment(
        Dictionary<(ulong PlayerId, uint ChoiceId), TaskCompletionSource<bool>> payments, (ulong PlayerId, uint ChoiceId) key)
    {
        if (!payments.TryGetValue(key, out var payment))
            payments.Add(key, payment = new(TaskCreationOptions.RunContinuationsAsynchronously));
        return payment;
    }

    internal static Player? Owner(RunState state)
    {
        ulong? owner = Net switch
        {
            INetHostGameService host => host.NetId,
            NetClientGameService { IsConnected: true } client => client.HostNetId,
            _ => BlackFlowSpireGenerationService.GetTeamOwnerId(state)
        };
        return owner is { } id ? state.Players.FirstOrDefault(player => player.NetId == id) : LocalContext.GetMe(state);
    }

    internal static bool CanTravel(RunState state)
    {
        if (!IsOnline) return CanManage(state);
        return IsOperator(state) && !SessionFor(state).CommandQueued && (!IsHost || TeamReady(state));
    }

    private static bool TeamReady(RunState state)
    {
        var session = SessionFor(state);
        var expected = ReadyAt(state, session, true);
        return RunManager.Instance.RunLobby?.PlayerIds.All(id => id == Net.NetId || session.Ready.GetValueOrDefault(id) == expected) == true;
    }

    private static BlackFlowSpireTeamReady ReadyAt(RunState state, Session session, bool ready) =>
        new(BlackFlowSpireRoomKey.From(state), session.Completed, BlackFlowSpireGenerationService.GetOrGenerate(state).PositionNodeId,
            BlackFlowSpireGenerationService.GetProgress(state).StepsTaken, ready);

    internal static void ReportReady(RunState state, bool ready)
    {
        if (!IsGuest || !Net.IsConnected) return;
        var session = SessionFor(state);
        if (session.CommandQueued) return;
        if (ready) Flush(state);
        RitsuLibSidecarSyncMessages.SendToHost(Net, ReadyMessage, ReadyAt(state, session, ready));
    }

    private static Task OnReady(RitsuLibSidecarSyncMessageContext<BlackFlowSpireTeamReady> context)
    {
        if (!context.IsHostIngest || !IsHost || !ReferenceEquals(context.NetService, Net)
            || !BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } state
            || RunManager.Instance.RunLobby?.PlayerIds.Contains(context.SenderNetId) != true) return Task.CompletedTask;
        var session = SessionFor(state);
        var first = !session.Ready.TryGetValue(context.SenderNetId, out var prior);
        session.Ready[context.SenderNetId] = context.Message;
        if (prior?.Ready != true && session.EventView is { } view && context.Message.Ready)
            RitsuLibSidecarSyncMessages.SendToPeer(Net, context.SenderNetId, EventMessage, view);
        if (context.Message.Ready && !session.CommandQueued && !RunManager.Instance.IsChangingRooms
            && context.Message.Completed == session.Completed && context.Message.Room == BlackFlowSpireRoomKey.From(state)
            && (first || context.Message != ReadyAt(state, session, true)))
            RitsuLibSidecarSyncMessages.SendToPeer(Net, context.SenderNetId, SnapshotMessage, Capture(state));
        return Task.CompletedTask;
    }

    internal static bool Request(RunState state, BlackFlowSpireTeamAction action, int argument)
    {
        if (!CanTravel(state)) return false;
        TaskHelper.RunSafely(Submit());
        return true;

        async Task Submit()
        {
            if (!await Operate(state, action, argument) && ReferenceEquals(state, BlackFlowSpireRuntime.State)
                && !RunManager.Instance.IsCleaningUp && NMapScreen.Instance is { IsOpen: true })
                RefreshViews(state);
        }
    }

    internal static void ResumeExitIfReady(RunState state)
    {
        if (IsHost && BlackFlowSpireGenerationService.GetFloorTransition(state)?.SourceActIndex == state.CurrentActIndex)
            QueueAutomatic(state, BlackFlowSpireTeamAction.ResumeExit, 0);
    }

    internal static void QueueAutomatic(RunState state, BlackFlowSpireTeamAction action, int argument)
    {
        var session = SessionFor(state);
        if (!IsHost || session.CommandQueued || !TeamReady(state)) return;
        _ = Queue(state, new(action, argument, session.Completed + 1, BlackFlowSpireRoomKey.From(state), Net.NetId));
    }

    private static async Task<bool> Queue(RunState state, BlackFlowSpireTeamCommand command)
    {
        var session = SessionFor(state);
        if (session.CommandQueued) return false;
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Operation = done;
        session.CommandQueued = true;
        try
        {
            return RitsuLibManagedNetActions.Request(RunManager.Instance, Command, command) && await done.Task;
        }
        finally
        {
            if (ReferenceEquals(session.Operation, done))
            {
                session.Operation = null;
                session.CommandQueued = false;
                done.TrySetResult(false);
            }
        }
    }

    private static Task Execute(RitsuLibManagedNetActionContext<BlackFlowSpireTeamCommand> context)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } state
            || Owner(state)?.NetId != context.Player.NetId) return Task.CompletedTask;
        return RunCommand(state, context.Message);
    }

    internal static Task RestorePendingRoom(RunState state, Func<Task> enter) => RunCommand(state,
        new(BlackFlowSpireTeamAction.RestoreRoom, 0, SessionFor(state).Completed + 1, BlackFlowSpireRoomKey.From(state)), enter);

    private static async Task RunCommand(RunState state, BlackFlowSpireTeamCommand command, Func<Task>? restore = null)
    {
        var net = Net;
        var session = SessionFor(state);
        if (command.Id <= session.Completed) return;
        var success = false;
        var originalTurn = BlackFlowSpireGenerationService.TeamData(state).Turn.Number;
        session.CommandQueued = true;
        try
        {
            session.Active = command.Id;
            session.Sequence = 0;
            session.NativePurchase = command.Action is BlackFlowSpireTeamAction.ShopOffer or BlackFlowSpireTeamAction.ClaimOffer
                or BlackFlowSpireTeamAction.ShopPart or BlackFlowSpireTeamAction.ShopCollectible;
            if (command.Id != session.Completed + 1 || command.Room != BlackFlowSpireRoomKey.From(state))
                throw new InvalidOperationException("Forest command reached a different native room.");
            BlackFlowSpireGenerationService.SetCommandId(state, command.Id);
            if (restore != null)
            {
                await restore();
                if (IsHost) await BlackFlowSpirePartService.SettleMovementRewards(state,
                    BlackFlowSpireGenerationService.GetOrGenerate(state), BlackFlowSpireGenerationService.GetProgress(state));
            }
            else if (IsGuest)
            {
                await ReceiveExpected(state, ForestBoundary.Begin);
                if (!session.NativePurchase)
                {
                    await BlackFlowSpireRuntime.FollowHost(state);
                    if (command.Action == BlackFlowSpireTeamAction.LeaveNode && state.CurrentRoom is MerchantRoom or EventRoom)
                        ReturnToMap();
                    return;
                }
            }
            else Emit(state, ForestBoundary.Begin);
            success = true;
            switch (command.Action)
            {
                case BlackFlowSpireTeamAction.RestoreRoom when restore != null: break;
                case BlackFlowSpireTeamAction.Move: await BlackFlowSpireRuntime.MoveCore(command.Argument); break;
                case BlackFlowSpireTeamAction.Passage: await BlackFlowSpireRuntime.ResolvePassageCore(command.Argument != 0); break;
                case BlackFlowSpireTeamAction.FloorExit: await BlackFlowSpireRuntime.ResolveFloorExitCore(command.Argument != 0); break;
                case BlackFlowSpireTeamAction.ResumeExit: await BlackFlowSpireRuntime.AdvanceThroughExit(); break;
                case BlackFlowSpireTeamAction.Hunt: await BlackFlowSpireRuntime.ConfirmHuntCore(); break;
                case BlackFlowSpireTeamAction.HuntBattle: await BlackFlowSpireRuntime.StartHuntCore(); break;
                case BlackFlowSpireTeamAction.Rest: await BlackFlowSpireRuntime.ResolveRestCore(command.Argument); break;
                case BlackFlowSpireTeamAction.Sacrifice: await BlackFlowSpireRuntime.ResolveSacrificeCore(command.Argument, exchange: false); break;
                case BlackFlowSpireTeamAction.SacrificeItem: await BlackFlowSpireRuntime.ResolveSacrificeCore(command.Argument, exchange: true); break;
                case BlackFlowSpireTeamAction.Wish: await BlackFlowSpireRuntime.ResolveWishCore(command.Argument); break;
                case BlackFlowSpireTeamAction.Expedition: await BlackFlowSpireRuntime.ResolveExpeditionCore(command.Argument); break;
                case BlackFlowSpireTeamAction.ShopOffer:
                    success = await PurchaseOffer(state, state.Players.Single(p => p.NetId == command.Actor), (BlackFlowSpireShopOfferKind)command.Argument) != null;
                    break;
                case BlackFlowSpireTeamAction.ClaimOffer:
                    success = await ClaimTicket(state, command);
                    break;
                default:
                    session.ExecutingUtility = true;
                    success = await ExecuteUtility(state, command);
                    break;
            }
            if (IsGuest) await ReceiveExpected(state, ForestBoundary.Finish);
            else
            {
                Emit(state, ForestBoundary.Finish);
                if (originalTurn != BlackFlowSpireGenerationService.TeamData(state).Turn.Number || session.RosterChanged
                    || success && session.NativePurchase)
                    await SaveManager.Instance.SaveRun(null);
            }

        }
        catch (Exception error)
        {
            success = false;
            Entry.Logger.Error($"[BlackFlowSpire] Team command {command.Id} ({command.Action}) failed: {error}");
            if (RunManager.Instance.IsCleaningUp || !ReferenceEquals(BlackFlowSpireRuntime.State, state) || !ReferenceEquals(net, Net)) return;
            if (IsGuest && Net.IsConnected) Net.Disconnect(NetError.StateDivergence);
            else if (Net is INetHostGameService host && RunManager.Instance.RunLobby is { } lobby)
                foreach (var id in lobby.PlayerIds.Where(id => id != host.NetId).ToArray())
                    host.DisconnectClient(id, NetError.StateDivergence);
            throw;
        }
        finally
        {
            session.Completed = command.Id;
            session.Frames.Complete(command.Id);
            session.Active = 0;
            session.Gold.Clear();
            session.Dispatched = session.Returned = null;
            session.CommandQueued = false;
            session.ExecutingUtility = false;
            session.Operation?.TrySetResult(success);
            session.Operation = null;
            if (ReferenceEquals(BlackFlowSpireRuntime.State, state) && ReferenceEquals(net, Net) && !RunManager.Instance.IsCleaningUp)
            {
                Flush(state);
                if (session.RosterChanged)
                {
                    session.RosterChanged = false;
                    RefreshViews(state);
                    Publish(state);
                }
                else if (command.Action is BlackFlowSpireTeamAction.ShopOffer or BlackFlowSpireTeamAction.ClaimOffer)
                    RefreshViews(state);
                else if (state.CurrentRoom is MerchantRoom && NMerchantRoom.Instance is { } shop)
                    BlackFlowSpireShopUi.Find(shop)?.RefreshSharedStock();
                if (IsGuest && NMapScreen.Instance is { IsOpen: true } map && !BlackFlowSpireMapUi.IsInspection(map)) ReportReady(state, true);
            }
        }
    }

    private static async Task<BlackFlowSpireShopOfferEntry?> PurchaseOffer(RunState state, Player player, BlackFlowSpireShopOfferKind kind)
    {
        if (state.CurrentRoom is not MerchantRoom) return null;
        var inventory = RunManager.Instance.MerchantSynchronizer.GetInventoryFor(player);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!progress.ShopOffers.TryGetValue(BlackFlowSpireGenerationService.GetOrGenerate(state).PositionNodeId, out var offers)) return null;
        var stock = offers.FirstOrDefault(item => item.Kind == kind && !item.Purchased);
        if (stock == null) return null;
        var offer = new BlackFlowSpireShopOfferEntry(player, stock, replicated: true);
        return await offer.OnTryPurchaseUnsynchronized(inventory) ? offer : null;
    }

    private static BlackFlowSpireTeamSnapshot Capture(RunState state)
    {
        var session = SessionFor(state);
        return new(BlackFlowSpireRoomKey.From(state), ++session.Revision, session.Completed, BlackFlowSpireGenerationService.CaptureFloor(state));
    }

    internal static void Publish(RunState state)
    {
        if (!IsHost || !Net.IsConnected || !BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)) return;
        if (SessionFor(state).CommandQueued || RunManager.Instance.IsChangingRooms) return;
        if (!RitsuLibSidecarSyncMessages.Broadcast(Net, SnapshotMessage, Capture(state)))
            Entry.Logger.Warn("[BlackFlowSpire] Could not publish shared forest state; next team command carries the current state.");
    }

    private static Task OnSnapshot(RitsuLibSidecarSyncMessageContext<BlackFlowSpireTeamSnapshot> context)
    {
        if (Net is not NetClientGameService { IsConnected: true } client || context.SenderNetId != client.HostNetId
            || !ReferenceEquals(context.NetService, Net) || !BlackFlowSpireRuntime.CanContinueRun
            || BlackFlowSpireRuntime.State is not { } state) return Task.CompletedTask;
        var session = SessionFor(state);
        if (context.Message.Revision > Math.Max(session.Revision, session.DeferredSnapshot?.Revision ?? 0))
            session.DeferredSnapshot = context.Message;
        Flush(state);
        return Task.CompletedTask;
    }

    internal static void Flush(RunState state)
    {
        if (!IsGuest || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress || !ReferenceEquals(BlackFlowSpireRuntime.State, state)) return;
        var session = SessionFor(state);
        if (session.CommandQueued) return;
        if (session.DeferredSnapshot is not { } snapshot) return;
        if (snapshot.Revision <= session.Revision)
        {
            session.DeferredSnapshot = null;
            return;
        }
        if (RunManager.Instance.IsChangingRooms || snapshot.Room != BlackFlowSpireRoomKey.From(state)
            || snapshot.Completed != session.Completed) return;
        session.DeferredSnapshot = null;
        BlackFlowSpireGenerationService.ApplyFloor(state, snapshot.Forest);
        session.Revision = snapshot.Revision;
        RefreshViews(state);
    }

    internal static void RefreshViews(RunState state)
    {
        BlackFlowSpirePartsUi.RefreshSharedBox();
        BlackFlowSpireCollectibleScreen.RefreshShared();
        if (NMerchantRoom.Instance is { } room) BlackFlowSpireShopUi.Find(room)?.RefreshSharedStock();
        if (NMapScreen.Instance is { IsOpen: true } screen)
            BlackFlowSpireMapUi.Show(screen, state, BlackFlowSpireMapUi.IsInspection(screen));
    }
}
