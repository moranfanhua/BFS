using System.Reflection;
using System.Runtime.CompilerServices;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
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

    public static void Begin(RunState run, CombatState combat)
    {
        foreach (var player in run.Players)
        {
            var creature = player.Creature;
            creature.HealInternal(creature.MaxHp - creature.CurrentHp);
        }
        Battles.Remove(combat);
        Battles.Add(combat, new Battle());
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
        var loveGrassCount = ForestGenerationService.GetEngine(run).NaturalParts.Count(part => part.Kind == ForestNaturalPartKind.LoveGrass);
        var lifeLost = battle.Health.GetTargetLifeLoss(maxHp, loveGrassCount);
        ForestGenerationService.SettleCombatHealth(run, lifeLost);
        battle.Settled = true;
        battle.TargetDefeat = won && ForestGenerationService.GetTargetLife(run) == 0;
        return battle.TargetDefeat;
    }

    public static bool IsTargetDefeat(CombatState combat) =>
        Battles.TryGetValue(combat, out var battle) && battle.TargetDefeat;
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.EnterInternal))]
internal static class ForestCombatHealthEntryPatch
{
    private static void Prefix(CombatRoom __instance, IRunState? runState, bool isRestoringRoomStackBase)
    {
        if (!isRestoringRoomStackBase && !__instance.IsPreFinished
            && runState is RunState run && ForestGenerationService.IsEnabled(run))
            ForestCombatHealthRuntime.Begin(run, __instance.CombatState);
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
        __state = CombatManager.Instance.IsAboutToLose
            && ForestCombatHealthRuntime.IsTargetDefeat(ForestNativeTurnAccess.State(__0));
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
            __result = ForestGenerationService.GetTargetLife(__instance) <= 0;
    }
}
