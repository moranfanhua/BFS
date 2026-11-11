using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Exploration;

public sealed class ForestFloorTransition
{
    public int SourceActIndex { get; set; }
    public int NodeId { get; set; }
    public int ActionPointBonus { get; set; }
    public int Hope { get; set; }
    public ForestProcessedPartKind Reward { get; set; }
}

public static class ForestFloorExitRules
{
    public static bool IsExitFloor(int actIndex) => ForestFloorPlan.Get(actIndex).ExitContent == ForestNodeContent.Final;
    public static bool IsChoice(ForestMap map, ForestNode node) => IsExitFloor(map.ActIndex)
        && node.Content is ForestNodeContent.Final or ForestNodeContent.Evacuate
        && ForestResidentRules.Occupant(map, node.Id) == null;
    public static bool IsPending(ForestMap map, ForestExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    public static void Populate(ForestMap map)
    {
        if (!IsExitFloor(map.ActIndex)) return;
        // Declining the black pool must not consume the only terminal exit.
        map.GetNode(map.BossNodeId).Repeatable = true;
        if (map.Nodes.Any(node => node.Content == ForestNodeContent.Evacuate)) return;
        var appearance = new StableRandom(map.Seed, map.ActIndex, "floor-shortcut/appearance/v1");
        if (appearance.Next(100) >= map.Options.ShortcutChancePercent) return;
        // Preserve node counts and the existing combat allocation. Each shortcut
        // replaces a regular event, or an original clearing when none is available.
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && node.Content == ForestNodeContent.Event).OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0)
            candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
                && node.Kind == ForestNodeKind.Empty).OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        var node = candidates[new StableRandom(map.Seed, map.ActIndex, "floor-shortcut").Next(candidates.Length)];
        node.Kind = ForestNodeKind.Event;
        node.Content = ForestNodeContent.Evacuate;
        node.Details = new ForestNodeDetailsSource { VisualId = ForestNodeVisuals.EVACUATE };
    }

    public static string Background(ForestMap map, ForestNode node) => node.Content == ForestNodeContent.Final
        ? "pic_rogue_6_32" : "pic_rogue_6_" + (27 + new StableRandom(map.Seed, map.ActIndex, $"shortcut-cg/{node.Id}").Next(3));

    public static ForestProcessedPartKind Reward(ForestMap map, ForestNode node)
    {
        ForestProcessedPartKind[] precious = [ForestProcessedPartKind.DisposableJetpack,
            ForestProcessedPartKind.MotherSnowmelt, ForestProcessedPartKind.CannotsTentacle,
            ForestProcessedPartKind.EmergencyEjectionSeat, ForestProcessedPartKind.StructuralPrinciple,
            ForestProcessedPartKind.SimpleRemote, ForestProcessedPartKind.ForceFeedbackSteeringWheel,
            ForestProcessedPartKind.CannotExpress];
        var pool = node.Content == ForestNodeContent.Evacuate ? precious : ForestProcessedParts.All.Select(part => part.Kind).ToArray();
        return pool[new StableRandom(map.Seed, map.ActIndex, $"floor-exit-reward/{node.Id}").Next(pool.Length)];
    }
}
