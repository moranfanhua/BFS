using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

internal static class ForestEncounterService
{
    private static readonly AccessTools.FieldRef<EncounterModel, Rng?> EncounterRng =
        AccessTools.FieldRefAccess<EncounterModel, Rng?>("_rng");

    public static bool Populate(RunState state, ForestMap map, ForestExplorationProgress? progress)
    {
        var changed = ForestResidentEncounterService.Populate(map);
        foreach (var node in map.Nodes.Where(node => node.Details == null && node.CombatSetup != null
                     && node.Content != ForestNodeContent.BattleSavage))
        {
            if (node.CombatSetup!.Stage is { } frozen)
            {
                node.Details = frozen.CreateDetails();
                changed = true;
                continue;
            }
            if (FindLegacyDefinition(node) is not { } definition) continue;
            node.Details = definition.CreateDetails(VariantFor(node));
            changed = true;
        }
        if (map.Nodes.All(node => node.Content is not (ForestNodeContent.NormalCombat
                or ForestNodeContent.EliteCombat or ForestNodeContent.Boss) || node.CombatSetup != null))
            return changed;
        if (map.StageCatalogVersion == 1)
            return PopulateStages(state, map) || changed;
        var rooms = state.Act.ToSave().SerializableRooms;
        var assignments = ForestCombatAssignments.Build(map, rooms.NormalEncounterIds.Select(id => id.ToString()).ToArray(),
            rooms.EliteEncounterIds.Select(id => id.ToString()).ToArray(), state.Act.BossEncounter.Id.ToString());
        foreach (var node in map.Nodes.Where(node => assignments.ContainsKey(node.Id) && node.CombatSetup == null))
        {
            var assignment = assignments[node.Id];
            var canonical = ModelDb.GetById<EncounterModel>(ModelId.Deserialize(assignment.EncounterId));
            var seed = assignment.Seed;
            var history = state.MapPointHistory.ElementAtOrDefault(state.CurrentActIndex);
            if (node.Completed && progress != null && progress.HistoryIndexes.TryGetValue(node.Id, out var index)
                && history != null && index < history.Count)
            {
                var room = history[index].Rooms.FirstOrDefault(room => room.ModelId != null && room.MonsterIds.Count > 0);
                if (room != null)
                {
                    node.CombatSetup = new()
                    {
                        EncounterId = room.ModelId!.ToString(), Seed = seed,
                        Enemies = room.MonsterIds.Select(id => new ForestEnemySlot(id.ToString(), null)).ToList()
                    };
                    continue;
                }
            }
            // A legacy save made before the current battle must resume its existing native roll.
            if (progress?.PendingNodeId == node.Id)
            {
                canonical = state.Act.PullNextEncounter(node.Content == ForestNodeContent.Boss ? RoomType.Boss
                    : node.Content == ForestNodeContent.EliteCombat ? RoomType.Elite : RoomType.Monster);
                seed = unchecked(state.Rng.Seed + (ulong)(state.TotalFloor + 1) + StringHelper.GetDeterministicHashCode(canonical.Id.Entry));
            }
            var encounter = canonical.ToMutable();
            EncounterRng(encounter) = new Rng(seed);
            encounter.GenerateMonstersWithSlots(state);
            node.CombatSetup = new()
            {
                EncounterId = canonical.Id.ToString(), Seed = seed,
                Enemies = encounter.MonstersWithSlots.Select(item => new ForestEnemySlot(item.Item1.Id.ToString(), item.Item2)).ToList()
            };
        }
        Entry.Logger.Info($"[Forest] Fixed enemy formations for act {map.ActIndex}: {assignments.Count} combat nodes.");
        foreach (var node in map.Nodes.Where(node => node.Details == null && node.CombatSetup != null
                     && node.Content != ForestNodeContent.BattleSavage))
            if (FindLegacyDefinition(node) is { } definition)
                node.Details = definition.CreateDetails(VariantFor(node));
        return true;
    }

    private static bool PopulateStages(RunState state, ForestMap map)
    {
        var assignments = ForestStageAssignments.Build(map, state.Act.Id.Entry, state.Act.BossEncounter.Id.ToString());
        foreach (var node in map.Nodes.Where(node => assignments.ContainsKey(node.Id) && node.CombatSetup == null))
        {
            var assignment = assignments[node.Id];
            node.CombatSetup = ForestEncounterFactory.CreateSetup(state, assignment.Stage, assignment.Seed);
            node.Details ??= assignment.Stage.CreateDetails();
        }
        Entry.Logger.Info($"[Forest] Fixed stage variants for {state.Act.Id.Entry}: {assignments.Count} nodes.");
        return true;
    }

    private static ForestBattleVariant VariantFor(ForestNode node) => node.Content switch
    {
        ForestNodeContent.Boss => ForestBattleVariant.Boss,
        ForestNodeContent.EliteCombat => ForestBattleVariant.Emergency,
        _ => ForestBattleVariant.Normal
    };

    private static ForestStageDefinition? FindLegacyDefinition(ForestNode node) => node.CombatSetup?.Stage is { } snapshot
        ? ForestStageCatalog.Find(snapshot.StageId)
        : ForestStageCatalog.FindByEncounter(node.CombatSetup!.EncounterId, VariantFor(node));

    public static EncounterModel CreateEncounter(RunState state, ForestCombatSetup setup)
    {
        var encounter = ForestEncounterFactory.Create(state, setup);
        Entry.Logger.Info($"[Forest] Enter fixed encounter {setup.EncounterId}: {string.Join(",", setup.Enemies.Select(enemy => enemy.MonsterId))}");
        return encounter;
    }
}
