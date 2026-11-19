using BlackFlowSpire.Persistence;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
internal static class BlackFlowSpireLocalizationReadyPatch
{
    private static void Postfix()
    {
        BlackFlowSpireLocalization.AttachLocaleManager();
        Entry.Logger.Info("[BlackFlowSpire] Native localization ready; locale refresh subscribed.");
    }
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class BlackFlowSpireFixedEncounterPatch
{
    private static bool Prefix(RoomType roomType, AbstractModel? model, ref AbstractRoom __result)
    {
        if (!BlackFlowSpireRuntime.IsActive || model != null || roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss)) return true;
        var state = BlackFlowSpireRuntime.State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (progress.HuntPhase == BlackFlowSpireHuntPhase.InBattle)
        {
            __result = new CombatRoom(BlackFlowSpireEncounterFactory.Create(state, BlackFlowSpireResidentEncounterService.CreateHunt(map)), state);
            return false;
        }
        var node = map.GetNode(map.PositionNodeId);
        if (BlackFlowSpireResidentRules.IsSpecialCombat(map, node)
            && progress.PendingNodeId == node.Id)
        {
            var specialSetup = BlackFlowSpireResidentRules.CombatSetupFor(map, node)
                ?? throw new InvalidDataException("The dedicated resident encounter is not configured.");
            __result = new CombatRoom(BlackFlowSpireEncounterService.CreateEncounter(state, specialSetup), state);
            return false;
        }
        if (node.Completed || node.Kind is not (BlackFlowSpireNodeKind.Combat or BlackFlowSpireNodeKind.Boss)) return true;
        var setup = node.CombatSetup ?? throw new InvalidDataException("Missing fixed encounter for combat node.");
        __result = new CombatRoom(BlackFlowSpireEncounterService.CreateEncounter(state, setup), state);
        return false;
    }
}

[HarmonyPatch(typeof(NMainMenu), "SingleplayerButtonPressed")]
internal static class BlackFlowSpireFirstRunModeSelectionPatch
{
    // The vanilla first-run shortcut skips the page containing the new mode.
    private static bool Prefix(NMainMenu __instance)
    {
        __instance.OpenSingleplayerSubmenu();
        return false;
    }
}

[HarmonyPatch(typeof(NSingleplayerSubmenu), nameof(NSingleplayerSubmenu.OnSubmenuOpened))]
internal static class BlackFlowSpireModeEntryPatch
{
    private static void Postfix(NSingleplayerSubmenu __instance) => BlackFlowSpireModeMenu.Add(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuClosed))]
internal static class BlackFlowSpireSelectionCancelledPatch
{
    private static void Postfix() => BlackFlowSpireRuntime.PendingNewRun = false;
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class BlackFlowSpireNewRunPatch
{
    private static void Prefix(RunState state)
    {
        if (!BlackFlowSpireRuntime.PendingNewRun) return;
        BlackFlowSpireRuntime.PendingNewRun = false;
        BlackFlowSpireCampaignService.ConfigureNewRun(state);
        BlackFlowSpireGenerationService.Enable(state);
        state.ExtraFields.StartedWithNeow = false;
        Entry.Logger.Info("[BlackFlowSpire] Starting BlackFlowSpire run from the Play mode screen.");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
internal static class BlackFlowSpireGenerateMapPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (!BlackFlowSpireGenerationService.IsEnabled(__instance.DebugOnlyGetState())) return true;
        __result = BlackFlowSpireRuntime.GenerateMap(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class BlackFlowSpireMapOpenPatch
{
    private static bool Prefix(NMapScreen __instance, bool isOpenedFromTopBar, ref NMapScreen __result)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        BlackFlowSpireRuntime.OpenMap(__instance, isOpenedFromTopBar);
        __result = __instance;
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
internal static class BlackFlowSpireMapClosePatch
{
    private static bool Prefix(NMapScreen __instance, bool animateOut)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        BlackFlowSpireRuntime.CloseMap(__instance, animateOut);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetTravelEnabled))]
internal static class BlackFlowSpireMapTravelEnabledPatch
{
    private static bool Prefix(NMapScreen __instance, bool enabled)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        BlackFlowSpireRuntime.SetMapFlag(__instance, "IsTravelEnabled", enabled);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), "get_DefaultFocusedControl")]
internal static class BlackFlowSpireMapFocusPatch
{
    private static bool Prefix(ref Control? __result)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        __result = BlackFlowSpireMapUi.DefaultFocus;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class BlackFlowSpireFixedRoomPatch
{
    private static bool Prefix(ref RoomType __result)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        __result = BlackFlowSpireRuntime.CurrentRoomType(BlackFlowSpireRuntime.State!);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "EnterMapCoordInternal")]
internal static class BlackFlowSpireRestorePositionPatch
{
    private static bool Prefix(RunManager __instance, MapCoord coord, AbstractRoom? preFinishedRoom, bool saveGame, ref Task __result)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        var state = BlackFlowSpireRuntime.State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var current = map.GetNode(map.PositionNodeId);
        if (progress.HuntPhase == BlackFlowSpireHuntPhase.InBattle) preFinishedRoom = null;
        else if (progress.HuntPhase == BlackFlowSpireHuntPhase.Won)
        {
            if (preFinishedRoom is not CombatRoom { IsPreFinished: true } hunt
                || BlackFlowSpireEncounterFactory.SpecialKind(hunt.Encounter) != BlackFlowSpireSpecialEncounter.Hunt)
            {
                hunt = new CombatRoom(BlackFlowSpireEncounterFactory.Create(null, BlackFlowSpireResidentEncounterService.CreateHunt(map)), state);
                hunt.MarkPreFinished();
                preFinishedRoom = hunt;
            }
        }
        else if (current.Completed || current.Content == BlackFlowSpireNodeContent.Door
            || BlackFlowSpireWishRules.IsPending(map, progress) || BlackFlowSpireExpeditionRules.IsPending(map, progress))
        {
            // Finished rooms and passage choices restore directly to the map. Reuse existing
            // history; a checkpoint made before room entry still needs its missing entry.
            var historyCount = state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex)?.Count ?? 0;
            preFinishedRoom = historyCount >= progress.StepsTaken ? new MapRoom() : null;
        }
        __result = BlackFlowSpireRuntime.RestoreMapPoint(__instance, state, map, progress, coord, preFinishedRoom, saveGame);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreenAsync))]
internal static class BlackFlowSpireHuntProceedPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State?.CurrentRoom is not CombatRoom { IsPreFinished: true } room
            || BlackFlowSpireEncounterFactory.SpecialKind(room.Encounter) != BlackFlowSpireSpecialEncounter.Hunt) return true;
        __instance.ActChangeSynchronizer.SetLocalPlayerReady();
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.GetHistoryEntryFor))]
internal static class BlackFlowSpireHistoryPatch
{
    private static bool Prefix(RunState __instance, MapLocation location, ref MapPointHistoryEntry? __result)
    {
        if (!BlackFlowSpireGenerationService.IsEnabled(__instance) || location.actIndex != __instance.CurrentActIndex || !location.coord.HasValue) return true;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(__instance);
        var id = location.coord.Value.row * map.Options.Width + location.coord.Value.col;
        if (id == map.PositionNodeId) __result = __instance.CurrentMapPointHistoryEntry;
        else
        {
            var progress = BlackFlowSpireGenerationService.GetProgress(__instance);
            var history = __instance.MapPointHistory.ElementAtOrDefault(location.actIndex);
            __result = progress.HistoryIndexes.TryGetValue(id, out var index) && history != null && index < history.Count ? history[index] : null;
        }
        return false;
    }
}
