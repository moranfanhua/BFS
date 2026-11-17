using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Parts;

public static class BlackFlowSpireConceptRules
{
    public static BlackFlowSpireConceptEffects OnProcessedArrival(BlackFlowSpireEngineInventory inventory, BlackFlowSpireNode node)
    {
        // Read the original node, not its fog icon or the roaming-resident overlay.
        var combat = node.Kind is BlackFlowSpireNodeKind.Combat or BlackFlowSpireNodeKind.Boss;
        var visualId = node.Details?.VisualId ?? node.Content switch
        {
            BlackFlowSpireNodeContent.Reward => BlackFlowSpireNodeVisuals.WISH,
            BlackFlowSpireNodeContent.Light => BlackFlowSpireNodeVisuals.LIGHT,
            BlackFlowSpireNodeContent.Rest => BlackFlowSpireNodeVisuals.REST,
            BlackFlowSpireNodeContent.Sacrifice => BlackFlowSpireNodeVisuals.SACRIFICE,
            _ => null
        };
        return Activate(inventory, definition => definition.Trigger switch
        {
            BlackFlowSpireConceptTrigger.Combat => combat,
            BlackFlowSpireConceptTrigger.NonCombat => !combat,
            BlackFlowSpireConceptTrigger.AnyNode => true,
            BlackFlowSpireConceptTrigger.Wish => visualId == BlackFlowSpireNodeVisuals.WISH,
            BlackFlowSpireConceptTrigger.Exchange => visualId == BlackFlowSpireNodeVisuals.SACRIFICE,
            BlackFlowSpireConceptTrigger.Vantage => visualId == BlackFlowSpireNodeVisuals.LIGHT,
            BlackFlowSpireConceptTrigger.Rest => visualId == BlackFlowSpireNodeVisuals.REST,
            _ => false
        });
    }

    public static BlackFlowSpireConceptEffects OnNaturalSold(BlackFlowSpireEngineInventory inventory, int appraisal) =>
        Activate(inventory, definition => appraisal >= 50 && definition.Trigger == BlackFlowSpireConceptTrigger.ValuableNaturalSale);

    private static BlackFlowSpireConceptEffects Activate(BlackFlowSpireEngineInventory inventory, Func<BlackFlowSpireConceptPartDefinition, bool> matches)
    {
        var effects = new BlackFlowSpireConceptEffects();
        foreach (var part in inventory.ConceptParts.ToArray())
        {
            var definition = BlackFlowSpireConceptParts.Get(part.Kind);
            if (part.RemainingActivations == 0 || !matches(definition)) continue;
            effects.AttackSpeed += definition.AttackSpeed;
            effects.VictoryRelics += definition.VictoryRelics;
            effects.Relics += definition.Relics;
            effects.RareRelics += definition.RareRelics;
            effects.Shield += definition.Shield;
            effects.ProcessedParts += definition.ProcessedParts;
            effects.RevealAll |= definition.RevealAll;
            if (part.RemainingActivations.HasValue && --part.RemainingActivations == 0) inventory.DiscardConcept(part.Id);
        }
        return effects;
    }
}
