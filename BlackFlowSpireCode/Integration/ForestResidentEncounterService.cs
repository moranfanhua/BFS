using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

// Dedicated encounter entry points. Fill these two methods when the resident battles
// are authored; a null setup keeps entry/intelligence disabled, without drawing vanilla fights.
internal static class ForestResidentEncounterService
{
    internal static ForestCombatSetup? CreateStronghold(RunState state, ForestNode node, ulong seed) => null;
    internal static ForestCombatSetup? CreateRoamer(RunState state, ForestRoamingResident resident, ulong seed) => null;

    public static bool Populate(RunState state, ForestMap map)
    {
        var changed = false;
        ulong Seed(string category, int id)
        {
            var random = new StableRandom(map.Seed, map.ActIndex, $"resident-combat/v1/{category}/{id}");
            return ((ulong)(uint)random.Next(int.MaxValue) << 32) | (uint)random.Next(int.MaxValue);
        }
        foreach (var node in map.Nodes.Where(node => node.Content == ForestNodeContent.BattleSavage
                     && node.CombatSetup == null))
            if (CreateStronghold(state, node, Seed("stronghold", node.Id)) is { } setup)
            {
                node.CombatSetup = setup;
                changed = true;
            }
        foreach (var resident in map.RoamingResidents.Where(resident => resident.CombatSetup == null))
            if (CreateRoamer(state, resident, Seed("roamer", resident.Id)) is { } setup)
            {
                resident.CombatSetup = setup;
                changed = true;
            }
        return changed;
    }
}
