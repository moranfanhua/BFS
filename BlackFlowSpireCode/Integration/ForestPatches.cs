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
internal static class ForestLocalizationReadyPatch
{
    private static void Postfix()
    {
        ForestLocalization.AttachLocaleManager();
        Entry.Logger.Info("[Forest] Native localization ready; locale refresh subscribed.");
    }
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class ForestFixedEncounterPatch
{
    private static bool Prefix(RoomType roomType, AbstractModel? model, ref AbstractRoom __result)
    {
        if (!ForestRuntime.IsActive || model != null || roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss)) return true;
        var state = ForestRuntime.State!;
        var map = ForestGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        if (ForestResidentRules.IsSpecialCombat(map, node)
            && ForestGenerationService.GetProgress(state).PendingNodeId == node.Id)
        {
            var specialSetup = ForestResidentRules.CombatSetupFor(map, node)
                ?? throw new InvalidDataException("The dedicated resident encounter is not configured.");
            __result = new CombatRoom(ForestEncounterService.CreateEncounter(state, specialSetup), state);
            return false;
        }
        if (node.Completed || node.Kind is not (ForestNodeKind.Combat or ForestNodeKind.Boss)) return true;
        var setup = node.CombatSetup ?? throw new InvalidDataException("Missing fixed encounter for combat node.");
        __result = new CombatRoom(ForestEncounterService.CreateEncounter(state, setup), state);
        return false;
    }
}

[HarmonyPatch(typeof(NMainMenu), "SingleplayerButtonPressed")]
internal static class ForestFirstRunModeSelectionPatch
{
    // The vanilla first-run shortcut skips the page containing the new mode.
    private static bool Prefix(NMainMenu __instance)
    {
        __instance.OpenSingleplayerSubmenu();
        return false;
    }
}

[HarmonyPatch(typeof(NSingleplayerSubmenu), nameof(NSingleplayerSubmenu.OnSubmenuOpened))]
internal static class ForestModeEntryPatch
{
    private static void Postfix(NSingleplayerSubmenu __instance) => ForestModeMenu.Add(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuClosed))]
internal static class ForestSelectionCancelledPatch
{
    private static void Postfix() => ForestRuntime.PendingNewRun = false;
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class ForestNewRunPatch
{
    private static void Prefix(RunState state)
    {
        if (!ForestRuntime.PendingNewRun) return;
        ForestRuntime.PendingNewRun = false;
        ForestCampaignService.ConfigureNewRun(state);
        ForestGenerationService.Enable(state);
        state.ExtraFields.StartedWithNeow = false;
        Entry.Logger.Info("[Forest] Starting BlackFlowSpire run from the Play mode screen.");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateMap))]
internal static class ForestGenerateMapPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (!ForestGenerationService.IsEnabled(__instance.DebugOnlyGetState())) return true;
        __result = ForestRuntime.GenerateMap(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class ForestMapOpenPatch
{
    private static bool Prefix(NMapScreen __instance, bool isOpenedFromTopBar, ref NMapScreen __result)
    {
        if (!ForestRuntime.IsActive) return true;
        ForestRuntime.OpenMap(__instance, isOpenedFromTopBar);
        __result = __instance;
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
internal static class ForestMapClosePatch
{
    private static bool Prefix(NMapScreen __instance, bool animateOut)
    {
        if (!ForestRuntime.IsActive) return true;
        ForestRuntime.CloseMap(__instance, animateOut);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetTravelEnabled))]
internal static class ForestMapTravelEnabledPatch
{
    private static bool Prefix(NMapScreen __instance, bool enabled)
    {
        if (!ForestRuntime.IsActive) return true;
        ForestRuntime.SetMapFlag(__instance, "IsTravelEnabled", enabled);
        return false;
    }
}

[HarmonyPatch(typeof(NMapScreen), "get_DefaultFocusedControl")]
internal static class ForestMapFocusPatch
{
    private static bool Prefix(ref Control? __result)
    {
        if (!ForestRuntime.IsActive) return true;
        __result = ForestMapUi.DefaultFocus;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class ForestFixedRoomPatch
{
    private static bool Prefix(ref RoomType __result)
    {
        if (!ForestRuntime.IsActive) return true;
        __result = ForestRuntime.CurrentRoomType(ForestRuntime.State!);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "EnterMapCoordInternal")]
internal static class ForestRestorePositionPatch
{
    private static bool Prefix(RunManager __instance, MapCoord coord, AbstractRoom? preFinishedRoom, bool saveGame, ref Task __result)
    {
        if (!ForestRuntime.IsActive) return true;
        var state = ForestRuntime.State!;
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        var current = map.GetNode(map.PositionNodeId);
        if (current.Completed || current.Content == ForestNodeContent.Door)
        {
            // Finished rooms and passage choices restore directly to the map. Reuse existing
            // history; a checkpoint made before room entry still needs its missing entry.
            var historyCount = state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex)?.Count ?? 0;
            preFinishedRoom = historyCount >= progress.StepsTaken ? new MapRoom() : null;
        }
        __result = ForestRuntime.RestoreMapPoint(__instance, state, map, progress, coord, preFinishedRoom, saveGame);
        return false;
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.GetHistoryEntryFor))]
internal static class ForestHistoryPatch
{
    private static bool Prefix(RunState __instance, MapLocation location, ref MapPointHistoryEntry? __result)
    {
        if (!ForestGenerationService.IsEnabled(__instance) || location.actIndex != __instance.CurrentActIndex || !location.coord.HasValue) return true;
        var map = ForestGenerationService.GetOrGenerate(__instance);
        var id = location.coord.Value.row * map.Options.Width + location.coord.Value.col;
        if (id == map.PositionNodeId) __result = __instance.CurrentMapPointHistoryEntry;
        else
        {
            var progress = ForestGenerationService.GetProgress(__instance);
            var history = __instance.MapPointHistory.ElementAtOrDefault(location.actIndex);
            __result = progress.HistoryIndexes.TryGetValue(id, out var index) && history != null && index < history.Count ? history[index] : null;
        }
        return false;
    }
}
