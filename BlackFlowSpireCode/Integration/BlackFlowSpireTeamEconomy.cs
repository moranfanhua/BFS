using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

// Native gold commands and reward synchronization still run once for their actual owner.
// Replicas of the wallet update the vanilla HUD without running reward or spending hooks again.
[HarmonyPatch(typeof(Player), nameof(Player.Gold), MethodType.Setter)]
internal static class BlackFlowSpireTeamEconomy
{
    [ThreadStatic] private static bool _updating;

    internal static void Initialize(RunState state)
    {
        if (!BlackFlowSpireTeam.IsOnline || state.Players.Count == 0) return;
        var data = BlackFlowSpireGenerationService.TeamData(state);
        if (data.TeamGoldPooled) return;
        var balance = (int)Math.Min(int.MaxValue, state.Players.Sum(player => (long)player.Gold));
        data.TeamGoldPooled = true;
        SetBalance(state, balance);
        BlackFlowSpireGenerationService.SaveTeamChange(state);
    }

    private static void Postfix(Player __instance)
    {
        if (_updating || __instance.RunState is not RunState state || !BlackFlowSpireTeam.IsOnline
            || !BlackFlowSpireGenerationService.IsEnabled(state) || !BlackFlowSpireGenerationService.TeamData(state).TeamGoldPooled
            || !state.Players.Contains(__instance)) return;
        SetBalance(state, __instance.Gold);
    }

    private static void SetBalance(RunState state, int balance)
    {
        _updating = true;
        try
        {
            foreach (var player in state.Players)
                if (player.Gold != balance) player.Gold = balance;
        }
        finally { _updating = false; }
    }
}
