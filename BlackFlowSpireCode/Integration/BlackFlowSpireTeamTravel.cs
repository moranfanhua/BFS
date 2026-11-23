using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static async Task FollowHost(RunState state)
    {
        var manager = RunManager.Instance;
        var faded = false;
        IsTraveling = true;
        try
        {
            while (true)
            {
                var boundary = await BlackFlowSpireTeam.Receive(state);
                if (boundary == ForestBoundary.Finish) break;
                if (boundary == ForestBoundary.NextAct)
                {
                    await manager.EnterNextAct();
                    continue;
                }
                if (boundary is not (ForestBoundary.CrossClearing or ForestBoundary.EnterNode or ForestBoundary.HuntBattle))
                    throw new InvalidDataException($"Unexpected forest travel boundary: {boundary}.");
                if (!faded)
                {
                    await manager.FadeOut();
                    faded = true;
                }
                var progress = BlackFlowSpireGenerationService.GetProgress(state);
                var pointType = MapPointType.Monster;
                if (boundary != ForestBoundary.HuntBattle)
                {
                    var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
                    var node = map.GetNode(map.PositionNodeId);
                    var coord = new MapCoord(node.X, node.Y);
                    Visited(state).Add(coord);
                    AccessTools.PropertySetter(typeof(RunState), nameof(RunState.NextRoomId)).Invoke(state, [0]);
                    pointType = state.Map.GetPoint(coord)!.PointType;
                }
                var intermediate = boundary == ForestBoundary.CrossClearing;
                await manager.EnterMapPointInternal(progress.StepsTaken, pointType, intermediate ? new MapRoom() : null, true);
                if (intermediate) state.AppendToMapPointHistory(pointType, RoomType.Map, null);
            }
        }
        finally
        {
            IsTraveling = false;
            if (faded && ReferenceEquals(State, state)) await manager.FadeIn();
        }
        if (!CanContinueRun || !ReferenceEquals(State, state)) return;
        if (state.CurrentRoom is MapRoom && NMapScreen.Instance is { IsOpen: false } screen) OpenMap(screen, false);
        else BlackFlowSpireTeam.RefreshViews(state);
    }
}
