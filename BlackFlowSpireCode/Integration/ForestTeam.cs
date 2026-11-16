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

internal enum ForestTeamAction { Move, Passage, FloorExit, Hunt, ShopOffer, HuntBattle }

// Steps distinguish revisits to the same node. Act distinguishes floor changes.
internal sealed record ForestTeamPosition(int Act, int Node, int Steps);
internal sealed record ForestTeamReady(ForestTeamPosition Position, bool Ready);
internal sealed record ForestHopePayment(ulong PlayerId, uint ChoiceId, bool Accepted);
internal sealed record ForestTeamSnapshot(ForestTeamPosition Position, long Revision, ForestEngineInventory Engine,
    int ActionPoints, ForestHuntPhase Hunt, Dictionary<int, ForestShopPartStock> ShopParts,
    Dictionary<int, List<ForestShopOfferStock>> ShopOffers, Dictionary<int, ForestSecretShopStock> SecretShops,
    int TargetLife = ForestCombatHealth.InitialTargetLife,
    int MaxTargetLife = ForestCombatHealth.InitialTargetLife,
    int TargetShield = 0,
    ForestDamageMultipliers? DamageMultipliers = null,
    Dictionary<ulong, decimal>? PlayerLifeMultipliers = null,
    int Hope = 0, int? MaxHope = null, int CommandExperience = 0);
internal sealed record ForestTeamCommand(ForestTeamAction Action, int Argument, ForestTeamSnapshot Snapshot);

internal static class ForestTeam
{
    private sealed class Session
    {
        public Dictionary<ulong, ForestTeamPosition> Ready { get; } = [];
        public long Revision { get; set; }
        public bool CommandQueued { get; set; }
        public bool AttachedToLobby { get; set; }
        public ForestTeamSnapshot? DeferredSnapshot { get; set; }
        public TaskCompletionSource<ForestShopOfferEntry?>? OfferCompletion { get; set; }
        public Dictionary<(ulong PlayerId, uint ChoiceId), TaskCompletionSource<bool>> HopePayments { get; } = [];
    }

    private static readonly ConditionalWeakTable<RunState, Session> Sessions = new();
    private static readonly RitsuLibManagedNetActionDescriptor<ForestTeamCommand> Command = new(
        Entry.ModId, "forest-team-command/v1", Encode, Decode<ForestTeamCommand>, Execute, GameActionType.NonCombat);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<ForestTeamReady> ReadyMessage = new(
        Entry.ModId, "forest-team-ready/v1", Encode, Decode<ForestTeamReady>, OnReady, false, false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<ForestTeamSnapshot> SnapshotMessage = new(
        Entry.ModId, "forest-team-state/v1", Encode, Decode<ForestTeamSnapshot>, OnSnapshot,
        DispatchLocalOnBroadcast: false, ShouldBuffer: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<ForestHopePayment> HopePaymentMessage = new(
        Entry.ModId, "forest-hope-payment/v1", Encode, Decode<ForestHopePayment>, OnHopePayment,
        LocationTargeted: true, DispatchLocalOnBroadcast: false, ShouldBuffer: false);

    internal static void Register()
    {
        RitsuLibManagedNetActions.Register(Command);
        RitsuLibSidecarSyncMessages.Register(ReadyMessage);
        RitsuLibSidecarSyncMessages.Register(SnapshotMessage);
        RitsuLibSidecarSyncMessages.Register(HopePaymentMessage);
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(e =>
        {
            if (e.IsMultiplayer && ForestGenerationService.IsEnabled(e.RunState))
                ForestCampaignService.ConfigureNewRun(e.RunState);
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
        if (!IsHost || !ForestGenerationService.IsEnabled(state) || RunManager.Instance.RunLobby is not { } lobby) return;
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
        if (!IsOnline) return ForestGenerationService.TryChangeHope(state, -cost);
        if (IsHost)
        {
            // Reward choices can complete concurrently. Only the host debits the shared balance.
            var accepted = ForestGenerationService.TryChangeHope(state, -cost, replicated: true);
            if (!RitsuLibSidecarSyncMessages.Broadcast(Net, HopePaymentMessage, new ForestHopePayment(player.NetId, choiceId, accepted)))
            {
                if (accepted) ForestGenerationService.RefundHope(state, cost);
                Entry.Logger.Warn("[Forest] Could not synchronize the card pack's Hope payment.");
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

    private static Task OnHopePayment(RitsuLibSidecarSyncMessageContext<ForestHopePayment> context)
    {
        if (Net is NetClientGameService { IsConnected: true } client && context.SenderNetId == client.HostNetId
            && ReferenceEquals(context.NetService, Net) && ForestRuntime.State is { } state)
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
            _ => ForestGenerationService.GetTeamOwnerId(state)
        };
        return owner is { } id ? state.Players.FirstOrDefault(player => player.NetId == id) : LocalContext.GetMe(state);
    }

    private static ForestTeamPosition Position(RunState state)
    {
        var map = ForestGenerationService.GetOrGenerate(state);
        return new(state.CurrentActIndex, map.PositionNodeId, ForestGenerationService.GetProgress(state).StepsTaken);
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
            RitsuLibSidecarSyncMessages.SendToHost(Net, ReadyMessage, new ForestTeamReady(Position(state), ready));
    }

    private static Task OnReady(RitsuLibSidecarSyncMessageContext<ForestTeamReady> context)
    {
        if (!context.IsHostIngest || !IsHost || !ReferenceEquals(context.NetService, Net)
            || !ForestRuntime.CanContinueRun || ForestRuntime.State is not { } state
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

    internal static bool Request(RunState state, ForestTeamAction action, int argument)
    {
        if (!IsHost || !CanTravel(state) || NMapScreen.Instance is not { IsOpen: true } screen || ForestMapUi.IsInspection(screen)) return false;
        return Queue(state, action, argument);
    }

    internal static async Task<bool> BuyOffer(RunState state, ForestShopOfferKind kind)
    {
        if (!IsHost || !CanManage(state) || state.CurrentRoom is not MerchantRoom) return false;
        var session = Sessions.GetOrCreateValue(state);
        var completion = new TaskCompletionSource<ForestShopOfferEntry?>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OfferCompletion = completion;
        try
        {
            if (!Queue(state, ForestTeamAction.ShopOffer, (int)kind) || await completion.Task is not { } offer) return false;
            await offer.CompleteDeferredClaim();
            await offer.Save();
            return true;
        }
        finally
        {
            session.OfferCompletion = null;
        }
    }

    private static bool Queue(RunState state, ForestTeamAction action, int argument)
    {
        var session = Sessions.GetOrCreateValue(state);
        session.CommandQueued = true;
        var queued = false;
        try
        {
            queued = RitsuLibManagedNetActions.Request(RunManager.Instance, Command,
                new ForestTeamCommand(action, argument, Capture(state)));
            return queued;
        }
        finally
        {
            if (!queued) session.CommandQueued = false;
        }
    }

    private static async Task Execute(RitsuLibManagedNetActionContext<ForestTeamCommand> context)
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.State is not { } state
            || Owner(state)?.NetId != context.Player.NetId) return;
        var session = Sessions.GetOrCreateValue(state);
        var command = context.Message;
        ForestShopOfferEntry? purchasedOffer = null;
        session.CommandQueued = true;
        try
        {
            if (command.Snapshot.Position != Position(state))
                throw new InvalidOperationException("Forest team command arrived at a different map position.");
            if (IsGuest) Apply(state, command.Snapshot);
            switch (command.Action)
            {
                case ForestTeamAction.Move: await ForestRuntime.MoveCore(command.Argument); break;
                case ForestTeamAction.Passage: await ForestRuntime.ResolvePassageCore(command.Argument != 0); break;
                case ForestTeamAction.FloorExit: await ForestRuntime.ResolveFloorExitCore(command.Argument != 0); break;
                case ForestTeamAction.Hunt: await ForestRuntime.ConfirmHuntCore(); break;
                case ForestTeamAction.HuntBattle: await ForestRuntime.StartHuntCore(); break;
                case ForestTeamAction.ShopOffer:
                    purchasedOffer = await PurchaseOffer(state, context.Player, (ForestShopOfferKind)command.Argument);
                    break;
                default: throw new InvalidDataException("Unknown forest team command.");
            }
        }
        finally
        {
            session.CommandQueued = false;
            if (command.Action == ForestTeamAction.ShopOffer) session.OfferCompletion?.TrySetResult(purchasedOffer);
            Flush(state);
            Publish(state);
        }
    }

    private static async Task<ForestShopOfferEntry?> PurchaseOffer(RunState state, Player player, ForestShopOfferKind kind)
    {
        if (state.CurrentRoom is not MerchantRoom) return null;
        var inventory = RunManager.Instance.MerchantSynchronizer.GetInventoryFor(player);
        var progress = ForestGenerationService.GetProgress(state);
        if (!progress.ShopOffers.TryGetValue(Position(state).Node, out var offers)) return null;
        var stock = offers.FirstOrDefault(item => item.Kind == kind && !item.Purchased);
        if (stock == null) return null;
        var offer = new ForestShopOfferEntry(player, stock, replicated: true);
        return await offer.OnTryPurchaseUnsynchronized(inventory) ? offer : null;
    }

    private static ForestTeamSnapshot Capture(RunState state)
    {
        var progress = ForestGenerationService.GetProgress(state);
        var (life, maxLife, shield) = ForestGenerationService.GetTargetLife(state);
        var hope = ForestGenerationService.GetHopeStatus(state);
        return new(Position(state), ++Sessions.GetOrCreateValue(state).Revision,
            ForestGenerationService.GetEngine(state), progress.ActionPointsRemaining, progress.HuntPhase,
            progress.ShopParts, progress.ShopOffers, progress.SecretShops, life, maxLife, shield,
            ForestGenerationService.GetDamageMultipliers(state), ForestGenerationService.CopyPlayerLifeMultipliers(state),
            hope.Balance, hope.Maximum, ForestGenerationService.GetCommandExperience(state));
    }

    internal static void Publish(RunState state)
    {
        if (!IsHost || !Net.IsConnected || !ForestRuntime.CanContinueRun || !ReferenceEquals(ForestRuntime.State, state)) return;
        if (!RitsuLibSidecarSyncMessages.Broadcast(Net, SnapshotMessage, Capture(state)))
            Entry.Logger.Warn("[Forest] Could not publish shared forest state; next team command carries the current state.");
    }

    private static Task OnSnapshot(RitsuLibSidecarSyncMessageContext<ForestTeamSnapshot> context)
    {
        if (Net is not NetClientGameService { IsConnected: true } client || context.SenderNetId != client.HostNetId
            || !ReferenceEquals(context.NetService, Net) || !ForestRuntime.CanContinueRun
            || ForestRuntime.State is not { } state) return Task.CompletedTask;
        var session = Sessions.GetOrCreateValue(state);
        if (context.Message.Revision > Math.Max(session.Revision, session.DeferredSnapshot?.Revision ?? 0))
            session.DeferredSnapshot = context.Message;
        Flush(state);
        return Task.CompletedTask;
    }

    internal static void Flush(RunState state)
    {
        if (!IsGuest || ForestRuntime.IsTraveling || CombatManager.Instance.IsInProgress || !ReferenceEquals(ForestRuntime.State, state)) return;
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
        ForestPartsUi.RefreshSharedBox();
        ForestCollectibleScreen.RefreshShared();
        if (NMerchantRoom.Instance is { } room) ForestShopUi.Find(room)?.RefreshSharedStock();
        if (NMapScreen.Instance is { IsOpen: true } screen)
            ForestMapUi.Show(screen, state, ForestMapUi.IsInspection(screen));
    }

    private static void Apply(RunState state, ForestTeamSnapshot snapshot)
    {
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        ForestGenerationService.SetSharedEngine(state, snapshot.Engine);
        // The host inventory already includes the appraisal from these discoveries.
        ForestCollectibleRules.RevealMap(map, snapshot.Engine, countInformation: false);
        ForestGenerationService.SetTargetLife(state, snapshot.TargetLife, snapshot.MaxTargetLife, snapshot.TargetShield);
        ForestGenerationService.RestoreCommandProgress(state, snapshot.Hope, snapshot.MaxHope, snapshot.CommandExperience);
        ForestGenerationService.RestoreDamageMultipliers(state, snapshot.DamageMultipliers ?? new());
        ForestGenerationService.RestorePlayerLifeMultipliers(state, snapshot.PlayerLifeMultipliers);
        progress.ActionPointsRemaining = snapshot.ActionPoints;
        progress.HuntPhase = snapshot.Hunt;
        progress.ShopParts = snapshot.ShopParts;
        progress.ShopOffers = snapshot.ShopOffers;
        progress.SecretShops = snapshot.SecretShops;
        ForestGenerationService.MarkChanged(state, map, progress);
        var session = Sessions.GetOrCreateValue(state);
        session.Revision = Math.Max(session.Revision, snapshot.Revision);
    }
}
