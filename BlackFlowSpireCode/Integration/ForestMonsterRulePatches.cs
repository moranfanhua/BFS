using System.Globalization;
using System.Runtime.CompilerServices;
using BlackFlowSpire.Map.Stages;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace BlackFlowSpire.Integration;

internal static class ForestMonsterRules
{
    private sealed record Binding(ForestMonsterRule Rule, bool Initial);
    private static readonly ConditionalWeakTable<MonsterModel, Binding> Bindings = new();
    private static readonly AccessTools.FieldRef<MonsterModel, Creature?> MonsterCreature =
        AccessTools.FieldRefAccess<MonsterModel, Creature?>("_creature");
    private static readonly AccessTools.FieldRef<MonsterModel, MonsterMoveStateMachine?> StateMachine =
        AccessTools.FieldRefAccess<MonsterModel, MonsterMoveStateMachine?>("_moveStateMachine");

    private static readonly Action<MoveState, IReadOnlyList<AbstractIntent>> SetIntents = AccessTools.PropertySetter(
        typeof(MoveState), nameof(MoveState.Intents)).CreateDelegate<Action<MoveState, IReadOnlyList<AbstractIntent>>>();

    internal static void Bind(EncounterModel? encounter, MonsterModel monster)
    {
        if (!ForestEncounterFactory.TryGet(encounter, out var setup)) return;
        var formation = encounter!.MonstersWithSlots;
        var index = Enumerable.Range(0, formation.Count)
            .FirstOrDefault(i => ReferenceEquals(formation[i].Item1, monster), -1);
        var rule = setup.Stage!.MonsterRules.FirstOrDefault(item => item.MonsterId == monster.Id.ToString()
            && (!item.InitialOnly || index >= 0) && (!item.FormationIndex.HasValue || item.FormationIndex == index));
        if (rule == null) return;
        Bindings.Remove(monster);
        Bindings.Add(monster, new(rule, index >= 0));
        ForestStageNativePropertyPatch.EnsureOffsets(monster.GetType(), rule.NativePropertyOffsets.Keys);
    }

    internal static ForestMonsterRule? Find(MonsterModel monster) => Bindings.TryGetValue(monster, out var binding)
        ? binding.Rule : Find(MonsterCreature(monster));

    internal static bool IsInitial(MonsterModel monster) => Bindings.TryGetValue(monster, out var binding) && binding.Initial;

    internal static ForestMonsterRule? Find(Creature? creature)
    {
        if (creature?.Side != CombatSide.Enemy || creature.Monster == null) return null;
        if (!Bindings.TryGetValue(creature.Monster, out var binding))
        {
            Bind(creature.CombatState?.Encounter, creature.Monster);
            Bindings.TryGetValue(creature.Monster, out binding);
        }
        return binding?.Rule;
    }

    internal static void SetUpMoves(MonsterModel monster)
    {
        var rule = Find(monster);
        var machine = monster.MoveStateMachine;
        if (rule == null || machine == null) return;
        if (rule.StatusCardCountOffset != 0)
            foreach (var move in machine.States.Values.OfType<MoveState>())
                if (move.Intents.Any(intent => intent is StatusIntent))
                    SetIntents(move, move.Intents.Select(intent => intent is StatusIntent status
                        ? new StatusIntent(Math.Max(0, status.CardCount + rule.StatusCardCountOffset)) : intent).ToArray());
        if (rule.FirstMoveId == null && rule.MoveCycle.Count == 0) return;
        MoveState Move(string id) => machine.States.GetValueOrDefault(id) as MoveState
            ?? throw new InvalidDataException($"Missing move {id} for {monster.Id}.");
        for (var i = 0; i < rule.MoveCycle.Count; i++)
            Move(rule.MoveCycle[i]).FollowUpState = Move(rule.MoveCycle[(i + 1) % rule.MoveCycle.Count]);
        var first = rule.FirstMoveId ?? rule.MoveCycle[0];
        StateMachine(monster) = new MonsterMoveStateMachine(machine.States.Values, Move(first));
    }

    internal static async Task ApplyStartingPowers(Creature creature, Task original)
    {
        await original;
        var rule = Find(creature);
        if (rule == null) return;
        foreach (var id in rule.RemovedStartingPowers)
            await PowerCmd.Remove(creature.GetPower(ModelId.Deserialize(id)));
        foreach (var power in rule.StartingPowers)
        {
            var amount = power.AmountProperty == null ? power.Amount : Convert.ToDecimal(
                AccessTools.Property(creature.Monster!.GetType(), power.AmountProperty)?.GetValue(creature.Monster)
                    ?? throw new InvalidDataException("Missing native monster property: " + power.AmountProperty), CultureInfo.InvariantCulture);
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(),
                ModelDb.GetById<PowerModel>(ModelId.Deserialize(power.PowerId)).ToMutable(), creature, amount, creature, null);
        }
        if (rule.StartingBlock > 0m)
            await CreatureCmd.GainBlock(creature, rule.StartingBlock, ValueProp.Unpowered, null);
        if (rule.RoomEntryMoveId is { } moveId)
        {
            var move = creature.Monster!.MoveStateMachine?.States.GetValueOrDefault(moveId) as MoveState
                ?? throw new InvalidDataException("Missing room-entry move: " + moveId);
            await move.PerformMove(creature.CombatState!.Players.Select(player => player.Creature));
        }
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetUpForCombat))]
internal static class ForestStageMonsterMovesPatch
{
    private static void Postfix(MonsterModel __instance) => ForestMonsterRules.SetUpMoves(__instance);
}

[HarmonyPatch(typeof(Creature), nameof(Creature.AfterAddedToRoom))]
internal static class ForestStageStartingPowersPatch
{
    private static void Postfix(Creature __instance, ref Task __result)
    {
        if (ForestMonsterRules.Find(__instance) is { } rule
            && (rule.StartingPowers.Count != 0 || rule.RemovedStartingPowers.Count != 0
                || rule.StartingBlock > 0m || rule.RoomEntryMoveId != null))
            __result = ForestMonsterRules.ApplyStartingPowers(__instance, __result);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountGiven))]
internal static class ForestStageSelfPowerAmountPatch
{
    private static void Prefix(PowerModel power, Creature giver, Creature? target, ref decimal amount)
    {
        if (amount > 0m && ReferenceEquals(giver, target)
            && ForestMonsterRules.Find(giver) is { } rule
            && rule.SelfPowerAmountOffsets.TryGetValue(power.Id.ToString(), out var offset))
            amount = Math.Max(0m, amount + offset);
    }
}

[HarmonyPatch(typeof(InfestedPower), nameof(InfestedPower.AfterDeath))]
internal static class ForestStageInfestedSpawnPatch
{
    // Retain the native reveal delay and special scene slots. The phase music
    // patch skips the elite-only music call when this is an ordinary stage.
    private static readonly Func<List<Creature>, float, Task> Reveal = AccessTools.Method(
        typeof(InfestedPower), "RevealWrigglersAfterDeathAnim").CreateDelegate<Func<List<Creature>, float, Task>>();

    private static bool Prefix(InfestedPower __instance, Creature target, bool wasRemovalPrevented,
        float deathAnimLength, ref Task __result)
    {
        if (ForestMonsterRules.Find(__instance.Owner)?.DeathSpawnCount is not { } count) return true;
        __result = Spawn(__instance, target, wasRemovalPrevented, deathAnimLength, count);
        return false;
    }

    private static async Task Spawn(InfestedPower power, Creature target, bool prevented, float deathAnimLength, int count)
    {
        if (prevented || !ReferenceEquals(power.Owner, target)) return;
        List<Creature> spawned = [];
        for (var i = 0; i < count; i++)
        {
            var monster = (Wriggler)ModelDb.Monster<Wriggler>().ToMutable();
            monster.StartStunned = true;
            var creature = await CreatureCmd.Add(monster, power.CombatState, power.Owner.Side,
                PhrogParasiteElite.GetWrigglerSlotName(i));
            creature.SetNodeVisible(false);
            spawned.Add(creature);
        }
        _ = TaskHelper.RunSafely(Reveal(spawned, deathAnimLength));
    }
}
