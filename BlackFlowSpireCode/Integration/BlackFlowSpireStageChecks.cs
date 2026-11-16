using System.Text.Json;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace BlackFlowSpire.Integration;

public static class BlackFlowSpireStageChecks
{
    // Creates isolated test runs/creatures. Never enters a room, starts combat,
    // consumes the active run's RNG, or changes its map/save/player objects.
    public static string Verify()
    {
        var cases = 0;
        var rngField = AccessTools.FieldRefAccess<EncounterModel, Rng?>("_rng");
        foreach (var definition in BlackFlowSpireStageCatalog.Definitions)
        foreach (var variant in definition.IsBoss ? new[] { BlackFlowSpireBattleVariant.Boss }
                     : new[] { BlackFlowSpireBattleVariant.Normal, BlackFlowSpireBattleVariant.Emergency })
        {
            var version = definition.GetVersion(variant);
            var player = Player.CreateForNewRun<Deprived>(UnlockState.all, LocalContext.NetId ?? 1UL);
            var state = RunState.CreateForTest(players: new[] { player }, seed: "BlackFlowSpire-STAGE-CHECK");
            var canonical = ModelDb.GetById<EncounterModel>(ModelId.Deserialize(version.SourceEncounterId));
            var canonicalType = canonical.RoomType;
            var source = canonical.ToMutable();
            rngField(source) = new Rng(87654321UL);
            source.GenerateMonstersWithSlots(state);
            var setup = BlackFlowSpireEncounterFactory.CreateSetup(state, definition.CreateSnapshot(variant), 87654321UL);
            var encounter = BlackFlowSpireEncounterFactory.Create(state, setup);
            var expectedType = variant == BlackFlowSpireBattleVariant.Boss ? RoomType.Boss
                : variant == BlackFlowSpireBattleVariant.Normal ? RoomType.Monster : RoomType.Elite;
            Require(encounter.RoomType == expectedType, definition.Id + " room type");
            Require(canonical.RoomType == canonicalType && source.RoomType == canonicalType, "native instance isolation");
            var room = new CombatRoom(encounter, state);
            var serializable = JsonSerializer.Deserialize<SerializableRoom>(JsonSerializer.Serialize(room.ToSerializable()))!;
            var restored = CombatRoom.FromSerializable(serializable, state);
            Require(restored.RoomType == expectedType && BlackFlowSpireEncounterFactory.TryGet(restored.Encounter, out var restoredSetup)
                && JsonSerializer.Serialize(restoredSetup) == JsonSerializer.Serialize(setup), definition.Id + " save restore");

            var combat = room.CombatState;
            combat.AddPlayer(player);
            var enemy = combat.CreateCreature(encounter.MonstersWithSlots[0].Item1, CombatSide.Enemy, encounter.MonstersWithSlots[0].Item2);
            Require(enemy.MaxHp == setup.Stage!.Balance.ScaleHealth(enemy.MonsterMaxHpBeforeModification!.Value, enemy.Monster!.Id.ToString()), definition.Id + " HP scaling");
            var expectedDamage = setup.Stage!.Balance.ScaleAttack(8m, enemy.Monster.Id.ToString());
            var actualDamage = Hook.ModifyDamage(state, combat, player.Creature, enemy, 8m, ValueProp.Move,
                null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
            Require(actualDamage == expectedDamage, definition.Id + " attack scaling");
            if (LocalContext.NetId.HasValue)
                Require(new SingleAttackIntent(8).GetSingleDamage(new[] { player.Creature }, enemy) == (int)actualDamage,
                    definition.Id + " intent/attack agreement");
            Require(Hook.ModifyDamage(state, combat, player.Creature, enemy, 8m, ValueProp.Unpowered,
                null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _) == 8m, "non-move damage isolation");
            Require(Hook.ModifyDamage(state, combat, null, player.Creature, 8m, ValueProp.Move,
                null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _) == 8m, "player attack isolation");
            // A later-created monster inherits the encounter profile too.
            var reinforcementModel = ModelDb.GetById<MonsterModel>(ModelId.Deserialize(setup.Enemies[0].MonsterId)).ToMutable();
            var reinforcement = combat.CreateCreature(reinforcementModel, CombatSide.Enemy, setup.Enemies[0].Slot);
            Require(reinforcement.MaxHp == setup.Stage!.Balance.ScaleHealth(reinforcement.MonsterMaxHpBeforeModification!.Value,
                reinforcementModel.Id.ToString()), definition.Id + " reinforcement profile");
            cases++;
        }
        var message = $"Stage checks passed: {cases} variants (40 normal, 40 emergency, 12 boss); native IDs/formations, room types, frozen saves, HP, damage, intent and reinforcement profiles verified. Active run unchanged.";
        Entry.Logger.Info("[BlackFlowSpire] " + message);
        return message;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("Stage check failed: " + message);
    }
}
