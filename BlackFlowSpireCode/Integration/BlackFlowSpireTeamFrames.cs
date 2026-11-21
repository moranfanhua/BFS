using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.Sidecar;

namespace BlackFlowSpire.Integration;

internal enum ForestBoundary { Begin, CrossClearing, EnterNode, HuntBattle, CombatArrival, NextAct, FloorGenerated, Finish }
internal sealed record BlackFlowSpireGoldChange(ulong Player, int Amount);
internal sealed record BlackFlowSpireTeamFrame(long Command, int Index, ForestBoundary Boundary, BlackFlowSpireTeamSnapshot Snapshot,
    BlackFlowSpireGoldChange[] Gold, BlackFlowSpireExpeditionDispatch? Dispatched, BlackFlowSpireExpeditionDispatch? Returned);

internal static partial class BlackFlowSpireTeam
{
    private static readonly RitsuLibSidecarSyncMessageDescriptor<BlackFlowSpireTeamFrame> FrameMessage = new(
        Entry.ModId, "BlackFlowSpire-team-frame/v3", Encode, Decode<BlackFlowSpireTeamFrame>, OnFrame,
        ShouldBuffer: false, DispatchLocalOnBroadcast: false);

    internal static bool InCommand(RunState state) => Sessions.TryGetValue(state, out var session) && session.Active != 0 && !session.NativePurchase;

    internal static void RecordGold(RunState state, int amount, Player? player = null)
    {
        if (IsHost && InCommand(state) && (player ?? Owner(state)) is { } owner)
            SessionFor(state).Gold.Add(new(owner.NetId, amount));
    }

    internal static void RecordDeck(RunState state, BlackFlowSpireExpeditionDispatch dispatch, bool returned)
    {
        if (!IsHost || !InCommand(state)) return;
        var session = SessionFor(state);
        if (returned) session.Returned = dispatch;
        else session.Dispatched = dispatch;
    }

    internal static void Emit(RunState state, ForestBoundary boundary)
    {
        if (!IsHost || !Sessions.TryGetValue(state, out var session) || session.Active == 0) return;
        var frame = new BlackFlowSpireTeamFrame(session.Active, session.Sequence++, boundary, Capture(state),
            session.Gold.ToArray(), session.Dispatched, session.Returned);
        if (!RitsuLibSidecarSyncMessages.Broadcast(Net, FrameMessage, frame))
            throw new IOException($"Could not broadcast forest {boundary} for command {frame.Command}.");
        session.Gold.Clear();
        session.Dispatched = session.Returned = null;
    }

    private static Task OnFrame(RitsuLibSidecarSyncMessageContext<BlackFlowSpireTeamFrame> context)
    {
        if (context.NetService is NetClientGameService { IsConnected: true } client && context.SenderNetId == client.HostNetId)
        {
            var frame = context.Message;
            Connections.GetOrCreateValue(client).Offer(frame.Command, frame.Index, frame);
        }
        return Task.CompletedTask;
    }

    internal static async Task<ForestBoundary> Receive(RunState state, ForestBoundary? expected = null)
    {
        var session = SessionFor(state);
        var frame = await session.Frames.Next(session.Active, session.Sequence++);
        var snapshot = frame.Snapshot;
        var valid = expected.HasValue ? frame.Boundary == expected : frame.Boundary is ForestBoundary.CrossClearing
            or ForestBoundary.EnterNode or ForestBoundary.HuntBattle or ForestBoundary.NextAct or ForestBoundary.Finish;
        if (!valid || snapshot.Room != BlackFlowSpireRoomKey.From(state)
            || snapshot.Revision <= session.Revision)
            throw new InvalidDataException($"Unexpected forest {frame.Boundary} at {snapshot.Room}, sequence {frame.Index}.");
        foreach (var change in frame.Gold)
        {
            var player = state.Players.Single(p => p.NetId == change.Player);
            if (change.Amount > 0) await PlayerCmd.GainGold(change.Amount, player);
            else if (change.Amount < 0) await PlayerCmd.LoseGold(-change.Amount, player, GoldLossType.Spent);
        }
        if (frame.Dispatched is { } sent) BlackFlowSpireGenerationService.ApplyExpeditionDeck(state, sent, returned: false);
        if (frame.Returned is { } returned) BlackFlowSpireGenerationService.ApplyExpeditionDeck(state, returned, returned: true);
        BlackFlowSpireGenerationService.ApplyFloor(state, snapshot.Forest);
        session.Revision = snapshot.Revision;
        if (frame.Dispatched != null) BlackFlowSpireGenerationService.RefreshExpeditionDecks(state, frame.Dispatched);
        if (frame.Returned != null) BlackFlowSpireGenerationService.RefreshExpeditionDecks(state, frame.Returned);
        return frame.Boundary;
    }

    internal static Task ReceiveExpected(RunState state, ForestBoundary expected) => Receive(state, expected);
}
