using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Generation;

namespace BlackFlowSpire.Map.Parts;

public enum BlackFlowSpireShopOfferKind { ColorlessPack, AttackPack, SkillPack, PowerPack, RemoveCard, UpgradeCard, PotionPack }

public sealed record BlackFlowSpireShopTicket(long Purchase, ulong Player, BlackFlowSpireShopOfferKind Kind,
    string[] Options, System.Text.Json.JsonElement[] Cards);

public sealed class BlackFlowSpireShopOfferStock
{
    public BlackFlowSpireShopOfferKind Kind { get; set; }
    public bool Purchased { get; set; }
}

public sealed class BlackFlowSpireCollectibleStock
{
    public required int Number { get; init; }
    public bool Purchased { get; set; }
}

public sealed class BlackFlowSpireSecretShopStock
{
    public List<BlackFlowSpireShopPartStock> Goods { get; set; } = [];
    public int RefreshCount { get; set; }
    public int SeedsCultivated { get; set; }
    // Rewards are already in the inventory. This receipt only resumes their presentation.
    public List<int> PendingHarvest { get; set; } = [];
}

internal static class BlackFlowSpireTraderStock
{
    internal static List<BlackFlowSpireCollectibleStock> RollCollectibles(string seed, int act, int node, int refresh, BlackFlowSpireEngineInventory engine)
    {
        var reserved = engine.PendingConceptRewards.SelectMany(reward => reward.CollectibleIds).ToHashSet();
        var pool = BlackFlowSpireCollectibleCatalog.All.Where(item => BlackFlowSpireCollectibleRules.CanRollRandomReward(item)
            && !engine.Collectibles.Has(item.Number) && !reserved.Contains(item.Id)).ToList();
        new StableRandom(seed, act, $"trader-collectibles/{node}/{refresh}").Shuffle(pool);
        return pool.Take(5).Select(item => new BlackFlowSpireCollectibleStock { Number = item.Number }).ToList();
    }
}
