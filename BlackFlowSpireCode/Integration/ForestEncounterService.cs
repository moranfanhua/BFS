using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

internal static class ForestEncounterService
{
    public static void Populate(RunState state, ForestMap map)
    {
        var assignments = ForestStageAssignments.Build(map, state.Act.Id.Entry, state.Act.BossEncounter.Id.ToString());
        foreach (var (nodeId, assignment) in assignments)
        {
            var node = map.GetNode(nodeId);
            node.CombatSetup = ForestEncounterFactory.CreateSetup(state, assignment.Stage, assignment.Seed);
            node.Details ??= assignment.Stage.CreateDetails();
        }
        ForestResidentEncounterService.Populate(map);
        Entry.Logger.Info($"[Forest] Fixed stage variants for {state.Act.Id.Entry}: {assignments.Count} nodes.");
    }

    public static EncounterModel CreateEncounter(RunState state, ForestCombatSetup setup)
    {
        var encounter = ForestEncounterFactory.Create(state, setup);
        Entry.Logger.Info($"[Forest] Enter fixed encounter {setup.EncounterId}: {string.Join(",", setup.Enemies.Select(enemy => enemy.MonsterId))}");
        return encounter;
    }
}
