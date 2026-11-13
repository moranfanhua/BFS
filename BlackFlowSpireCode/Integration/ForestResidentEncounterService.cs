using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace BlackFlowSpire.Integration;

internal static class ForestResidentEncounterService
{
    public static bool Populate(ForestMap map)
    {
        var changed = false;
        foreach (var node in map.Nodes.Where(node => node.Content == ForestNodeContent.BattleSavage
                     && node.CombatSetup == null))
        {
            node.CombatSetup = CreateHoppers(map, $"resident-combat/v1/stronghold/{node.Id}", ForestSpecialEncounter.Resident, 1);
            changed = true;
        }
        foreach (var resident in map.RoamingResidents.Where(resident => resident.CombatSetup == null))
        {
            resident.CombatSetup = CreateHoppers(map, $"resident-combat/v1/roamer/{resident.Id}", ForestSpecialEncounter.Resident, 1);
            changed = true;
        }
        return changed;
    }

    internal static ForestCombatSetup CreateHunt(ForestMap map) =>
        CreateHoppers(map, "hunt-combat/v1", ForestSpecialEncounter.Hunt, 2);

    private static ForestCombatSetup CreateHoppers(ForestMap map, string salt, ForestSpecialEncounter special, int count)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, salt);
        var seed = ((ulong)(uint)random.Next(int.MaxValue) << 32) | (uint)random.Next(int.MaxValue);
        var enemy = new ForestEnemySlot(ModelDb.Monster<ThievingHopper>().Id.ToString(), null);
        return new()
        {
            EncounterId = ModelDb.Encounter<ThievingHopperWeak>().Id.ToString(),
            Seed = seed, Enemies = Enumerable.Repeat(enemy, count).ToList(), Special = special
        };
    }
}
