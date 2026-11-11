using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Parts;

public static class ForestConceptRules
{
    public static ForestConceptEffects OnProcessedArrival(ForestEngineInventory inventory, ForestNode node)
    {
        // Read the original node, not its fog icon or the roaming-resident overlay.
        var combat = node.Kind is ForestNodeKind.Combat or ForestNodeKind.Boss;
        var visualId = node.Details?.VisualId ?? node.Content switch
        {
            ForestNodeContent.Reward => ForestNodeVisuals.WISH,
            ForestNodeContent.Light => ForestNodeVisuals.LIGHT,
            _ => null
        };
        return Activate(inventory, definition => definition.Trigger switch
        {
            ForestConceptTrigger.Combat => combat,
            ForestConceptTrigger.NonCombat => !combat,
            ForestConceptTrigger.AnyNode => true,
            ForestConceptTrigger.Wish => visualId == ForestNodeVisuals.WISH,
            ForestConceptTrigger.Exchange => visualId == ForestNodeVisuals.SACRIFICE,
            ForestConceptTrigger.Vantage => visualId == ForestNodeVisuals.LIGHT,
            ForestConceptTrigger.Rest => visualId == ForestNodeVisuals.REST,
            _ => false
        });
    }

    public static ForestConceptEffects OnNaturalSold(ForestEngineInventory inventory, int appraisal) =>
        Activate(inventory, definition => appraisal >= 50 && definition.Trigger == ForestConceptTrigger.ValuableNaturalSale);

    private static ForestConceptEffects Activate(ForestEngineInventory inventory, Func<ForestConceptPartDefinition, bool> matches)
    {
        var effects = new ForestConceptEffects();
        foreach (var part in inventory.ConceptParts.ToArray())
        {
            var definition = ForestConceptParts.Get(part.Kind);
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
