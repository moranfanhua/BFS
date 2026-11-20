using System.Reflection;
using System.Runtime.CompilerServices;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

internal static class BlackFlowSpireCombatHealthRuntime
{
    internal readonly record struct Settlement(int Damage, int Experience, int Hope);

    private sealed class Battle(decimal playerAttack, decimal allyLife, BlackFlowSpireCollectibleInventory collectibles)
    {
        public BlackFlowSpireCombatHealth Health { get; } = new();
        public decimal PlayerAttack { get; } = playerAttack;
        public decimal AllyLife { get; } = allyLife;
        public HashSet<Creature> AdjustedCreatures { get; } = [];
        public Dictionary<Creature, int>? UnhurtTurns { get; } = collectibles.Has(120) ? [] : null;
        public HashSet<Creature>? PendingKnockdowns { get; } = collectibles.Has(244) ? [] : null;
        public bool LifeRestored { get; set; }
        public bool Settled { get; set; }
        public int Damage { get; set; }
        public Settlement? Result { get; set; }
        public bool SpecialVictoryRecorded { get; set; }
        public bool TargetDefeat { get; set; }
    }

    private static readonly ConditionalWeakTable<CombatState, Battle> Battles = new();
    private static readonly AttachedState<CombatRoom, Settlement?> RestoredResults = new();
    private sealed record TrackedLife(CombatState Combat, BlackFlowSpirePlayerCombatLife Life);
    private static readonly ConditionalWeakTable<Creature, TrackedLife> CreatureLives = new();

    public static void Begin(RunState run, CombatState combat)
    {
        var inventory = BlackFlowSpireGenerationService.GetEngine(run);
        var appraisal = inventory.Collectibles.Has(216) ? BlackFlowSpirePartService.TotalAppraisal(inventory) : 0;
        var (playerLife, attackMultiplier) = BlackFlowSpireCollectibleRules.PlayerModifiers(
            inventory.Collectibles, inventory.TotalPartCount, appraisal);
        var allyLifeBonus = BlackFlowSpireCollectibleRules.AllyLifeBonus(inventory.Collectibles,
            BlackFlowSpireGenerationService.GetTargetLife(run).Shield > 0);
        var lifeMultiplier = playerLife + allyLifeBonus;
        RestoreCombatLife(combat);
        Battles.Remove(combat);
        var battle = new Battle(attackMultiplier, 1m + allyLifeBonus, inventory.Collectibles);
        Battles.Add(combat, battle);
        try
        {
            foreach (var player in run.Players)
            {
                var creature = player.Creature;
                RestoreCreatureLife(creature);
                var multiplier = BlackFlowSpireGenerationService.GetPlayerLifeMultiplier(run, player.NetId);
                if (multiplier != 1m || lifeMultiplier != 1m)
                {
                    var life = new BlackFlowSpirePlayerCombatLife(creature.MaxHp, multiplier, lifeMultiplier);
                    creature.SetMaxHpInternal(life.CombatMaximum);
                    TrackLife(creature, combat, battle, life);
                }
                creature.HealInternal(creature.MaxHp - creature.CurrentHp);
            }
        }
        catch
        {
            RestoreCombatLife(combat);
            throw;
        }
    }

    internal static void ApplySummonLife(Creature creature, CombatState combat)
    {
        if (!Battles.TryGetValue(combat, out var battle) || battle.LifeRestored || battle.AllyLife == 1m
            || CreatureLives.TryGetValue(creature, out _)) return;

        var life = new BlackFlowSpirePlayerCombatLife(creature.MaxHp, battle.AllyLife);
        var wasAlive = creature.IsAlive;
        creature.SetMaxHpInternal(life.CombatMaximum);
        TrackLife(creature, combat, battle, life);
        if (wasAlive) creature.HealInternal(creature.MaxHp - life.BaseMaximum);
    }

    internal static async Task<decimal> SetOstySummonMaximum(Creature osty, decimal amount)
    {
        if (osty.CombatState is not CombatState combat || !Battles.TryGetValue(combat, out var battle)
            || battle.LifeRestored || battle.AllyLife == 1m) return await CreatureCmd.SetMaxHp(osty, amount);

        var life = new BlackFlowSpirePlayerCombatLife((int)Math.Clamp(amount, 0m, BlackFlowSpirePlayerCombatLife.MaxSupportedLife), battle.AllyLife);
        CreatureLives.Remove(osty);
        var change = await CreatureCmd.SetMaxHp(osty, life.CombatMaximum);
        if (!battle.LifeRestored) TrackLife(osty, combat, battle, life);
        return change;
    }

    internal static Task HealOstyOnSummon(Creature osty, decimal amount, bool playAnim) =>
        CreatureCmd.Heal(osty, CreatureLives.TryGetValue(osty, out _) ? osty.MaxHp : amount, playAnim);

    private static void TrackLife(Creature creature, CombatState combat, Battle battle, BlackFlowSpirePlayerCombatLife life)
    {
        CreatureLives.Add(creature, new(combat, life));
        battle.AdjustedCreatures.Add(creature);
    }

    internal static decimal PlayerAttack(ICombatState combat) =>
        combat is CombatState state && Battles.TryGetValue(state, out var battle) ? battle.PlayerAttack : 1m;

    internal static int? GetBaseMaximum(Creature creature) =>
        CreatureLives.TryGetValue(creature, out var entry) ? entry.Life.BaseMaximum : null;

    internal static void AdjustMaximum(Creature creature, ref decimal amount)
    {
        if (amount >= 0m && CreatureLives.TryGetValue(creature, out var entry))
            amount = entry.Life.AdjustBaseMaximum(creature.MaxHp, amount);
    }

    internal static void RestoreCombatLife(CombatState combat)
    {
        if (!Battles.TryGetValue(combat, out var battle)) return;
        battle.LifeRestored = true;
        foreach (var creature in battle.AdjustedCreatures)
            if (CreatureLives.TryGetValue(creature, out var entry) && ReferenceEquals(entry.Combat, combat))
                RestoreCreatureLife(creature);
        battle.AdjustedCreatures.Clear();
    }

    private static void RestoreCreatureLife(Creature creature)
    {
        if (!CreatureLives.TryGetValue(creature, out var entry)) return;
        CreatureLives.Remove(creature);
        creature.SetMaxHpInternal(entry.Life.BaseMaximum);
    }

    public static void RecordHpChange(Creature creature, int previousHp)
    {
        var hpLost = previousHp - creature.CurrentHp;
        if (creature.Side != CombatSide.Player || creature.CombatState is not CombatState combat
            || !Battles.TryGetValue(combat, out var battle) || battle.Settled) return;
        if (hpLost <= 0)
        {
            if (previousHp == 0 && creature.IsAlive) battle.PendingKnockdowns?.Remove(creature);
            return;
        }
        var manager = CombatManager.Instance;
        if (manager.IsInProgress && ReferenceEquals(manager.DebugOnlyGetState(), combat))
        {
            battle.UnhurtTurns?.Remove(creature);
            if (creature.IsPlayer)
            {
                battle.Health.RecordLoss(hpLost);
                if (creature.IsDead) battle.PendingKnockdowns?.Add(creature);
            }
        }
    }

    internal static bool TakeKnockdown(CombatState combat, Creature creature) =>
        Battles.TryGetValue(combat, out var battle) && !battle.Settled && battle.PendingKnockdowns?.Remove(creature) == true;

    internal static async Task GrantUnhurtBlock(CombatState combat)
    {
        if (!Battles.TryGetValue(combat, out var battle) || battle.Settled || battle.LifeRestored
            || battle.UnhurtTurns is not { } turns) return;

        var recipients = new List<Creature>();
        foreach (var ally in combat.Allies)
        {
            if (ally.IsDead)
            {
                turns.Remove(ally);
                continue;
            }

            // A missing entry starts a new streak at this turn boundary.
            // A recorded entry completes one interval since the previous allied turn start.
            var completed = turns.TryGetValue(ally, out var previous) ? previous + 1 : 0;
            if (completed == 3)
            {
                recipients.Add(ally);
                completed = 0;
            }
            turns[ally] = completed;
        }

        foreach (var ally in recipients)
            await CreatureCmd.GainBlock(ally, ally.MaxHp * 0.2m, ValueProp.Unpowered, null);
    }

    public static bool Settle(CombatState? combat, bool won)
    {
        if (combat?.RunState is not RunState run || !Battles.TryGetValue(combat, out var battle)) return false;
        if (battle.Settled) return battle.TargetDefeat;

        var maxHp = combat.Players.Sum(player => (long)player.Creature.MaxHp);
        var (life, _, shield) = BlackFlowSpireGenerationService.GetTargetLife(run);
        var lifeRemaining = BlackFlowSpireGenerationService.SettleCombatHealth(run, battle.Health, maxHp);
        var shieldUsed = shield - BlackFlowSpireGenerationService.GetTargetLife(run).Shield;
        battle.Damage = life - lifeRemaining + shieldUsed;
        battle.Settled = true;
        battle.TargetDefeat = won && lifeRemaining == 0;
        return battle.TargetDefeat;
    }

    public static bool IsTargetDefeat(CombatState combat) =>
        Battles.TryGetValue(combat, out var battle) && battle.TargetDefeat;

    internal static void RecordCommandExperience(RunState run, CombatRoom room)
    {
        if (!Battles.TryGetValue(room.CombatState, out var battle) || battle.Result.HasValue || battle.TargetDefeat)
            return;
        var experience = BlackFlowSpireEncounterFactory.TryGetFixedSetup(room.Encounter, out var setup)
            ? BlackFlowSpireBattleLoot.For(BlackFlowSpireGenerationService.GetOrGenerate(run), setup,
                run.CurrentActIndex == run.Acts.Count - 1).Experience
            : BlackFlowSpireBattleLoot.BattleExperience(run.CurrentActIndex, room.RoomType switch
            {
                RoomType.Boss => BlackFlowSpireBattleVariant.Boss,
                RoomType.Elite => BlackFlowSpireBattleVariant.Emergency,
                _ => BlackFlowSpireBattleVariant.Normal
            });
        var reward = BlackFlowSpireGenerationService.GrantCommandExperience(run, experience);
        battle.Result = new(battle.Damage, reward.Experience, reward.Hope);
    }

    internal static Settlement? GetSettlement(CombatRoom room) => Battles.TryGetValue(room.CombatState, out var battle)
        ? battle.Result : RestoredResults.GetValueOrDefault(room);

    internal static void RestoreSettlement(CombatRoom room, Settlement value) => RestoredResults.Add(room, value);

    internal static void RecordSpecialVictory(RunState run, CombatRoom room)
    {
        var kind = BlackFlowSpireEncounterFactory.SpecialKind(room.Encounter);
        if (kind == BlackFlowSpireSpecialEncounter.None || !Battles.TryGetValue(room.CombatState, out var battle)
            || battle.SpecialVictoryRecorded || battle.TargetDefeat) return;

        BlackFlowSpireGenerationService.RecordSpecialVictory(run, kind);
        battle.SpecialVictoryRecorded = true;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class BlackFlowSpireCombatMaximumPatch
{
    private static void Prefix(Creature __instance, ref decimal amount) =>
        BlackFlowSpireCombatHealthRuntime.AdjustMaximum(__instance, ref amount);
}

[HarmonyPatch(typeof(CombatManager), "AfterCreatureAdded", new[] { typeof(Creature), typeof(CombatState) })]
internal static class BlackFlowSpireSummonLifePatch
{
    private static void Postfix(Creature creature, CombatState state, ref Task __result)
    {
        if (creature.Side == CombatSide.Player && !creature.IsPlayer && creature.Monster is not Osty)
            __result = HarmonyAsyncTaskBridge.After(__result, () => BlackFlowSpireCombatHealthRuntime.ApplySummonLife(creature, state));
    }
}

[HarmonyPatch(typeof(OstyCmd), nameof(OstyCmd.Summon), MethodType.Async)]
internal static class BlackFlowSpireOstySummonLifePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
    {
        var rewriter = HarmonyIlRewriter.From(source);
        var setMaximum = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp));
        var heal = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal));
        if (rewriter.FindAll(HarmonyIl.IsCall(setMaximum)).Count != 1 || rewriter.FindAll(HarmonyIl.IsCall(heal)).Count != 1)
        {
            Entry.Logger.Warn("[BlackFlowSpire] Could not locate Osty's initial summon and revival health setup.");
            return rewriter.Instructions();
        }

        // Initial summon and revival set an absolute base. Growth while alive
        // uses native GainMaxHp and goes through the ordinary maximum HP patch.
        var maximumRewrite = rewriter.ReplaceCall("Osty summon maximum", setMaximum,
            AccessTools.Method(typeof(BlackFlowSpireCombatHealthRuntime), nameof(BlackFlowSpireCombatHealthRuntime.SetOstySummonMaximum)));
        var healRewrite = rewriter.ReplaceCall("Osty summon healing", heal,
            AccessTools.Method(typeof(BlackFlowSpireCombatHealthRuntime), nameof(BlackFlowSpireCombatHealthRuntime.HealOstyOnSummon)));
        return rewriter.InstructionsChecked([maximumRewrite, healRewrite]);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ToSerializable))]
internal static class BlackFlowSpirePlayerCombatLifeSavePatch
{
    private static void Postfix(Player __instance, SerializablePlayer __result)
    {
        if (BlackFlowSpireCombatHealthRuntime.GetBaseMaximum(__instance.Creature) is not { } maximum) return;
        __result.MaxHp = maximum;
        __result.CurrentHp = Math.Min(__result.CurrentHp, maximum);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class BlackFlowSpirePlayerCombatLifeRestorePatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (runState is RunState run && BlackFlowSpireGenerationService.IsEnabled(run) && combatState is CombatState combat)
            __result = RestoreAfterCombat(__result, combat);
    }

    private static async Task RestoreAfterCombat(Task original, CombatState combat)
    {
        try { await original; }
        finally { BlackFlowSpireCombatHealthRuntime.RestoreCombatLife(combat); }
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class BlackFlowSpirePlayerCombatLifeResetPatch
{
    private static void Prefix(CombatManager __instance)
    {
        if (__instance.DebugOnlyGetState() is { } combat)
            BlackFlowSpireCombatHealthRuntime.RestoreCombatLife(combat);
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.EnterInternal))]
internal static class BlackFlowSpireCombatHealthEntryPatch
{
    private static void Prefix(CombatRoom __instance, IRunState? runState, bool isRestoringRoomStackBase, out bool __state)
    {
        __state = false;
        if (!isRestoringRoomStackBase && !__instance.IsPreFinished
            && runState is RunState run && BlackFlowSpireGenerationService.IsEnabled(run))
        {
            BlackFlowSpireCombatHealthRuntime.Begin(run, __instance.CombatState);
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
            BlackFlowSpireCombatHealthRuntime.RestoreCombatLife(combat);
            throw;
        }
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.CurrentHp), MethodType.Setter)]
internal static class BlackFlowSpireCombatHpChangePatch
{
    private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;

    private static void Postfix(Creature __instance, int __state) =>
        BlackFlowSpireCombatHealthRuntime.RecordHpChange(__instance, __state);
}

[HarmonyPatch]
internal static class BlackFlowSpireCombatHealthVictorySettlementPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CombatManager))
        .Single(method => method.Name == "EndCombatInternal" && method.GetParameters().Length == 1);

    private static bool Prefix(CombatManager __instance, object __0, ref Task __result)
    {
        var combat = BlackFlowSpireNativeTurnAccess.State(__0);
        if (!BlackFlowSpireCombatHealthRuntime.Settle(combat, won: true))
        {
            // Settle on each peer before the game clears IsInProgress and allows host snapshots to apply.
            if (combat?.RunState is RunState run && run.CurrentRoom is CombatRoom room)
                BlackFlowSpireCombatHealthRuntime.RecordCommandExperience(run, room);
            return true;
        }

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
internal static class BlackFlowSpireCombatHealthLossSettlementPatch
{
    private static void Postfix(CombatManager __instance)
    {
        if (__instance.IsInProgress)
            BlackFlowSpireCombatHealthRuntime.Settle(__instance.DebugOnlyGetState(), won: false);
    }
}

[HarmonyPatch(typeof(CombatManager), "ProcessPendingLoss")]
internal static class BlackFlowSpireCombatHealthDefeatPatch
{
    private static void Prefix(object __0, out bool __state)
    {
        __state = false;
        if (!CombatManager.Instance.IsAboutToLose) return;
        var combat = BlackFlowSpireNativeTurnAccess.State(__0);
        __state = BlackFlowSpireCombatHealthRuntime.IsTargetDefeat(combat);
        BlackFlowSpireCombatHealthRuntime.RestoreCombatLife(combat);
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
internal static class BlackFlowSpireTargetLifeGameOverPatch
{
    private static void Postfix(RunState __instance, ref bool __result)
    {
        if (!__result && BlackFlowSpireGenerationService.IsEnabled(__instance))
            __result = BlackFlowSpireGenerationService.GetTargetLife(__instance).Current <= 0;
    }
}
