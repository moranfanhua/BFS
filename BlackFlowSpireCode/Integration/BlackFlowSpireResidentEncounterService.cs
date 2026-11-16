using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace BlackFlowSpire.Integration;

internal static class BlackFlowSpireResidentEncounterService
{
    public static void Populate(BlackFlowSpireMap map)
    {
        foreach (var node in map.Nodes.Where(node => node.Content == BlackFlowSpireNodeContent.BattleSavage))
            node.CombatSetup = CreateHoppers(map, $"resident-combat/v1/stronghold/{node.Id}", BlackFlowSpireSpecialEncounter.Resident, 1);
        foreach (var resident in map.RoamingResidents)
            resident.CombatSetup = CreateHoppers(map, $"resident-combat/v1/roamer/{resident.Id}", BlackFlowSpireSpecialEncounter.Resident, 1);
    }

    internal static BlackFlowSpireCombatSetup CreateHunt(BlackFlowSpireMap map) =>
        CreateHoppers(map, "hunt-combat/v1", BlackFlowSpireSpecialEncounter.Hunt, 2);

    private static BlackFlowSpireCombatSetup CreateHoppers(BlackFlowSpireMap map, string salt, BlackFlowSpireSpecialEncounter special, int count)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, salt);
        var seed = ((ulong)(uint)random.Next(int.MaxValue) << 32) | (uint)random.Next(int.MaxValue);
        var enemy = new BlackFlowSpireEnemySlot(ModelDb.Monster<ThievingHopper>().Id.ToString(), null);
        return new()
        {
            EncounterId = ModelDb.Encounter<ThievingHopperWeak>().Id.ToString(),
            Seed = seed, Enemies = Enumerable.Repeat(enemy, count).ToList(), Special = special
        };
    }
}
