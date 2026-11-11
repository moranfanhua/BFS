using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BlackFlowSpire.Integration;

[HarmonyPatch]
internal static class ForestStageNativePropertyPatch
{
    // Patch the built-in getters before native monster actions are JIT compiled.
    private static IEnumerable<MethodBase> TargetMethods() => new[]
    {
        AccessTools.PropertyGetter(typeof(Axebot), nameof(Axebot.StockAmount)),
        AccessTools.PropertyGetter(typeof(Axebot), "RespawnCount"),
        AccessTools.PropertyGetter(typeof(TheLost), "DebilitatingSmogStrengthStealAmount"),
        AccessTools.PropertyGetter(typeof(TheForgotten), "DebilitatingSmogDexStealAmount")
    };

    internal static void EnsureOffsets(Type type, IEnumerable<string> properties)
    {
        foreach (var name in properties)
        {
            var property = AccessTools.Property(type, name);
            var getter = property?.GetGetMethod(true);
            if (getter == null || property!.PropertyType != typeof(int))
                throw new InvalidDataException($"Missing integer property {type.Name}.{name}.");
            if (Harmony.GetPatchInfo(getter)?.Owners.Contains(Entry.ModId) == true) continue;
            new Harmony(Entry.ModId).Patch(getter, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(ForestStageNativePropertyPatch), nameof(Postfix))));
        }
    }

    private static void Postfix(MonsterModel __instance, MethodBase __originalMethod, ref int __result)
    {
        var rule = ForestMonsterRules.Find(__instance);
        if (rule == null) return;
        var name = __originalMethod.Name[4..];
        if (rule.NativePropertyOffsets.TryGetValue(name, out var offset)) __result += offset;
        if (__instance is not Axebot || rule.InitialStockBonus == 0) return;
        if (name == nameof(Axebot.StockAmount) && ForestMonsterRules.IsInitial(__instance))
            __result += rule.InitialStockBonus;
        else if (name == "RespawnCount")
            __result += rule.InitialStockBonus;
    }
}

[HarmonyPatch(typeof(Chomper), "ScreechMove")]
internal static class ForestStageChomperStatusPatch
{
    private static readonly Func<MonsterModel, string> CastSfx = AccessTools.PropertyGetter(
        typeof(MonsterModel), "CastSfx").CreateDelegate<Func<MonsterModel, string>>();

    private static bool Prefix(Chomper __instance, IReadOnlyList<Creature> targets, ref Task __result)
    {
        if (ForestMonsterRules.Find(__instance)?.StatusCardCountOffset is not { } offset || offset == 0) return true;
        __result = Screech(__instance, targets, Math.Max(0, 3 + offset));
        return false;
    }

    private static async Task Screech(Chomper monster, IReadOnlyList<Creature> targets, int count)
    {
        // Native dialogue, sound and animation; only the card count changes.
        TalkCmd.Play(MonsterModel.L10NMonsterLookup("CHOMPER.moves.SCREECH.title"), monster.Creature, VfxColor.Cyan);
        SfxCmd.Play(CastSfx(monster));
        await CreatureCmd.TriggerAnim(monster.Creature, "Cast", 1f);
        await CardPileCmd.AddToCombatAndPreview<Dazed>(targets, PileType.Discard, count, null);
    }
}

internal static class ForestStageGenericLayoutPatch
{
    internal static void EnsurePatched(Type type)
    {
        Patch(AccessTools.PropertyGetter(type, nameof(EncounterModel.HasScene)), nameof(HasScene));
        Patch(AccessTools.PropertyGetter(type, nameof(EncounterModel.Slots)), nameof(Slots));
    }

    private static void Patch(MethodInfo getter, string postfix)
    {
        if (Harmony.GetPatchInfo(getter)?.Owners.Contains(Entry.ModId) == true) return;
        new Harmony(Entry.ModId).Patch(getter, postfix: new HarmonyMethod(
            AccessTools.Method(typeof(ForestStageGenericLayoutPatch), postfix)));
    }

    private static void HasScene(EncounterModel __instance, ref bool __result)
    {
        if (ForestEncounterFactory.TryGet(__instance, out var setup) && setup.Stage!.UseGenericEnemyLayout)
            __result = false;
    }

    private static void Slots(EncounterModel __instance, ref IReadOnlyList<string> __result)
    {
        if (ForestEncounterFactory.TryGet(__instance, out var setup) && setup.Stage!.UseGenericEnemyLayout)
            __result = Array.Empty<string>();
    }
}
