using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Shop;

internal sealed class BlackFlowSpireShopCollectibleEntry : MerchantEntry
{
    private readonly RunState _state;
    private readonly BlackFlowSpireCollectibleStock _slot;
    internal BlackFlowSpireCollectible Item { get; }
    public override bool IsStocked => !_slot.Purchased && !BlackFlowSpireGenerationService.GetEngine(_state).Collectibles.Has(Item.Number);

    internal BlackFlowSpireShopCollectibleEntry(Player buyer, RunState state, BlackFlowSpireCollectibleStock slot) : base(buyer)
    {
        _state = state;
        _slot = slot;
        Item = BlackFlowSpireCollectibleCatalog.Find(slot.Number)!;
        CalcCost();
    }

    public override void CalcCost() => _cost = BlackFlowSpirePartService.GoldPerOriginium *
        (Item.Rarity switch { "SUPER_RARE" => 16, "RARE" => 12, _ => 8 });

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        var price = ignoreCost ? 0 : Cost;
        if (!IsStocked || _player.Gold < price) return (false, 0);
        _slot.Purchased = true;
        await PlayerCmd.LoseGold(price, _player, GoldLossType.Spent);
        var map = BlackFlowSpireGenerationService.GetOrGenerate(_state);
        BlackFlowSpireGenerationService.GrantCollectible(_state, map, Item);
        await BlackFlowSpireCollectibleService.SettleGold(_state, map, replicated: BlackFlowSpireTeam.IsOnline);
        return (true, price);
    }

    protected override void ClearAfterPurchase() => _slot.Purchased = true;
    protected override void RestockAfterPurchase(MerchantInventory? inventory) => ClearAfterPurchase();
}
