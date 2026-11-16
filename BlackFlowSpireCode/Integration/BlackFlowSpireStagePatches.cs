using System.Reflection;
using System.Text.Json;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

[HarmonyPatch]
internal static class BlackFlowSpireStageRoomTypePatch
{
    // Native encounters override RoomType, so patch the concrete getters rather
    // than only the abstract base. Untagged instances, including canonical ones,
    // retain their normal behavior in every other mode.
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(EncounterModel).Assembly, typeof(Entry).Assembly }
        .Distinct().SelectMany(assembly => assembly.GetTypes())
        .Where(type => typeof(EncounterModel).IsAssignableFrom(type))
        .Select(type => type.GetMethod("get_RoomType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .OfType<MethodInfo>().Where(method => !method.IsAbstract);

    internal static void EnsurePatched(Type encounterType)
    {
        var getter = encounterType.GetProperty(nameof(EncounterModel.RoomType))?.GetMethod
            ?? throw new InvalidDataException("Encounter has no room type getter.");
        if (Harmony.GetPatchInfo(getter)?.Owners.Contains(Entry.ModId) == true) return;
        new Harmony(Entry.ModId).Patch(getter,
            postfix: new HarmonyMethod(AccessTools.Method(typeof(BlackFlowSpireStageRoomTypePatch), nameof(Postfix))));
    }

    private static void Postfix(EncounterModel __instance, ref RoomType __result)
    {
        if (!BlackFlowSpireEncounterFactory.TryGet(__instance, out var setup)) return;
        __result = setup.Stage!.Variant switch
        {
            BlackFlowSpireBattleVariant.Normal => RoomType.Monster,
            BlackFlowSpireBattleVariant.Emergency => RoomType.Elite,
            BlackFlowSpireBattleVariant.Boss => RoomType.Boss,
            _ => throw new InvalidDataException("Invalid stage variant.")
        };
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
internal static class BlackFlowSpireStageEnemyHealthPatch
{
    private static void Prefix(CombatState __instance, MonsterModel monster, CombatSide side)
    {
        if (side == CombatSide.Enemy) BlackFlowSpireMonsterRules.Bind(__instance.Encounter, monster);
    }

    private static void Postfix(Creature __result) => BlackFlowSpireEncounterFactory.ScaleCreatedEnemy(__result);
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class BlackFlowSpireStageFixedFormationPatch
{
    private static void Postfix(EncounterModel __instance) => BlackFlowSpireEncounterFactory.ApplyFixedFormation(__instance);
}

[HarmonyPatch]
internal static class BlackFlowSpireStageMonsterAssetsPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(EncounterModel).Assembly, typeof(Entry).Assembly }
        .Distinct().SelectMany(assembly => assembly.GetTypes())
        .Where(type => typeof(EncounterModel).IsAssignableFrom(type))
        .Select(type => type.GetMethod("get_AllPossibleMonsters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        .OfType<MethodInfo>().Where(method => !method.IsAbstract);

    internal static void EnsurePatched(Type encounterType)
    {
        var getter = encounterType.GetProperty(nameof(EncounterModel.AllPossibleMonsters))?.GetMethod
            ?? throw new InvalidDataException("Encounter has no monster asset getter.");
        if (Harmony.GetPatchInfo(getter)?.Owners.Contains(Entry.ModId) == true) return;
        new Harmony(Entry.ModId).Patch(getter,
            postfix: new HarmonyMethod(AccessTools.Method(typeof(BlackFlowSpireStageMonsterAssetsPatch), nameof(Postfix))));
    }

    private static void Postfix(EncounterModel __instance, ref IEnumerable<MonsterModel> __result)
    {
        if (!BlackFlowSpireEncounterFactory.TryGet(__instance, out var setup) || !setup.Stage!.UsesCustomFormation) return;
        __result = __result.Concat(setup.Enemies.Select(enemy =>
                ModelDb.GetById<MonsterModel>(ModelId.Deserialize(enemy.MonsterId))))
            .DistinctBy(monster => monster.Id).ToArray();
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class BlackFlowSpireStageAttackDamagePatch
{
    // Both native intent preview and actual attacks call this entry point.
    // Scale base move damage before native Strength/Weak/caps, not card damage,
    // poison, thorns, or any attack performed by the player's side.
    private static void Prefix(Creature? dealer, ValueProp props, ref decimal damage)
    {
        if (dealer?.Side != CombatSide.Enemy || dealer.Monster == null || !props.HasFlag(ValueProp.Move)
            || !BlackFlowSpireEncounterFactory.TryGet(dealer.CombatState?.Encounter, out var setup)) return;
        damage = setup.Stage!.Balance.ScaleAttack(damage, dealer.Monster.Id.ToString());
    }
}

[HarmonyPatch(typeof(ToughEgg), "Hatch")]
internal static class BlackFlowSpireStageHatchHealthPatch
{
    // Hatch explicitly rolls and replaces HP, after the egg was created.
    private static void Postfix(ToughEgg __instance, ref Task __result) =>
        __result = HarmonyAsyncTaskBridge.After(__result, () => BlackFlowSpireEncounterFactory.ScaleCreatedEnemy(__instance.Creature));
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.ToSerializable))]
internal static class BlackFlowSpireStageSavePatch
{
    internal const string StateKey = "BlackFlowSpire.FixedStage.v1";
    internal const string SettlementKey = "BlackFlowSpire.BattleSettlement.v1";

    private static void Postfix(CombatRoom __instance, SerializableRoom __result)
    {
        if (BlackFlowSpireEncounterFactory.TryGetFixedSetup(__instance.Encounter, out var setup))
            __result.EncounterState[StateKey] = JsonSerializer.Serialize(setup);
        if (__instance.IsPreFinished && BlackFlowSpireCombatHealthRuntime.GetSettlement(__instance) is { } result)
            __result.EncounterState[SettlementKey] = JsonSerializer.Serialize(result);
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.FromSerializable))]
internal static class BlackFlowSpireStageRestorePatch
{
    private static void Prefix(SerializableRoom serializableRoom, out BlackFlowSpireCombatSetup? __state)
    {
        __state = null;
        if (!serializableRoom.EncounterState.TryGetValue(BlackFlowSpireStageSavePatch.StateKey, out var json)) return;
        __state = JsonSerializer.Deserialize<BlackFlowSpireCombatSetup>(json)
            ?? throw new InvalidDataException("Invalid saved forest stage.");
        if (__state.EncounterId != serializableRoom.EncounterId?.ToString()
            || __state.Enemies == null || __state.Enemies.Count == 0)
            throw new InvalidDataException("Saved stage and room identity differ.");
        if (__state.Special is BlackFlowSpireSpecialEncounter.Hunt or BlackFlowSpireSpecialEncounter.Resident)
        {
            if (__state.Stage != null || serializableRoom.RoomType != RoomType.Monster)
                throw new InvalidDataException("Saved special encounter and room type differ.");
            return;
        }
        if (__state.Stage == null || __state.Special != BlackFlowSpireSpecialEncounter.None)
            throw new InvalidDataException("Invalid saved stage type.");
        __state.Stage.Validate();
        var expectedType = __state.Stage.Variant == BlackFlowSpireBattleVariant.Boss ? RoomType.Boss
            : __state.Stage.Variant == BlackFlowSpireBattleVariant.Emergency ? RoomType.Elite : RoomType.Monster;
        if (serializableRoom.RoomType != expectedType)
            throw new InvalidDataException("Saved stage and room variant differ.");
    }

    private static void Postfix(CombatRoom __result, SerializableRoom serializableRoom, IRunState? runState, BlackFlowSpireCombatSetup? __state)
    {
        if (__state != null)
            BlackFlowSpireEncounterFactory.Prepare(__result.Encounter, __state.Stage != null ? runState : null, __state);
        if (__result.IsPreFinished && serializableRoom.EncounterState.TryGetValue(BlackFlowSpireStageSavePatch.SettlementKey, out var json))
            BlackFlowSpireCombatHealthRuntime.RestoreSettlement(__result, JsonSerializer.Deserialize<BlackFlowSpireCombatHealthRuntime.Settlement>(json));
    }
}
