using System.Runtime.CompilerServices;
using System.Text.Json;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
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

internal enum BlackFlowSpireTeamAction { Move, Passage, FloorExit, Hunt, ShopOffer, HuntBattle, Rest }

// Steps distinguish revisits to the same node. Act distinguishes floor changes.
internal sealed record BlackFlowSpireTeamPosition(int Act, int Node, int Steps);
internal sealed record BlackFlowSpireTeamReady(BlackFlowSpireTeamPosition Position, bool Ready);
internal sealed record BlackFlowSpireHopePayment(ulong PlayerId, uint ChoiceId, bool Accepted);
internal sealed record BlackFlowSpireTeamSnapshot(BlackFlowSpireTeamPosition Position, long Revision, BlackFlowSpireEngineInventory Engine,
    int ActionPoints, BlackFlowSpireHuntPhase Hunt, Dictionary<int, BlackFlowSpireShopPartStock> ShopParts,
    Dictionary<int, List<BlackFlowSpireShopOfferStock>> ShopOffers, Dictionary<int, BlackFlowSpireSecretShopStock> SecretShops,
    int TargetLife, int MaxTargetLife, int TargetShield,
    BlackFlowSpireDamageMultipliers DamageMultipliers, Dictionary<ulong, decimal> PlayerLifeMultipliers,
    int Hope, int MaxHope, int CommandExperience);
internal sealed record BlackFlowSpireTeamCommand(BlackFlowSpireTeamAction Action, int Argument, BlackFlowSpireTeamSnapshot Snapshot);

internal static class BlackFlowSpireTeam
{
    private sealed class Session
    {
        public Dictionary<ulong, BlackFlowSpireTeamPosition> Ready { get; } = [];
        public long Revision { get; set; }
        public bool CommandQueued { get; set; }
        public bool AttachedToLobby { get; set; }
        public BlackFlowSpireTeamSnapshot? DeferredSnapshot { get; set; }
        public TaskCompletionSource<BlackFlowSpireShopOfferEntry?>? OfferCompletion { get; set; }
        public Dictionary<(ulong PlayerId, uint ChoiceId), TaskCompletionSource<bool>> HopePayments { get; } = [];
    }

    private static readonly ConditionalWeakTable<RunState, Session> Sessions = new();
    private static readonly RitsuLibManagedNetActionDescriptor<BlackFlowSpireTeamCommand> Command = new(
        Entry.ModId, "BlackFlowSpire-team-command/v1", Encode, Decode<BlackFlowSpireTeamCommand>, Execute, GameActionType.NonCombat);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireTeamReady> ReadyMessage = new(
        Entry.ModId, "BlackFlowSpire-team-ready/v1", Encode, Decode<BlackFlowSpireTeamReady>, OnReady, false, false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireTeamSnapshot> SnapshotMessage = new(
        Entry.ModId, "BlackFlowSpire-team-state/v1", Encode, Decode<BlackFlowSpireTeamSnapshot>, OnSnapshot,
        DispatchLocalOnBroadcast: false, ShouldBuffer: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireHopePayment> HopePaymentMessage = new(
        Entry.ModId, "BlackFlowSpire-hope-payment/v1", Encode, Decode<BlackFlowSpireHopePayment>, OnHopePayment,
        LocationTargeted: true, DispatchLocalOnBroadcast: false, ShouldBuffer: false);

    internal static void Register()
    {
        RitsuLibManagedNetActions.Register(Command);
        RitsuLibSidecarSyncMessages.Register(ReadyMessage);
        RitsuLibSidecarSyncMessages.Register(SnapshotMessage);
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
        if (IsGuest) return false;
        var session = Sessions.GetOrCreateValue(state);
        return !session.CommandQueued && session.OfferCompletion == null;
    }

    internal static void AttachLobby(RunState state)
    {
        if (!IsHost || !BlackFlowSpireGenerationService.IsEnabled(state) || RunManager.Instance.RunLobby is not { } lobby) return;
        var session = Sessions.GetOrCreateValue(state);
        if (session.AttachedToLobby) return;
        session.AttachedToLobby = true;
        lobby.RemotePlayerDisconnected += id => session.Ready.Remove(id);
        lobby.PlayerRejoined += player => session.Ready.Remove(player.id);
    }

    internal static void EndSession(RunState state)
    {
        if (!Sessions.TryGetValue(state, out var session)) return;
        session.OfferCompletion?.TrySetResult(null);
        foreach (var payment in session.HopePayments.Values) payment.TrySetResult(false);
        Sessions.Remove(state);
    }

    internal static async Task<bool> TryPayHope(RunState state, Player player, uint choiceId, int cost)
    {
        if (cost == 0) return true;
        if (!IsOnline) return BlackFlowSpireGenerationService.TryChangeHope(state, -cost);
        if (IsHost)
        {
            // Reward choices can complete concurrently. Only the host debits the shared balance.
            var accepted = BlackFlowSpireGenerationService.TryChangeHope(state, -cost, replicated: true);
            if (!RitsuLibSidecarSyncMessages.Broadcast(Net, HopePaymentMessage, new BlackFlowSpireHopePayment(player.NetId, choiceId, accepted)))
            {
                if (accepted) BlackFlowSpireGenerationService.RefundHope(state, cost);
                Entry.Logger.Warn("[BlackFlowSpire] Could not synchronize the card pack's Hope payment.");
                return false;
            }
            if (accepted) Publish(state);
            return accepted;
        }

        var payments = Sessions.GetOrCreateValue(state).HopePayments;
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
            var payments = Sessions.GetOrCreateValue(state).HopePayments;
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

    private static BlackFlowSpireTeamPosition Position(RunState state)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        return new(state.CurrentActIndex, map.PositionNodeId, BlackFlowSpireGenerationService.GetProgress(state).StepsTaken);
    }

    internal static bool CanTravel(RunState state)
    {
        if (!CanManage(state)) return false;
        if (!IsHost) return true;
        var position = Position(state);
        var ready = Sessions.GetOrCreateValue(state).Ready;
        return RunManager.Instance.RunLobby?.PlayerIds.All(id => id == Net.NetId
            || (ready.TryGetValue(id, out var reported) && reported == position)) == true;
    }

    internal static void ReportReady(RunState state, bool ready)
    {
        if (IsGuest && Net.IsConnected)
            RitsuLibSidecarSyncMessages.SendToHost(Net, ReadyMessage, new BlackFlowSpireTeamReady(Position(state), ready));
    }

    private static Task OnReady(RitsuLibSidecarSyncMessageContext<BlackFlowSpireTeamReady> context)
    {
        if (!context.IsHostIngest || !IsHost || !ReferenceEquals(context.NetService, Net)
            || !BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } state
            || RunManager.Instance.RunLobby?.PlayerIds.Contains(context.SenderNetId) != true) return Task.CompletedTask;
        var ready = Sessions.GetOrCreateValue(state).Ready;
        if (context.Message.Ready)
        {
            if (context.Message.Position == Position(state)) ready[context.SenderNetId] = context.Message.Position;
        }
        else if (ready.TryGetValue(context.SenderNetId, out var reported) && reported == context.Message.Position)
            ready.Remove(context.SenderNetId);
        return Task.CompletedTask;
    }

    internal static bool Request(RunState state, BlackFlowSpireTeamAction action, int argument)
    {
        if (!IsHost || !CanTravel(state) || NMapScreen.Instance is not { IsOpen: true } screen || BlackFlowSpireMapUi.IsInspection(screen)) return false;
        return Queue(state, action, argument);
    }

    internal static async Task<bool> BuyOffer(RunState state, BlackFlowSpireShopOfferKind kind)
    {
        if (!IsHost || !CanManage(state) || state.CurrentRoom is not MerchantRoom) return false;
        var session = Sessions.GetOrCreateValue(state);
        var completion = new TaskCompletionSource<BlackFlowSpireShopOfferEntry?>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OfferCompletion = completion;
        try
        {
            if (!Queue(state, BlackFlowSpireTeamAction.ShopOffer, (int)kind) || await completion.Task is not { } offer) return false;
            await offer.CompleteDeferredClaim();
            await offer.Save();
            return true;
        }
        finally
        {
            session.OfferCompletion = null;
        }
    }

    private static bool Queue(RunState state, BlackFlowSpireTeamAction action, int argument)
    {
        var session = Sessions.GetOrCreateValue(state);
        session.CommandQueued = true;
        var queued = false;
        try
        {
            queued = RitsuLibManagedNetActions.Request(RunManager.Instance, Command,
                new BlackFlowSpireTeamCommand(action, argument, Capture(state)));
            return queued;
        }
        finally
        {
            if (!queued) session.CommandQueued = false;
        }
    }

    private static async Task Execute(RitsuLibManagedNetActionContext<BlackFlowSpireTeamCommand> context)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } state
            || Owner(state)?.NetId != context.Player.NetId) return;
        var session = Sessions.GetOrCreateValue(state);
        var command = context.Message;
        BlackFlowSpireShopOfferEntry? purchasedOffer = null;
        session.CommandQueued = true;
        try
        {
            if (command.Snapshot.Position != Position(state))
                throw new InvalidOperationException("Forest team command arrived at a different map position.");
            if (IsGuest) Apply(state, command.Snapshot);
            switch (command.Action)
            {
                case BlackFlowSpireTeamAction.Move: await BlackFlowSpireRuntime.MoveCore(command.Argument); break;
                case BlackFlowSpireTeamAction.Passage: await BlackFlowSpireRuntime.ResolvePassageCore(command.Argument != 0); break;
                case BlackFlowSpireTeamAction.FloorExit: await BlackFlowSpireRuntime.ResolveFloorExitCore(command.Argument != 0); break;
                case BlackFlowSpireTeamAction.Hunt: await BlackFlowSpireRuntime.ConfirmHuntCore(); break;
                case BlackFlowSpireTeamAction.HuntBattle: await BlackFlowSpireRuntime.StartHuntCore(); break;
                case BlackFlowSpireTeamAction.Rest: await BlackFlowSpireRuntime.ResolveRestCore(command.Argument); break;
                case BlackFlowSpireTeamAction.ShopOffer:
                    purchasedOffer = await PurchaseOffer(state, context.Player, (BlackFlowSpireShopOfferKind)command.Argument);
                    break;
                default: throw new InvalidDataException("Unknown forest team command.");
            }
        }
        finally
        {
            session.CommandQueued = false;
            if (command.Action == BlackFlowSpireTeamAction.ShopOffer) session.OfferCompletion?.TrySetResult(purchasedOffer);
            Flush(state);
            Publish(state);
        }
    }

    private static async Task<BlackFlowSpireShopOfferEntry?> PurchaseOffer(RunState state, Player player, BlackFlowSpireShopOfferKind kind)
    {
        if (state.CurrentRoom is not MerchantRoom) return null;
        var inventory = RunManager.Instance.MerchantSynchronizer.GetInventoryFor(player);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!progress.ShopOffers.TryGetValue(Position(state).Node, out var offers)) return null;
        var stock = offers.FirstOrDefault(item => item.Kind == kind && !item.Purchased);
        if (stock == null) return null;
        var offer = new BlackFlowSpireShopOfferEntry(player, stock, replicated: true);
        return await offer.OnTryPurchaseUnsynchronized(inventory) ? offer : null;
    }

    private static BlackFlowSpireTeamSnapshot Capture(RunState state)
    {
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var (life, maxLife, shield) = BlackFlowSpireGenerationService.GetTargetLife(state);
        var hope = BlackFlowSpireGenerationService.GetHopeStatus(state);
        return new(Position(state), ++Sessions.GetOrCreateValue(state).Revision,
            BlackFlowSpireGenerationService.GetEngine(state), progress.ActionPointsRemaining, progress.HuntPhase,
            progress.ShopParts, progress.ShopOffers, progress.SecretShops, life, maxLife, shield,
            BlackFlowSpireGenerationService.GetDamageMultipliers(state), BlackFlowSpireGenerationService.CopyPlayerLifeMultipliers(state),
            hope.Balance, hope.Maximum, BlackFlowSpireGenerationService.GetCommandExperience(state));
    }

    internal static void Publish(RunState state)
    {
        if (!IsHost || !Net.IsConnected || !BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)) return;
        if (!RitsuLibSidecarSyncMessages.Broadcast(Net, SnapshotMessage, Capture(state)))
            Entry.Logger.Warn("[BlackFlowSpire] Could not publish shared forest state; next team command carries the current state.");
    }

    private static Task OnSnapshot(RitsuLibSidecarSyncMessageContext<BlackFlowSpireTeamSnapshot> context)
    {
        if (Net is not NetClientGameService { IsConnected: true } client || context.SenderNetId != client.HostNetId
            || !ReferenceEquals(context.NetService, Net) || !BlackFlowSpireRuntime.CanContinueRun
            || BlackFlowSpireRuntime.State is not { } state) return Task.CompletedTask;
        var session = Sessions.GetOrCreateValue(state);
        if (context.Message.Revision > Math.Max(session.Revision, session.DeferredSnapshot?.Revision ?? 0))
            session.DeferredSnapshot = context.Message;
        Flush(state);
        return Task.CompletedTask;
    }

    internal static void Flush(RunState state)
    {
        if (!IsGuest || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress || !ReferenceEquals(BlackFlowSpireRuntime.State, state)) return;
        var session = Sessions.GetOrCreateValue(state);
        if (session.CommandQueued) return;
        if (session.DeferredSnapshot is not { } snapshot) return;
        if (snapshot.Revision <= session.Revision)
        {
            session.DeferredSnapshot = null;
            return;
        }
        if (snapshot.Position != Position(state)) return;
        session.DeferredSnapshot = null;
        Apply(state, snapshot);
        BlackFlowSpirePartsUi.RefreshSharedBox();
        BlackFlowSpireCollectibleScreen.RefreshShared();
        if (NMerchantRoom.Instance is { } room) BlackFlowSpireShopUi.Find(room)?.RefreshSharedStock();
        if (NMapScreen.Instance is { IsOpen: true } screen)
            BlackFlowSpireMapUi.Show(screen, state, BlackFlowSpireMapUi.IsInspection(screen));
    }

    private static void Apply(RunState state, BlackFlowSpireTeamSnapshot snapshot)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        BlackFlowSpireGenerationService.SetSharedEngine(state, snapshot.Engine);
        // The host inventory already includes the appraisal from these discoveries.
        BlackFlowSpireCollectibleRules.RevealMap(map, snapshot.Engine, countInformation: false);
        BlackFlowSpireGenerationService.SetTargetLife(state, snapshot.TargetLife, snapshot.MaxTargetLife, snapshot.TargetShield);
        BlackFlowSpireGenerationService.RestoreCommandProgress(state, snapshot.Hope, snapshot.MaxHope, snapshot.CommandExperience);
        BlackFlowSpireGenerationService.RestoreDamageMultipliers(state, snapshot.DamageMultipliers);
        BlackFlowSpireGenerationService.RestorePlayerLifeMultipliers(state, snapshot.PlayerLifeMultipliers);
        progress.ActionPointsRemaining = snapshot.ActionPoints;
        progress.HuntPhase = snapshot.Hunt;
        progress.ShopParts = snapshot.ShopParts;
        progress.ShopOffers = snapshot.ShopOffers;
        progress.SecretShops = snapshot.SecretShops;
        BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        var session = Sessions.GetOrCreateValue(state);
        session.Revision = Math.Max(session.Revision, snapshot.Revision);
    }
}
