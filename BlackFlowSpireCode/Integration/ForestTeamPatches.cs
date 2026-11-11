using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
internal static class ForestTeamLobbyPatch
{
    private const string ToggleName = "BlackFlowSpireTeamToggle";

    private static void Postfix(NCharacterSelectScreen __instance)
    {
        if (__instance.GetNodeOrNull<CheckButton>(ToggleName) is { } old)
        {
            __instance.RemoveChild(old);
            old.QueueFree();
        }
        if (__instance.Lobby is not { NetService.Type: NetGameType.Host } lobby) return;
        var toggle = new CheckButton
        {
            Name = ToggleName, Text = ForestText.Get("UI.TEAM.lobby"),
            ButtonPressed = ForestGenerationService.IsTeamLobbyEnabled(lobby)
        };
        toggle.AddThemeFontOverride("font", ForestFonts.Body);
        __instance.AddChild(toggle);
        toggle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        toggle.OffsetLeft = 52;
        toggle.OffsetRight = 360;
        toggle.OffsetTop = -177;
        toggle.OffsetBottom = -133;
        toggle.Toggled += enabled => ForestGenerationService.SetTeamLobby(lobby, enabled);
        ForestLocalization.RefreshWithLocale(toggle, () => toggle.Text = ForestText.Get("UI.TEAM.lobby"));
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class ForestTeamRunPatch
{
    private static void Postfix(RunState __result) => ForestTeam.AttachLobby(__result);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class ForestTeamCleanupPatch
{
    private static void Prefix(RunManager __instance)
    {
        if (__instance.DebugOnlyGetState() is { } state && ForestGenerationService.IsEnabled(state))
            ForestTeam.EndSession(state);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
internal static class ForestTeamCombatArrivalPatch
{
    private static void Postfix(IRunState runState, AbstractRoom room, ref Task __result)
    {
        if (room is CombatRoom && runState is RunState state && ForestTeam.IsOnline && ForestGenerationService.IsEnabled(state))
            __result = SettleBeforeCombat(__result, state);
    }

    private static async Task SettleBeforeCombat(Task original, RunState state)
    {
        await original;
        // Native player and RNG synchronization is complete, and the combat's
        // start hooks have not run yet. Both peers can grant the same relics here.
        var progress = ForestGenerationService.GetProgress(state);
        if (progress.PendingPartReward != null || ForestGenerationService.GetEngine(state).PendingConceptRewards.Count > 0)
            await ForestPartService.SettleMovementRewards(state, ForestGenerationService.GetOrGenerate(state), progress);
    }
}
