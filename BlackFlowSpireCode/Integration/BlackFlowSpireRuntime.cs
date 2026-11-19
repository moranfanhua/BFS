using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    public static bool PendingNewRun { get; set; }
    public static bool IsTraveling { get; private set; }
    public static string? LastError { get; private set; }
    public static RunState? State => RunManager.Instance.DebugOnlyGetState();
    public static bool IsActive => BlackFlowSpireGenerationService.IsEnabled(State);
    public static bool CanContinueRun => IsActive && !RunManager.Instance.IsAbandoned
        && !RunManager.Instance.IsGameOver && !RunManager.Instance.IsCleaningUp;
    private static readonly AccessTools.FieldRef<RunState, List<MapCoord>> Visited =
        AccessTools.FieldRefAccess<RunState, List<MapCoord>>("_visitedMapCoords");

    public static Task GenerateMap(RunManager manager)
    {
        var state = manager.DebugOnlyGetState()!;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.EnterFloor(state.CurrentActIndex));
        manager.MapSelectionSynchronizer.BeforeMapGenerated();
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        BlackFlowSpireGenerationService.ApplyFloorTransition(state, map, progress);
        BlackFlowSpireGenerationService.SettleExpeditionReturn(state, map, progress);
        state.Map = new BlackFlowSpireActMap(map);
        state.ExtraFields.StartedWithNeow = false;
        if (!state.CurrentMapCoord.HasValue)
            state.AddVisitedMapCoord(new MapCoord(map.GetNode(map.PositionNodeId).X, map.GetNode(map.PositionNodeId).Y));
        NMapScreen.Instance?.SetMap(state.Map, state.Rng.Seed, true);
        Entry.Logger.Info($"[BlackFlowSpire] Map generated: act={state.CurrentActIndex}, seed={map.Seed}, nodes={map.Nodes.Count}");
        return BlackFlowSpireCollectibleService.OnFloorEntered(state, map);
    }

    public static void OpenMap(NMapScreen screen, bool fromTopBar, bool focusCurrent = false)
    {
        // Intermediate map rooms retain native history/checkpoints without reopening the UI.
        if (!CanContinueRun || screen.IsOpen || IsTraveling) return;
        var state = State!;
        BlackFlowSpireTeam.Flush(state);
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var inspection = fromTopBar && state.CurrentRoom is not MapRoom;
        if (!inspection && BlackFlowSpireGenerationService.GetFloorTransition(state) is { } transition
            && transition.SourceActIndex == state.CurrentActIndex)
        {
            // Only an accepted and checkpointed choice may resume a floor transition.
            TaskHelper.RunSafely(AdvanceThroughExit());
            return;
        }
        var completedNow = false;
        if (!inspection && !CombatManager.Instance.IsInProgress && progress.PendingNodeId.HasValue
            && !BlackFlowSpirePassageRules.IsPending(map, progress) && !BlackFlowSpireFloorExitRules.IsPending(map, progress)
            && !BlackFlowSpireRestRules.IsPending(map, progress) && !BlackFlowSpireSacrificeRules.IsPending(map, progress)
            && !BlackFlowSpireWishRules.IsPending(map, progress) && !BlackFlowSpireExpeditionRules.IsPending(map, progress))
        {
            BlackFlowSpireTravelRules.CompleteEncounter(map, progress);
            ((BlackFlowSpireActMap)state.Map).RefreshPointTypes();
            completedNow = true;
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            Entry.Logger.Info($"[BlackFlowSpire] Completed node {map.PositionNodeId}");
        }
        var huntStarted = !inspection && !CombatManager.Instance.IsInProgress
            && BlackFlowSpireHuntRules.TryBegin(map, progress, BlackFlowSpireGenerationService.GetEngine(state));
        if (huntStarted)
        {
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            Entry.Logger.Info("[BlackFlowSpire] Hunt queued after the current node settled at zero AP.");
        }
        foreach (var child in screen.GetChildren().OfType<Control>()) child.Hide();
        SetMapFlag(screen, "IsOpen", true);
        screen.ProcessMode = Node.ProcessModeEnum.Inherit;
        screen.Show();
        if (RunManager.Instance.IsSingleplayerOrFakeMultiplayer) CombatManager.Instance.Pause();
        BlackFlowSpireMapUi.Show(screen, state, inspection, focusCurrent: focusCurrent);
        ActiveScreenContext.Instance.Update();
        screen.EmitSignal(NMapScreen.SignalName.Opened);
        if (!inspection && !CombatManager.Instance.IsInProgress)
        {
            BlackFlowSpireTeam.ReportReady(state, true);
            BlackFlowSpireTeam.Publish(state);
        }
        // Save the central opening too: the vanilla pause menu relies on existing autosaves.
        // Cleared rooms persist claimed rewards and completion as one map checkpoint.
        if (completedNow || huntStarted || progress.StepsTaken == 0 && state.CurrentRoom is MapRoom)
            TaskHelper.RunSafely(SaveManager.Instance.SaveRun(null));
    }

    public static void CloseMap(NMapScreen screen, bool animateOut = true)
    {
        if (!screen.IsOpen || (animateOut && IsTraveling && CanContinueRun)) return;
        if (IsActive && BlackFlowSpireHuntRules.BlocksMapClose(BlackFlowSpireGenerationService.GetProgress(State!).HuntPhase,
                animateOut, IsTraveling, CanContinueRun)) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpirePassageRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpireFloorExitRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpireRestRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpireSacrificeRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpireWishRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && animateOut && !IsTraveling && CanContinueRun
            && BlackFlowSpireExpeditionRules.IsPending(BlackFlowSpireGenerationService.GetOrGenerate(State!),
                BlackFlowSpireGenerationService.GetProgress(State!))) return;
        if (IsActive && !IsTraveling && !BlackFlowSpireMapUi.IsInspection(screen)) BlackFlowSpireTeam.ReportReady(State!, false);
        SetMapFlag(screen, "IsOpen", false);
        screen.Hide();
        screen.ProcessMode = Node.ProcessModeEnum.Disabled;
        BlackFlowSpireHuntUi.DismissNotice(screen);
        BlackFlowSpirePassageUi.DismissNotice(screen);
        BlackFlowSpireRestUi.Dismiss(screen);
        BlackFlowSpireSacrificeUi.Dismiss(screen);
        BlackFlowSpireWishUi.Dismiss(screen);
        BlackFlowSpireExpeditionUi.Dismiss(screen);
        BlackFlowSpireMovementModeScreen.Dismiss(screen);
        BlackFlowSpirePartsUi.DismissBox();
        if (RunManager.Instance.IsSingleplayerOrFakeMultiplayer) CombatManager.Instance.Unpause();
        ActiveScreenContext.Instance.Update();
        screen.EmitSignal(NMapScreen.SignalName.Closed);
    }

    public static void SetMapFlag(NMapScreen screen, string name, bool value) =>
        AccessTools.PropertySetter(typeof(NMapScreen), name).Invoke(screen, [value]);

    public static RoomType CurrentRoomType(RunState state)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (progress.HuntPhase == BlackFlowSpireHuntPhase.InBattle) return RoomType.Monster;
        var node = map.GetNode(map.PositionNodeId);
        if (BlackFlowSpireResidentRules.IsSpecialCombat(map, node)
            && progress.PendingNodeId == node.Id)
            return BlackFlowSpireResidentRules.CombatSetupFor(map, node) != null ? RoomType.Monster : RoomType.Map;
        if (node.Completed || node.Kind == BlackFlowSpireNodeKind.Empty) return RoomType.Map;
        return ((BlackFlowSpireActMap)state.Map).GetFixedRoomType(new MapCoord(node.X, node.Y)) ?? RoomType.Map;
    }

    public static Task Move(int targetId) => SubmitTeamAction(BlackFlowSpireTeamAction.Move, targetId, () => MoveCore(targetId));

    internal static async Task MoveCore(int targetId)
    {
        if (IsTraveling || !CanContinueRun) return;
        var manager = RunManager.Instance;
        var state = State!;
        // Completed events can reopen the map while the native current room is still EventRoom.
        // The pending encounter flag in PlanMove guards room completion; combat also blocks input.
        if (CombatManager.Instance.IsInProgress) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var screen = NMapScreen.Instance!;
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        try
        {
            IsTraveling = true;
            LastError = null;
            var route = BlackFlowSpireTravelRules.PlanMove(map, progress, targetId, engine);
            Entry.Logger.Info($"[BlackFlowSpire] Route {string.Join("->", route.NodeIds)}, steps={route.Steps}, cost={route.ActionPointCost}");
            // Keep the map over the outgoing room until native room entry clears it.
            await manager.FadeOut();
            foreach (var id in route.NodeIds.Skip(1))
            {
                var result = BlackFlowSpireTravelRules.Move(map, progress, id, enterDestination: id == targetId, inventory: engine);
                BlackFlowSpirePartService.PrepareMovementRewards(state, map, progress, result);
                ((BlackFlowSpireActMap)state.Map).RefreshPointTypes();
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
                var node = map.GetNode(id);
                // Keep every traversed coordinate and native history entry, including return visits.
                Visited(state).Add(new MapCoord(node.X, node.Y));
                AccessTools.PropertySetter(typeof(RunState), nameof(RunState.NextRoomId)).Invoke(state, [0]);
                Entry.Logger.Info($"[BlackFlowSpire] Move to {id}, AP={result.ActionPointsRemaining}, room={result.RequiresRoom}");
                var pointType = state.Map.GetPoint(new MapCoord(node.X, node.Y))!.PointType;
                // Traversed clearings have no encounter to synchronize. Keep their
                // history, but run the network room handshake once at the destination.
                var intermediate = BlackFlowSpireTeam.IsOnline && id != targetId;
                await manager.EnterMapPointInternal(progress.StepsTaken, pointType, intermediate ? new MapRoom() : null, true);
                if (intermediate) state.AppendToMapPointHistory(pointType, RoomType.Map, null);
                var history = state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex);
                if (history != null) progress.HistoryIndexes[id] = history.Count - 1;
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
                await BlackFlowSpirePartService.SettleMovementRewards(state, map, progress);
                if (!result.RequiresRoom) await SaveManager.Instance.SaveRun(null);
            }
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Travel failed: {error}");
        }
        finally
        {
            IsTraveling = false;
            await RefreshMapAfterTravel(state, map, progress, screen);
            if (CanContinueRun && ReferenceEquals(State, state)) await manager.FadeIn();
        }
    }

    internal static async Task RestoreMapPoint(RunManager manager, RunState state, BlackFlowSpireMap map,
        BlackFlowSpireExplorationProgress progress, MapCoord coordinate, AbstractRoom? preFinishedRoom, bool saveGame)
    {
        var alreadyTraveling = IsTraveling;
        IsTraveling = true;
        try
        {
            await manager.EnterMapPointInternal(progress.StepsTaken, state.Map.GetPoint(coordinate)!.PointType,
                preFinishedRoom, saveGame);
            await BlackFlowSpirePartService.SettleMovementRewards(state, map, progress);
        }
        finally
        {
            IsTraveling = alreadyTraveling;
        }
        if (!alreadyTraveling && CanContinueRun && ReferenceEquals(State, state)
            && state.CurrentRoom is MapRoom && NMapScreen.Instance is { } screen)
            OpenMap(screen, false);
        if (!alreadyTraveling) BlackFlowSpireTeam.Publish(state);
    }

    private static async Task AdvanceThroughExit()
    {
        if (IsTraveling || !CanContinueRun) return;
        var state = State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        if (BlackFlowSpireGenerationService.GetFloorTransition(state) is not { } transition
            || transition.SourceActIndex != state.CurrentActIndex || transition.NodeId != map.PositionNodeId) return;
        var manager = RunManager.Instance;
        var screen = NMapScreen.Instance!;
        try
        {
            IsTraveling = true;
            LastError = null;
            // The ordinary act transition resets exploration and uses the next
            // independent region instance; no Boss combat or Boss rewards here.
            await manager.EnterNextAct();
            if (transition.Hope > 0) await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Exit transition failed: {error}");
            if (CanContinueRun && ReferenceEquals(State, state) && state.CurrentActIndex == map.ActIndex)
            {
                // No destination reward has been granted yet. Restore the
                // choice instead of leaving a closed map or auto-retry loop.
                var progress = BlackFlowSpireGenerationService.GetProgress(state);
                progress.ActionPointsRemaining = map.GetNode(transition.NodeId).Content == BlackFlowSpireNodeContent.Final
                    ? transition.Hope : transition.ActionPointBonus;
                progress.PendingNodeId = transition.NodeId;
                map.GetNode(transition.NodeId).Completed = false;
                BlackFlowSpireGenerationService.SetFloorTransition(state, null);
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
                try { await SaveManager.Instance.SaveRun(null); }
                catch (Exception saveError) { Entry.Logger.Error($"[BlackFlowSpire] Exit recovery save failed: {saveError}"); }
            }
            if (CanContinueRun && ReferenceEquals(State, state)) await manager.FadeIn();
        }
        finally
        {
            IsTraveling = false;
            if (CanContinueRun && ReferenceEquals(State, state)
                && (state.CurrentActIndex == map.ActIndex || state.CurrentRoom is MapRoom))
                OpenMap(screen, false);
        }
    }

    public static Task ResolvePassage(bool usePassage) => SubmitTeamAction(BlackFlowSpireTeamAction.Passage,
        usePassage ? 1 : 0, () => ResolvePassageCore(usePassage));

    internal static async Task ResolvePassageCore(bool usePassage)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress) return;
        var state = State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpirePassageRules.IsPending(map, progress)) return;
        var manager = RunManager.Instance;
        var screen = NMapScreen.Instance!;
        try
        {
            IsTraveling = true;
            LastError = null;
            if (usePassage) await manager.FadeOut();
            var result = BlackFlowSpirePassageRules.Resolve(map, progress, usePassage, BlackFlowSpireGenerationService.GetEngine(state));
            BlackFlowSpireCollectibleService.QueueSurvey(state, map);
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            if (usePassage)
            {
                var node = map.GetNode(result.NodeId);
                Visited(state).Add(new MapCoord(node.X, node.Y));
                AccessTools.PropertySetter(typeof(RunState), nameof(RunState.NextRoomId)).Invoke(state, [0]);
                await manager.EnterMapPointInternal(progress.StepsTaken,
                    state.Map.GetPoint(new MapCoord(node.X, node.Y))!.PointType, null, true);
                var history = state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex);
                if (history != null) progress.HistoryIndexes[node.Id] = history.Count - 1;
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            }
            await BlackFlowSpireCollectibleService.SettleGold(state, map, replicated: BlackFlowSpireTeam.IsOnline);
            await SaveManager.Instance.SaveRun(null);
            Entry.Logger.Info($"[BlackFlowSpire] Passage resolved: teleport={usePassage}, position={result.NodeId}, AP={result.ActionPointsRemaining}");
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Passage failed: {error}");
        }
        finally
        {
            IsTraveling = false;
            await RefreshMapAfterTravel(state, map, progress, screen, focusCurrent: usePassage);
            if (usePassage && CanContinueRun && ReferenceEquals(State, state)) await manager.FadeIn();
        }
    }

    private static async Task RefreshMapAfterTravel(RunState state, BlackFlowSpireMap map,
        BlackFlowSpireExplorationProgress progress, NMapScreen screen, bool focusCurrent = false)
    {
        if (!CanContinueRun || !ReferenceEquals(State, state) || !GodotObject.IsInstanceValid(screen)
            || state.CurrentRoom is not MapRoom) return;
        if (!screen.IsOpen)
        {
            OpenMap(screen, false, focusCurrent);
            return;
        }
        if (BlackFlowSpireHuntRules.TryBegin(map, progress, BlackFlowSpireGenerationService.GetEngine(state)))
        {
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            await SaveManager.Instance.SaveRun(null);
        }
        BlackFlowSpireMapUi.Show(screen, state, false, focusCurrent: focusCurrent);
    }

    private static Task SubmitTeamAction(BlackFlowSpireTeamAction action, int argument, Func<Task> local)
    {
        if (!CanContinueRun || IsTraveling || CombatManager.Instance.IsInProgress) return Task.CompletedTask;
        if (!BlackFlowSpireTeam.IsOnline) return local();
        if (BlackFlowSpireTeam.Request(State!, action, argument)) return Task.CompletedTask;
        if (NMapScreen.Instance is { IsOpen: true } screen)
            BlackFlowSpireMapUi.Show(screen, State!, BlackFlowSpireMapUi.IsInspection(screen));
        return Task.CompletedTask;
    }
}
