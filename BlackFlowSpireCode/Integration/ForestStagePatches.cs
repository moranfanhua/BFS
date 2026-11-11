using System.Reflection;
using System.Text.Json;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace BlackFlowSpire.Integration;

[HarmonyPatch]
internal static class ForestStageRoomTypePatch
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
            postfix: new HarmonyMethod(AccessTools.Method(typeof(ForestStageRoomTypePatch), nameof(Postfix))));
    }

    private static void Postfix(EncounterModel __instance, ref RoomType __result)
    {
        if (!ForestEncounterFactory.TryGet(__instance, out var setup)) return;
        __result = setup.Stage!.Variant switch
        {
            ForestBattleVariant.Normal => RoomType.Monster,
            ForestBattleVariant.Emergency => RoomType.Elite,
            ForestBattleVariant.Boss => RoomType.Boss,
            _ => throw new InvalidDataException("Invalid stage variant.")
        };
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
internal static class ForestStageEnemyHealthPatch
{
    private static void Prefix(CombatState __instance, MonsterModel monster, CombatSide side)
    {
        if (side == CombatSide.Enemy) ForestMonsterRules.Bind(__instance.Encounter, monster);
    }

    private static void Postfix(Creature __result) => ForestEncounterFactory.ScaleCreatedEnemy(__result);
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class ForestStageFixedFormationPatch
{
    private static void Postfix(EncounterModel __instance) => ForestEncounterFactory.ApplyFixedFormation(__instance);
}

[HarmonyPatch]
internal static class ForestStageMonsterAssetsPatch
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
            postfix: new HarmonyMethod(AccessTools.Method(typeof(ForestStageMonsterAssetsPatch), nameof(Postfix))));
    }

    private static void Postfix(EncounterModel __instance, ref IEnumerable<MonsterModel> __result)
    {
        if (!ForestEncounterFactory.TryGet(__instance, out var setup) || !setup.Stage!.UsesCustomFormation) return;
        __result = __result.Concat(setup.Enemies.Select(enemy =>
                ModelDb.GetById<MonsterModel>(ModelId.Deserialize(enemy.MonsterId))))
            .DistinctBy(monster => monster.Id).ToArray();
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class ForestStageAttackDamagePatch
{
    // Both native intent preview and actual attacks call this entry point.
    // Scale base move damage before native Strength/Weak/caps, not card damage,
    // poison, thorns, or any attack performed by the player's side.
    private static void Prefix(Creature? dealer, ValueProp props, ref decimal damage)
    {
        if (dealer?.Side != CombatSide.Enemy || dealer.Monster == null || !props.HasFlag(ValueProp.Move)
            || !ForestEncounterFactory.TryGet(dealer.CombatState?.Encounter, out var setup)) return;
        damage = setup.Stage!.Balance.ScaleAttack(damage, dealer.Monster.Id.ToString());
    }
}

[HarmonyPatch(typeof(ToughEgg), "Hatch")]
internal static class ForestStageHatchHealthPatch
{
    private static void Postfix(ToughEgg __instance, ref Task __result) => __result = ScaleAfterHatch(__result, __instance);

    private static async Task ScaleAfterHatch(Task original, ToughEgg egg)
    {
        await original;
        // Hatch explicitly rolls and replaces HP, after the egg was created.
        ForestEncounterFactory.ScaleCreatedEnemy(egg.Creature);
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.ToSerializable))]
internal static class ForestStageSavePatch
{
    internal const string StateKey = "BlackFlowSpire.FixedStage.v1";

    private static void Postfix(CombatRoom __instance, SerializableRoom __result)
    {
        if (ForestEncounterFactory.TryGet(__instance.Encounter, out var setup))
            __result.EncounterState[StateKey] = JsonSerializer.Serialize(setup);
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.FromSerializable))]
internal static class ForestStageRestorePatch
{
    private static void Prefix(SerializableRoom serializableRoom, out ForestCombatSetup? __state)
    {
        __state = null;
        if (!serializableRoom.EncounterState.TryGetValue(ForestStageSavePatch.StateKey, out var json)) return;
        __state = JsonSerializer.Deserialize<ForestCombatSetup>(json)
            ?? throw new InvalidDataException("Invalid saved forest stage.");
        if (__state.Stage == null || __state.EncounterId != serializableRoom.EncounterId?.ToString()
            || __state.Enemies == null || __state.Enemies.Count == 0)
            throw new InvalidDataException("Saved stage and room identity differ.");
        __state.Stage.Validate();
        var expectedType = __state.Stage.Variant == ForestBattleVariant.Boss ? RoomType.Boss
            : __state.Stage.Variant == ForestBattleVariant.Emergency ? RoomType.Elite : RoomType.Monster;
        if (serializableRoom.RoomType != expectedType)
            throw new InvalidDataException("Saved stage and room variant differ.");
    }

    private static void Postfix(CombatRoom __result, IRunState? runState, ForestCombatSetup? __state)
    {
        if (__state != null) ForestEncounterFactory.Prepare(__result.Encounter, runState, __state);
    }
}
