using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Exploration;

public sealed class BlackFlowSpireFloorTransition
{
    public int SourceActIndex { get; set; }
    public int NodeId { get; set; }
    public int ActionPointBonus { get; set; }
    public int Hope { get; set; }
    public BlackFlowSpireProcessedPartKind Reward { get; set; }
}

public static class BlackFlowSpireFloorExitRules
{
    public static bool IsExitFloor(int actIndex) => BlackFlowSpireFloorPlan.Get(actIndex).ExitContent == BlackFlowSpireNodeContent.Final;
    public static bool IsChoice(BlackFlowSpireMap map, BlackFlowSpireNode node) => IsExitFloor(map.ActIndex)
        && node.Content is BlackFlowSpireNodeContent.Final or BlackFlowSpireNodeContent.Evacuate
        && BlackFlowSpireResidentRules.Occupant(map, node.Id) == null;
    public static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    public static void Populate(BlackFlowSpireMap map)
    {
        if (!IsExitFloor(map.ActIndex)) return;
        // Declining the black pool must not consume the only terminal exit.
        map.GetNode(map.BossNodeId).Repeatable = true;
        if (map.Nodes.Any(node => node.Content == BlackFlowSpireNodeContent.Evacuate)) return;
        var appearance = new StableRandom(map.Seed, map.ActIndex, "floor-shortcut/appearance/v1");
        if (appearance.Next(100) >= map.Options.ShortcutChancePercent) return;
        // Preserve node counts and the existing combat allocation. Each shortcut
        // replaces a regular event, or an original clearing when none is available.
        var candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
            && node.Content == BlackFlowSpireNodeContent.Event).OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0)
            candidates = map.Nodes.Where(node => node.Id != map.StartNodeId && node.Id != map.BossNodeId
                && node.Kind == BlackFlowSpireNodeKind.Empty).OrderBy(node => node.Id).ToArray();
        if (candidates.Length == 0) return;
        var node = candidates[new StableRandom(map.Seed, map.ActIndex, "floor-shortcut").Next(candidates.Length)];
        node.Kind = BlackFlowSpireNodeKind.Event;
        node.Content = BlackFlowSpireNodeContent.Evacuate;
        node.Details = new BlackFlowSpireNodeDetailsSource { VisualId = BlackFlowSpireNodeVisuals.EVACUATE };
    }

    public static string Background(BlackFlowSpireMap map, BlackFlowSpireNode node) => node.Content == BlackFlowSpireNodeContent.Final
        ? "pic_rogue_6_32" : "pic_rogue_6_" + (27 + new StableRandom(map.Seed, map.ActIndex, $"shortcut-cg/{node.Id}").Next(3));

    public static BlackFlowSpireProcessedPartKind Reward(BlackFlowSpireMap map, BlackFlowSpireNode node)
    {
        BlackFlowSpireProcessedPartKind[] precious = [BlackFlowSpireProcessedPartKind.DisposableJetpack,
            BlackFlowSpireProcessedPartKind.MotherSnowmelt, BlackFlowSpireProcessedPartKind.CannotsTentacle,
            BlackFlowSpireProcessedPartKind.EmergencyEjectionSeat, BlackFlowSpireProcessedPartKind.StructuralPrinciple,
            BlackFlowSpireProcessedPartKind.SimpleRemote, BlackFlowSpireProcessedPartKind.ForceFeedbackSteeringWheel,
            BlackFlowSpireProcessedPartKind.CannotExpress];
        var pool = node.Content == BlackFlowSpireNodeContent.Evacuate ? precious : BlackFlowSpireProcessedParts.All.Select(part => part.Kind).ToArray();
        return pool[new StableRandom(map.Seed, map.ActIndex, $"floor-exit-reward/{node.Id}").Next(pool.Length)];
    }
}
