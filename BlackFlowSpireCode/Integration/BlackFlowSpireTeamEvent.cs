using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.Sidecar;

namespace BlackFlowSpire.Integration;

internal enum ForestEventPage { Map, Rest, Wish, Sacrifice, FloorExit, Passage, HuntNotice, HuntDetails, HuntIntel, ExpeditionResult }

// Only presentation state travels here. Each client renders its own language and assets.
internal sealed record ForestEventView(ForestEventPage Page, int Option = -1, bool? Parts = null,
    int? Item = null, bool ConfirmLoaded = false, int Scroll = 0, bool TextRevealed = false)
{
    public static readonly ForestEventView Map = new(ForestEventPage.Map);
}

internal sealed record ForestEventMessage(long Sequence, long Command, int Act, int Node, int Steps, ForestEventView View)
{
    public bool Matches(long command, int act, int node, int steps) =>
        Command == command && Act == act && Node == node && Steps == steps;
}

internal static partial class BlackFlowSpireTeam
{
    private static readonly RitsuLibSidecarSyncMessageDescriptor<ForestEventMessage> EventMessage = new(
        Entry.ModId, "BlackFlowSpire-event-view/v1", Encode, Decode<ForestEventMessage>, OnEventView,
        DispatchLocalOnBroadcast: false, ShouldBuffer: false);

    internal static void ShareEventView(RunState state, int node, int steps, ForestEventView view)
    {
        if (!IsHost || !Net.IsConnected) return;
        var session = SessionFor(state);
        if (session.CommandQueued) return;
        var message = session.EventView;
        if (message == null && view.Page == ForestEventPage.Map) return;
        if (message == null || message.View != view || !message.Matches(session.Completed, state.CurrentActIndex, node, steps))
        {
            message = new((message?.Sequence ?? 0) + 1, session.Completed, state.CurrentActIndex, node, steps, view);
            session.EventView = message;
            session.EventViewSent = false;
        }
        // A broadcast can reach some peers before failing. Retry that same sequence until it succeeds.
        if (!session.EventViewSent) session.EventViewSent = RitsuLibSidecarSyncMessages.Broadcast(Net, EventMessage, message);
    }

    internal static ForestEventView ReadEventView(RunState state, int node, int steps)
    {
        var session = SessionFor(state);
        return !session.CommandQueued && session.EventView is { } message
            && message.Matches(session.Completed, state.CurrentActIndex, node, steps) ? message.View : ForestEventView.Map;
    }

    private static Task OnEventView(RitsuLibSidecarSyncMessageContext<ForestEventMessage> context)
    {
        if (Net is NetClientGameService { IsConnected: true } client && context.SenderNetId == client.HostNetId
            && ReferenceEquals(context.NetService, Net) && BlackFlowSpireRuntime.CanContinueRun && BlackFlowSpireRuntime.State is { } state)
        {
            var session = SessionFor(state);
            if (context.Message.Sequence > (session.EventView?.Sequence ?? 0)) session.EventView = context.Message;
        }
        return Task.CompletedTask;
    }
}
