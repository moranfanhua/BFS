using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

internal static class BlackFlowSpireEncounterService
{
    public static void Populate(RunState state, BlackFlowSpireMap map)
    {
        var assignments = BlackFlowSpireStageAssignments.Build(map, state.Act.Id.Entry, state.Act.BossEncounter.Id.ToString());
        foreach (var (nodeId, assignment) in assignments)
        {
            var node = map.GetNode(nodeId);
            node.CombatSetup = BlackFlowSpireEncounterFactory.CreateSetup(state, assignment.Stage, assignment.Seed);
            node.Details ??= assignment.Stage.CreateDetails();
        }
        BlackFlowSpireResidentEncounterService.Populate(map);
        Entry.Logger.Info($"[BlackFlowSpire] Fixed stage variants for {state.Act.Id.Entry}: {assignments.Count} nodes.");
    }

    public static EncounterModel CreateEncounter(RunState state, BlackFlowSpireCombatSetup setup)
    {
        var encounter = BlackFlowSpireEncounterFactory.Create(state, setup);
        Entry.Logger.Info($"[BlackFlowSpire] Enter fixed encounter {setup.EncounterId}: {string.Join(",", setup.Enemies.Select(enemy => enemy.MonsterId))}");
        return encounter;
    }
}
