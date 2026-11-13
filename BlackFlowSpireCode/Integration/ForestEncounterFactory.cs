using System.Runtime.CompilerServices;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

// Keep the native concrete type: several encounters cast themselves in monster
// scripts or provide special slots/scenes. Only the new mutable instance is tagged.
internal static class ForestEncounterFactory
{
    private static readonly AccessTools.FieldRef<EncounterModel, Rng?> EncounterRng =
        AccessTools.FieldRefAccess<EncounterModel, Rng?>("_rng");
    private static readonly AccessTools.FieldRef<EncounterModel, IReadOnlyList<(MonsterModel, string?)>?> Formation =
        AccessTools.FieldRefAccess<EncounterModel, IReadOnlyList<(MonsterModel, string?)>?>("_monstersWithSlots");
    private static readonly ConditionalWeakTable<EncounterModel, ForestCombatSetup> Context = new();

    public static ForestCombatSetup CreateSetup(IRunState state, ForestStageSnapshot stage, ulong seed)
    {
        var frozen = stage.Copy();
        var version = ForestStageCatalog.Get(frozen.StageId).GetVersion(frozen.Variant);
        var encounter = ModelDb.GetById<EncounterModel>(ModelId.Deserialize(frozen.SourceEncounterId)).ToMutable();
        EncounterRng(encounter) = new Rng(seed);
        encounter.GenerateMonstersWithSlots(state);
        var original = encounter.MonstersWithSlots.Select(item => new ForestEnemySlot(item.Item1.Id.ToString(), item.Item2)).ToArray();
        var desired = version.BuildFormation(Array.AsReadOnly(original), seed)?.ToArray()
            ?? throw new InvalidDataException("Stage code returned a null enemy formation.");
        version.ValidateFormation(desired);
        frozen.UsesCustomFormation = !original.SequenceEqual(desired);
        // Resolve added monsters now, while generating the map rather than when
        // the player eventually enters the node.
        if (frozen.UsesCustomFormation) ReplaceFormation(encounter, desired);
        return new()
        {
            EncounterId = frozen.SourceEncounterId, Seed = seed, Stage = frozen, Enemies = desired.ToList()
        };
    }

    public static EncounterModel Create(IRunState? state, ForestCombatSetup setup)
    {
        var encounter = ModelDb.GetById<EncounterModel>(ModelId.Deserialize(setup.EncounterId)).ToMutable();
        Prepare(encounter, state, setup);
        return encounter;
    }

    public static void Prepare(EncounterModel encounter, IRunState? state, ForestCombatSetup setup)
    {
        if (!encounter.IsMutable || encounter.Id.ToString() != setup.EncounterId)
            throw new InvalidDataException("Saved encounter does not match the native instance.");
        setup.Stage?.Validate();
        if (setup.Stage is { } snapshot)
        {
            ForestStageRoomTypePatch.EnsurePatched(encounter.GetType());
            ForestStageMonsterAssetsPatch.EnsurePatched(encounter.GetType());
            if (snapshot.SourceEncounterId != setup.EncounterId)
                throw new InvalidDataException("Saved stage does not match its source encounter.");
            if (snapshot.UseGenericEnemyLayout) ForestStageGenericLayoutPatch.EnsurePatched(encounter.GetType());
        }
        if (setup.Stage != null || setup.Special != ForestSpecialEncounter.None)
        {
            Context.Remove(encounter);
            Context.Add(encounter, Copy(setup));
        }
        EncounterRng(encounter) = new Rng(setup.Seed);
        if (!encounter.HaveMonstersBeenGenerated && state != null)
            encounter.GenerateMonstersWithSlots(state);
        if (encounter.HaveMonstersBeenGenerated)
        {
            ApplyFixedFormation(encounter);
            var actual = encounter.MonstersWithSlots.Select(item => new ForestEnemySlot(item.Item1.Id.ToString(), item.Item2)).ToArray();
            if (!actual.SequenceEqual(setup.Enemies))
                throw new InvalidDataException("The native encounter no longer matches the saved enemy formation.");
            if (setup.Stage is { } stage && ForestStageCatalog.Find(stage.StageId) is { } family
                && family.IsBoss == (stage.Variant == ForestBattleVariant.Boss))
            {
                var version = family.GetVersion(stage.Variant);
                if (version.Id == stage.VersionId && version.Revision == stage.Revision
                    && version.SourceEncounterId == stage.SourceEncounterId)
                    version.ValidateFormation(actual);
            }
        }
    }

    internal static void ApplyFixedFormation(EncounterModel encounter)
    {
        if (encounter.HaveMonstersBeenGenerated && TryGetFixedSetup(encounter, out var setup)
            && (setup.Stage?.UsesCustomFormation == true || setup.Special == ForestSpecialEncounter.Hunt))
            ReplaceFormation(encounter, setup.Enemies);
    }

    private static void ReplaceFormation(EncounterModel encounter, IReadOnlyList<ForestEnemySlot> desired)
    {
        var native = encounter.MonstersWithSlots.ToList();
        var used = new HashSet<int>();
        var result = new List<(MonsterModel, string?)>();
        foreach (var enemy in desired)
        {
            // Reuse matching generated models to keep native setup flags, such
            // as the middle inklet or the front nibbit. Added enemies are fresh.
            var match = Enumerable.Range(0, native.Count).FirstOrDefault(index => !used.Contains(index)
                && native[index].Item1.Id.ToString() == enemy.MonsterId && native[index].Item2 == enemy.Slot, -1);
            if (match < 0)
                match = Enumerable.Range(0, native.Count).FirstOrDefault(index => !used.Contains(index)
                    && native[index].Item1.Id.ToString() == enemy.MonsterId, -1);
            MonsterModel monster;
            if (match >= 0) { used.Add(match); monster = native[match].Item1; }
            else monster = ModelDb.GetById<MonsterModel>(ModelId.Deserialize(enemy.MonsterId)).ToMutable();
            result.Add((monster, enemy.Slot));
        }
        Formation(encounter) = result;
    }

    public static bool TryGet(EncounterModel? encounter, out ForestCombatSetup setup) =>
        TryGetFixedSetup(encounter, out setup) && setup.Stage != null;

    internal static bool TryGetFixedSetup(EncounterModel? encounter, out ForestCombatSetup setup)
    {
        setup = null!;
        return encounter != null && Context.TryGetValue(encounter, out setup!);
    }

    internal static ForestSpecialEncounter SpecialKind(EncounterModel? encounter) =>
        TryGetFixedSetup(encounter, out var setup) ? setup.Special : ForestSpecialEncounter.None;

    public static ForestCombatSetup Copy(ForestCombatSetup setup) => new()
    {
        EncounterId = setup.EncounterId, Seed = setup.Seed, Enemies = setup.Enemies.ToList(),
        Stage = setup.Stage?.Copy(), Special = setup.Special
    };

    public static void ScaleCreatedEnemy(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || creature.Monster == null || creature.CombatState is not { } combat) return;
        var hp = creature.MaxHp;
        var scaledByStage = TryGet(combat.Encounter, out var setup);
        if (scaledByStage)
        {
            var summoned = !combat.Encounter!.MonstersWithSlots.Any(item => ReferenceEquals(item.Item1, creature.Monster));
            hp = setup.Stage!.Balance.ScaleHealth(hp, creature.Monster.Id.ToString(), summoned);
        }
        if (combat.RunState is RunState run && ForestGenerationService.IsEnabled(run))
        {
            var collectibles = ForestCollectibleService.GetInventory(run);
            var multiplier = ForestCollectibleRules.EnemyLife(collectibles, run.CurrentActIndex);
            if (SpecialKind(combat.Encounter) == ForestSpecialEncounter.Resident && collectibles.Has(208))
                multiplier *= 0.6m;
            if (multiplier != 1m)
            {
                hp = Math.Max(1, (int)decimal.Round(hp * multiplier, 0, MidpointRounding.AwayFromZero));
                scaledByStage = true;
            }
        }
        if (!scaledByStage) return;
        creature.SetMaxHpInternal(hp);
        creature.SetCurrentHpInternal(hp);
    }
}
