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
using STS2RitsuLib.Utils.HarmonyIl;

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
internal static class ForestCombatArrivalRewardPatch
{
    private static void Postfix(IRunState runState, AbstractRoom room, ref Task __result)
    {
        if (room is not CombatRoom || runState is not RunState state || !ForestGenerationService.IsEnabled(state)) return;
        var engine = ForestGenerationService.GetEngine(state);
        if (ForestTeam.IsOnline || engine.Collectibles.PendingGold > 0
            || engine.PendingConceptRewards.Any(reward => reward.Shield > 0 || reward.CollectibleIds.Count > 0))
            __result = HarmonyAsyncTaskBridge.After(__result, () => SettleBeforeCombat(state));
    }

    private static Task SettleBeforeCombat(RunState state)
    {
        // Native player and RNG synchronization is complete. Settle shared rewards
        // and apply any movement shield before combat starts.
        var progress = ForestGenerationService.GetProgress(state);
        var engine = ForestGenerationService.GetEngine(state);
        return progress.PendingPartReward != null || engine.PendingConceptRewards.Count > 0 || engine.Collectibles.PendingGold > 0
            ? ForestPartService.SettleMovementRewards(state, ForestGenerationService.GetOrGenerate(state), progress)
            : Task.CompletedTask;
    }
}
