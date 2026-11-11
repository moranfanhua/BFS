using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Exploration;

public static class ForestNodeLifecycle
{
    public static void Leave(ForestNode node)
    {
        if (!node.Repeatable) MakeEmpty(node, node.Kind != ForestNodeKind.Empty);
    }

    public static void MakeEmpty(ForestNode node, bool createdByCompletion)
    {
        node.CreatedByCompletion |= createdByCompletion;
        node.Kind = ForestNodeKind.Empty;
        node.Content = ForestNodeContent.Empty;
        node.CombatSetup = null;
        node.Details = null;
        node.CanPassThrough = false;
        node.Repeatable = false;
        node.PairedNodeId = null;
        node.VantageActivated = false;
        node.Completed = true;
    }
}
