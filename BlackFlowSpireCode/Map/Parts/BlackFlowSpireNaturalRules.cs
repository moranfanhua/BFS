using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Parts;

public static class BlackFlowSpireNaturalRules
{
    public static void PartObtained(IEnumerable<BlackFlowSpireNaturalPart> existingParts)
    {
        foreach (var part in existingParts)
            if (part.Kind == BlackFlowSpireNaturalPartKind.GrowingMoss) part.Adjust(1);
            else if (part.Kind == BlackFlowSpireNaturalPartKind.BoardVine) part.Adjust(4);
    }

    public static void NodesRevealed(BlackFlowSpireEngineInventory inventory, int count)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == BlackFlowSpireNaturalPartKind.FogTumbleweed))
            part.Adjust(count);
    }

    public static void DistanceMoved(BlackFlowSpireEngineInventory inventory, int distance)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == BlackFlowSpireNaturalPartKind.TravelFluff))
            part.Adjust(distance);
    }

    public static IReadOnlyList<int> JourneyFinished(BlackFlowSpireMap map, BlackFlowSpireEngineInventory inventory)
    {
        inventory.NaturalMovementCount++;
        foreach (var part in inventory.NaturalParts)
        {
            if (part.Kind == BlackFlowSpireNaturalPartKind.Wave)
            {
                var random = new StableRandom(map.Seed, map.ActIndex, $"natural-wave/{inventory.NaturalMovementCount}/{part.Id}");
                part.Appraisal = (int)Math.Clamp((long)part.Appraisal + (random.Next(2) == 0 ? -6 : 8), 0, 999);
            }
            else if (part.Kind == BlackFlowSpireNaturalPartKind.HomesickFruit) part.Adjust(-2);
        }
        NodeEntered(map, inventory, map.GetNode(map.PositionNodeId));
        return inventory.NaturalParts.Where(part => part.Kind == BlackFlowSpireNaturalPartKind.LifeAndDeath).Select(part => part.Id).ToArray();
    }

    public static void NodeEntered(BlackFlowSpireMap map, BlackFlowSpireEngineInventory inventory, BlackFlowSpireNode node)
    {
        var combat = node.Kind is BlackFlowSpireNodeKind.Combat or BlackFlowSpireNodeKind.Boss
            || map.RoamingResidents.Any(resident => resident.NodeId == node.Id);
        foreach (var part in inventory.NaturalParts)
        {
            if (!combat && part.Kind == BlackFlowSpireNaturalPartKind.BloodOrange) part.Adjust(2);
            if (!combat && node.Content == BlackFlowSpireNodeContent.Shop && part.Kind == BlackFlowSpireNaturalPartKind.UnfatedFruit)
                part.Adjust(10);
        }
    }

    public static void CardObtained(BlackFlowSpireEngineInventory inventory)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == BlackFlowSpireNaturalPartKind.EchoCorn)) part.Adjust(3);
    }

    public static void CombatFinished(BlackFlowSpireEngineInventory inventory, int targetLifeLost)
    {
        foreach (var part in inventory.NaturalParts.ToArray())
        {
            if (part.Kind == BlackFlowSpireNaturalPartKind.BloodMushroom) part.Adjust(2);
            else if (part.Kind == BlackFlowSpireNaturalPartKind.LoveGrass) part.Adjust(6);
            else if (part.Kind == BlackFlowSpireNaturalPartKind.FrostCrystalTree)
            {
                if (targetLifeLost == 0) part.Adjust(4);
                else inventory.DiscardNatural(part.Id);
            }
        }
    }
}
