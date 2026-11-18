using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Exploration;

public sealed record BlackFlowSpireWishProgress(bool Refreshed = false, string? ClaimedId = null);

internal static class BlackFlowSpireWishRules
{
    internal const int RefreshPrice = 4;
    internal const int Refresh = -1;
    internal const int Leave = 0;

    internal static bool IsChoice(BlackFlowSpireMap map, BlackFlowSpireNode node) =>
        node.Content == BlackFlowSpireNodeContent.Reward && !node.Completed && BlackFlowSpireResidentRules.Occupant(map, node.Id) == null;

    internal static bool IsPending(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress) =>
        progress.PendingNodeId == map.PositionNodeId && IsChoice(map, map.GetNode(map.PositionNodeId));

    internal static BlackFlowSpireCollectible[] Offers(BlackFlowSpireMap map, BlackFlowSpireEngineInventory engine, bool refreshed)
    {
        var reserved = engine.PendingConceptRewards.SelectMany(reward => reward.CollectibleIds).ToHashSet(StringComparer.Ordinal);
        var candidates = BlackFlowSpireCollectibleCatalog.All.Where(item => BlackFlowSpireCollectibleRules.CanRollRandomReward(item)
            && !engine.Collectibles.Has(item.Number) && !reserved.Contains(item.Id)).ToList();
        var count = engine.Collectibles.Has(113) ? 3 : 2;
        var random = new StableRandom(map.Seed, map.ActIndex, $"wish/v1/{map.PositionNodeId}/{(refreshed ? 1 : 0)}");
        random.Shuffle(candidates);
        if (!refreshed) return candidates.OrderBy(item => item.Rarity == "SUPER_RARE").Take(count).ToArray();

        var previous = Offers(map, engine, refreshed: false).Select(item => item.Number).ToHashSet();
        var upgraded = candidates.Where(item => !previous.Contains(item.Number) && item.Rarity != "NORMAL")
            .OrderByDescending(item => item.Rarity == "SUPER_RARE").Take(count).ToArray();
        return upgraded.Length > 0 && upgraded[0].Rarity == "SUPER_RARE" ? upgraded : [];
    }

    internal static BlackFlowSpireCollectible? Claimable(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireEngineInventory engine, int number) => IsPending(map, progress) && progress.Wish?.ClaimedId == null
            ? Offers(map, engine, progress.Wish?.Refreshed == true).FirstOrDefault(item => item.Number == number) : null;

    internal static bool CanRefresh(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory engine) =>
        IsPending(map, progress) && progress.Wish == null && Offers(map, engine, refreshed: true).Length > 0;

    internal static bool TryLeave(BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireEngineInventory engine)
    {
        if (!IsPending(map, progress) || progress.Wish?.ClaimedId == null
            && Offers(map, engine, progress.Wish?.Refreshed == true).Length > 0) return false;
        BlackFlowSpireTravelRules.CompleteEncounter(map, progress);
        progress.Wish = null;
        return true;
    }
}
