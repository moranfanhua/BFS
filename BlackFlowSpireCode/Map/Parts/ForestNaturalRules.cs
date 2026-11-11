using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Parts;

public static class ForestNaturalRules
{
    public static void PartObtained(IEnumerable<ForestNaturalPart> existingParts)
    {
        foreach (var part in existingParts)
            if (part.Kind == ForestNaturalPartKind.GrowingMoss) part.Adjust(1);
            else if (part.Kind == ForestNaturalPartKind.BoardVine) part.Adjust(4);
    }

    public static void NodesRevealed(ForestEngineInventory inventory, int count)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == ForestNaturalPartKind.FogTumbleweed))
            part.Adjust(count);
    }

    public static void DistanceMoved(ForestEngineInventory inventory, int distance)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == ForestNaturalPartKind.TravelFluff))
            part.Adjust(distance);
    }

    public static IReadOnlyList<int> JourneyFinished(ForestMap map, ForestEngineInventory inventory)
    {
        inventory.NaturalMovementCount++;
        foreach (var part in inventory.NaturalParts)
        {
            if (part.Kind == ForestNaturalPartKind.Wave)
            {
                var random = new StableRandom(map.Seed, map.ActIndex, $"natural-wave/{inventory.NaturalMovementCount}/{part.Id}");
                part.Appraisal = (int)Math.Clamp((long)part.Appraisal + (random.Next(2) == 0 ? -6 : 8), 0, 999);
            }
            else if (part.Kind == ForestNaturalPartKind.HomesickFruit) part.Adjust(-2);
        }
        NodeEntered(map, inventory, map.GetNode(map.PositionNodeId));
        return inventory.NaturalParts.Where(part => part.Kind == ForestNaturalPartKind.LifeAndDeath).Select(part => part.Id).ToArray();
    }

    public static void NodeEntered(ForestMap map, ForestEngineInventory inventory, ForestNode node)
    {
        var combat = node.Kind is ForestNodeKind.Combat or ForestNodeKind.Boss
            || map.RoamingResidents.Any(resident => resident.NodeId == node.Id);
        foreach (var part in inventory.NaturalParts)
        {
            if (!combat && part.Kind == ForestNaturalPartKind.BloodOrange) part.Adjust(2);
            if (!combat && node.Content == ForestNodeContent.Shop && part.Kind == ForestNaturalPartKind.UnfatedFruit)
                part.Adjust(10);
        }
    }

    public static void CardObtained(ForestEngineInventory inventory)
    {
        foreach (var part in inventory.NaturalParts.Where(part => part.Kind == ForestNaturalPartKind.EchoCorn)) part.Adjust(3);
    }

    public static void CombatFinished(ForestEngineInventory inventory, int targetLifeLost)
    {
        foreach (var part in inventory.NaturalParts.ToArray())
        {
            if (part.Kind == ForestNaturalPartKind.BloodMushroom) part.Adjust(2);
            else if (part.Kind == ForestNaturalPartKind.LoveGrass) part.Adjust(6);
            else if (part.Kind == ForestNaturalPartKind.FrostCrystalTree)
            {
                if (targetLifeLost == 0) part.Adjust(4);
                else inventory.DiscardNatural(part.Id);
            }
        }
    }
}
