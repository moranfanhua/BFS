using System.Reflection;
using System.Runtime.CompilerServices;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace BlackFlowSpire.Integration;

internal static class ForestCombatHealthRuntime
{
    private sealed class Battle
    {
        public ForestCombatHealth Health { get; } = new();
        public bool Settled { get; set; }
        public bool TargetDefeat { get; set; }
    }

    private static readonly ConditionalWeakTable<CombatState, Battle> Battles = new();
    private sealed record PlayerLife(CombatState Combat, ForestPlayerCombatLife Life);
    private static readonly ConditionalWeakTable<Creature, PlayerLife> PlayerLives = new();

    public static void Begin(RunState run, CombatState combat)
    {
        Battles.Remove(combat);
        Battles.Add(combat, new Battle());
        try
        {
            foreach (var player in run.Players)
            {
                var creature = player.Creature;
                RestorePlayerLife(creature);
                var multiplier = ForestGenerationService.GetPlayerLifeMultiplier(run, player.NetId);
                if (multiplier != 1m)
                {
                    var life = new ForestPlayerCombatLife(creature.MaxHp, multiplier);
                    creature.SetMaxHpInternal(life.CombatMaximum);
                    PlayerLives.Add(creature, new(combat, life));
                }
                creature.HealInternal(creature.MaxHp - creature.CurrentHp);
            }
        }
        catch
        {
            RestorePlayerLives(combat);
            throw;
        }
    }

    internal static int? GetBaseMaximum(Creature creature) =>
        PlayerLives.TryGetValue(creature, out var entry) ? entry.Life.BaseMaximum : null;

    internal static void AdjustPlayerMaximum(Creature creature, ref decimal amount)
    {
        if (amount >= 0m && PlayerLives.TryGetValue(creature, out var entry))
            amount = entry.Life.AdjustBaseMaximum(creature.MaxHp, amount);
    }

    internal static void RestorePlayerLives(CombatState combat)
    {
        if (!Battles.TryGetValue(combat, out _)) return;
        foreach (var player in combat.RunState.Players)
        {
            var creature = player.Creature;
            if (PlayerLives.TryGetValue(creature, out var entry) && ReferenceEquals(entry.Combat, combat))
                RestorePlayerLife(creature);
        }
    }

    private static void RestorePlayerLife(Creature creature)
    {
        if (!PlayerLives.TryGetValue(creature, out var entry)) return;
        PlayerLives.Remove(creature);
        creature.SetMaxHpInternal(entry.Life.BaseMaximum);
    }

    public static void RecordHpChange(Creature creature, int previousHp)
    {
        var hpLost = previousHp - creature.CurrentHp;
        if (hpLost <= 0 || !creature.IsPlayer || creature.CombatState is not CombatState combat
            || !Battles.TryGetValue(combat, out var battle) || battle.Settled) return;
        var manager = CombatManager.Instance;
        if (manager.IsInProgress && ReferenceEquals(manager.DebugOnlyGetState(), combat))
            battle.Health.RecordLoss(hpLost);
    }

    public static bool Settle(CombatState? combat, bool won)
    {
        if (combat?.RunState is not RunState run || !Battles.TryGetValue(combat, out var battle)) return false;
        if (battle.Settled) return battle.TargetDefeat;

        var maxHp = combat.Players.Sum(player => (long)player.Creature.MaxHp);
        var lifeRemaining = ForestGenerationService.SettleCombatHealth(run, battle.Health, maxHp);
        battle.Settled = true;
        battle.TargetDefeat = won && lifeRemaining == 0;
        return battle.TargetDefeat;
    }

    public static bool IsTargetDefeat(CombatState combat) =>
        Battles.TryGetValue(combat, out var battle) && battle.TargetDefeat;
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class ForestPlayerCombatMaximumPatch
{
    private static void Prefix(Creature __instance, ref decimal amount) =>
        ForestCombatHealthRuntime.AdjustPlayerMaximum(__instance, ref amount);
}

[HarmonyPatch(typeof(Player), nameof(Player.ToSerializable))]
internal static class ForestPlayerCombatLifeSavePatch
{
    private static void Postfix(Player __instance, SerializablePlayer __result)
    {
        if (ForestCombatHealthRuntime.GetBaseMaximum(__instance.Creature) is not { } maximum) return;
        __result.MaxHp = maximum;
        __result.CurrentHp = Math.Min(__result.CurrentHp, maximum);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class ForestPlayerCombatLifeRestorePatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (runState is RunState run && ForestGenerationService.IsEnabled(run) && combatState is CombatState combat)
            __result = RestoreAfterCombat(__result, combat);
    }

    private static async Task RestoreAfterCombat(Task original, CombatState combat)
    {
        try { await original; }
        finally { ForestCombatHealthRuntime.RestorePlayerLives(combat); }
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class ForestPlayerCombatLifeResetPatch
{
    private static void Prefix(CombatManager __instance)
    {
        if (__instance.DebugOnlyGetState() is { } combat)
            ForestCombatHealthRuntime.RestorePlayerLives(combat);
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.EnterInternal))]
internal static class ForestCombatHealthEntryPatch
{
    private static void Prefix(CombatRoom __instance, IRunState? runState, bool isRestoringRoomStackBase, out bool __state)
    {
        __state = false;
        if (!isRestoringRoomStackBase && !__instance.IsPreFinished
            && runState is RunState run && ForestGenerationService.IsEnabled(run))
        {
            ForestCombatHealthRuntime.Begin(run, __instance.CombatState);
            __state = true;
        }
    }

    private static void Postfix(CombatRoom __instance, bool __state, ref Task __result)
    {
        if (__state) __result = RestoreIfEntryFails(__result, __instance.CombatState);
    }

    private static async Task RestoreIfEntryFails(Task original, CombatState combat)
    {
        try { await original; }
        catch
        {
            ForestCombatHealthRuntime.RestorePlayerLives(combat);
            throw;
        }
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.CurrentHp), MethodType.Setter)]
internal static class ForestCombatHpChangePatch
{
    private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;

    private static void Postfix(Creature __instance, int __state) =>
        ForestCombatHealthRuntime.RecordHpChange(__instance, __state);
}

[HarmonyPatch]
internal static class ForestCombatHealthVictorySettlementPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CombatManager))
        .Single(method => method.Name == "EndCombatInternal" && method.GetParameters().Length == 1);

    private static bool Prefix(CombatManager __instance, object __0, ref Task __result)
    {
        var combat = ForestNativeTurnAccess.State(__0);
        if (!ForestCombatHealthRuntime.Settle(combat, won: true)) return true;

        // The game has determined the fight was won. Apply the run-wide result before rewards and saving.
        __result = Task.CompletedTask;
        if (__instance.IsInProgress && ReferenceEquals(__instance.DebugOnlyGetState(), combat))
        {
            __instance.LoseCombat();
            __result = __instance.CheckWinCondition();
        }
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
internal static class ForestCombatHealthLossSettlementPatch
{
    private static void Postfix(CombatManager __instance)
    {
        if (__instance.IsInProgress)
            ForestCombatHealthRuntime.Settle(__instance.DebugOnlyGetState(), won: false);
    }
}

[HarmonyPatch(typeof(CombatManager), "ProcessPendingLoss")]
internal static class ForestCombatHealthDefeatPatch
{
    private static void Prefix(object __0, out bool __state)
    {
        __state = false;
        if (!CombatManager.Instance.IsAboutToLose) return;
        var combat = ForestNativeTurnAccess.State(__0);
        __state = ForestCombatHealthRuntime.IsTargetDefeat(combat);
        ForestCombatHealthRuntime.RestorePlayerLives(combat);
    }

    private static void Postfix(bool __state)
    {
        if (!__state || TestMode.IsOn || NRun.Instance is not { } run) return;
        run.RunMusicController.StopMusic();
        NDebugAudioManager.Instance?.StopAll();
        NAudioManager.Instance?.PlayMusic("event:/temp/sfx/game_over");
        run.ShowGameOverScreen(RunManager.Instance.OnEnded(isVictory: false));
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.IsGameOver), MethodType.Getter)]
internal static class ForestTargetLifeGameOverPatch
{
    private static void Postfix(RunState __instance, ref bool __result)
    {
        if (!__result && ForestGenerationService.IsEnabled(__instance))
            __result = ForestGenerationService.GetTargetLife(__instance).Current <= 0;
    }
}
