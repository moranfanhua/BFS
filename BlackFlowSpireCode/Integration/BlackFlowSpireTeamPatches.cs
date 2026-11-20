using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
internal static class BlackFlowSpireTeamLobbyPatch
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
            Name = ToggleName, Text = BlackFlowSpireText.Get("UI.TEAM.lobby"),
            ButtonPressed = BlackFlowSpireGenerationService.IsTeamLobbyEnabled(lobby)
        };
        toggle.AddThemeFontOverride("font", BlackFlowSpireFonts.Body);
        __instance.AddChild(toggle);
        toggle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        toggle.OffsetLeft = 52;
        toggle.OffsetRight = 360;
        toggle.OffsetTop = -177;
        toggle.OffsetBottom = -133;
        toggle.Toggled += enabled => BlackFlowSpireGenerationService.SetTeamLobby(lobby, enabled);
        BlackFlowSpireLocalization.RefreshWithLocale(toggle, () => toggle.Text = BlackFlowSpireText.Get("UI.TEAM.lobby"));
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class BlackFlowSpireTeamRunPatch
{
    private static void Postfix(RunState __result) => BlackFlowSpireTeam.AttachLobby(__result);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class BlackFlowSpireTeamCleanupPatch
{
    private static void Prefix(RunManager __instance)
    {
        if (__instance.DebugOnlyGetState() is { } state && BlackFlowSpireGenerationService.IsEnabled(state))
            BlackFlowSpireTeam.EndSession(state);
    }
}

[HarmonyPatch(typeof(RunLobby), "HandleClientRejoinRequestMessage")]
internal static class BlackFlowSpireTeamRejoinPatch
{
    private static bool Prefix(ulong senderId)
    {
        if (RunManager.Instance.NetService is not INetHostGameService host || BlackFlowSpireRuntime.State is not { } state
            || !BlackFlowSpireGenerationService.IsEnabled(state)
            || BlackFlowSpireTeam.CanManage(state) && !RunManager.Instance.IsChangingRooms && !BlackFlowSpireRuntime.IsTraveling) return true;
        host.DisconnectClient(senderId, NetError.TryAgainLater);
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
internal static class BlackFlowSpireCombatArrivalRewardPatch
{
    private static void Postfix(IRunState runState, AbstractRoom room, ref Task __result)
    {
        if (room is not CombatRoom || runState is not RunState state || !BlackFlowSpireGenerationService.IsEnabled(state)) return;
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        if (BlackFlowSpireTeam.IsOnline || engine.Collectibles.PendingGold > 0
            || engine.PendingConceptRewards.Any(reward => reward.Shield > 0 || reward.CollectibleIds.Count > 0))
            __result = HarmonyAsyncTaskBridge.After(__result, () => SettleBeforeCombat(state));
    }

    private static async Task SettleBeforeCombat(RunState state)
    {
        if (BlackFlowSpireTeam.IsGuest && BlackFlowSpireTeam.InCommand(state))
        {
            await BlackFlowSpireTeam.ReceiveExpected(state, ForestBoundary.CombatArrival);
            return;
        }
        // Native player and RNG synchronization is complete. Settle shared rewards
        // and apply any movement shield before combat starts.
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        if (progress.PendingPartReward != null || engine.PendingConceptRewards.Count > 0 || engine.Collectibles.PendingGold > 0)
            await BlackFlowSpirePartService.SettleMovementRewards(state, BlackFlowSpireGenerationService.GetOrGenerate(state), progress);
        BlackFlowSpireTeam.Emit(state, ForestBoundary.CombatArrival);
    }
}
