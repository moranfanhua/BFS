namespace BlackFlowSpire.Map.Parts;

public enum BlackFlowSpireShopOfferKind { ColorlessPack, AttackPack, SkillPack, PowerPack, RemoveCard, UpgradeCard, PotionPack }

public sealed class BlackFlowSpireShopOfferStock
{
    public BlackFlowSpireShopOfferKind Kind { get; set; }
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
