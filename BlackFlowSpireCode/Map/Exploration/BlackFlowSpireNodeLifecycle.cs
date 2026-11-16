using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Exploration;

public static class BlackFlowSpireNodeLifecycle
{
    public static void Leave(BlackFlowSpireNode node)
    {
        if (!node.Repeatable) MakeEmpty(node, node.Kind != BlackFlowSpireNodeKind.Empty);
    }

    public static void MakeEmpty(BlackFlowSpireNode node, bool createdByCompletion)
    {
        node.CreatedByCompletion |= createdByCompletion;
        node.Kind = BlackFlowSpireNodeKind.Empty;
        node.Content = BlackFlowSpireNodeContent.Empty;
        node.CombatSetup = null;
        node.Details = null;
        node.CanPassThrough = false;
        node.Repeatable = false;
        node.PairedNodeId = null;
        node.VantageActivated = false;
        node.Completed = true;
    }
}
