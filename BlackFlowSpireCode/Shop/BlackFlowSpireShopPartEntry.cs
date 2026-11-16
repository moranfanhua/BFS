using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Shop;

internal sealed class BlackFlowSpireShopPartEntry : MerchantEntry
{
    private readonly RunState _state;
    private readonly BlackFlowSpireMap _map;
    private readonly BlackFlowSpireExplorationProgress _progress;
    private readonly int _nodeId;
    private readonly BlackFlowSpireShopPartStock _stock;
    private readonly int? _secretSlot;

    public BlackFlowSpireShopPartDefinition Part => BlackFlowSpireShopParts.Get(_stock.PartId);
    public override bool IsStocked => !_stock.Purchased;
    public int RefreshCount => _stock.RefreshCount;
    public int PaidRefreshCount => _stock.PaidRefreshCount;
    public string NameKey => BlackFlowSpireText.PartKey(Part.Id) + ".name";
    public string Title => BlackFlowSpireText.Get(NameKey);
    public string Description => Part.Processed is { } processed ? BlackFlowSpirePartsUi.Description(processed)
        : Part.Natural is { } natural ? BlackFlowSpirePartsUi.NaturalDescription(natural)
        : BlackFlowSpirePartsUi.ConceptDescription(Part.Concept!.Value);
    public string ArtworkPath => $"res://BlackFlowSpire/Images/Parts/{Part.Id}.png";

    public static BlackFlowSpireShopPartEntry? Create(MerchantInventory inventory)
    {
        if (inventory.Player.RunState is not RunState state || !BlackFlowSpireGenerationService.IsEnabled(state)) return null;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        if (node.Content != BlackFlowSpireNodeContent.Shop || node.Details?.VisualId == BlackFlowSpireNodeVisuals.SCRAP_SHOP) return null;
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!progress.ShopParts.TryGetValue(node.Id, out var stock))
        {
            stock = new BlackFlowSpireShopPartStock { PartId = Roll(map, node.Id, 0).Id };
            progress.ShopParts.Add(node.Id, stock);
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        }
        return new BlackFlowSpireShopPartEntry(inventory.Player, state, map, progress, node.Id, stock);
    }

    public static IReadOnlyList<BlackFlowSpireShopPartEntry> CreateSecret(MerchantInventory inventory)
    {
        if (inventory.Player.RunState is not RunState state || !BlackFlowSpireSecretShop.IsCurrent(state)) return [];
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        return BlackFlowSpireSecretShop.GetStock(state).Goods.Select((stock, slot) =>
            new BlackFlowSpireShopPartEntry(inventory.Player, state, map, progress, map.PositionNodeId, stock, slot)).ToArray();
    }

    private BlackFlowSpireShopPartEntry(Player player, RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress,
        int nodeId, BlackFlowSpireShopPartStock stock, int? secretSlot = null) : base(player)
    {
        _state = state;
        _map = map;
        _progress = progress;
        _nodeId = nodeId;
        _stock = stock;
        _secretSlot = secretSlot;
        CalcCost();
    }

    private static BlackFlowSpireShopPartDefinition Roll(BlackFlowSpireMap map, int nodeId, int restocks, int refreshes = 0)
    {
        // Stable per trader, also before the first checkpoint after displaying it.
        // UI refresh, reopening and restoring the room cannot reroll the item.
        var stream = refreshes == 0 ? $"trader-part/{nodeId}/{restocks}" : $"trader-part/{nodeId}/refresh/{refreshes}/{restocks}";
        var random = new StableRandom(map.Seed, map.ActIndex, stream);
        return BlackFlowSpireShopParts.All[random.Next(BlackFlowSpireShopParts.All.Count)];
    }

    public override void CalcCost() => _cost = checked(Part.PurchaseOriginium * BlackFlowSpirePartService.GoldPerOriginium);

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        if (!BlackFlowSpireTeam.CanManage(_state)) return (false, 0);
        var price = ignoreCost ? 0 : Cost;
        if (_player.Gold < price)
        {
            InvokePurchaseFailed(PurchaseStatus.FailureGold);
            return (false, 0);
        }
        var part = Part;
        _stock.Purchased = true;
        Persist();
        if (price > 0)
        {
            await PlayerCmd.LoseGold(price, _player, GoldLossType.Spent);
            RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
        }
        // Use the shared acquisition path, including moss/vine growth and the
        // three moss balls from GrowingMoss. Box capacity never blocks buying.
        BlackFlowSpireGenerationService.ModifyEngine(_state, part.Grant);
        OnMerchantInventoryUpdated();
        return (true, price);
    }

    protected override void ClearAfterPurchase()
    {
        _stock.Purchased = true;
        Persist();
    }

    protected override void RestockAfterPurchase(MerchantInventory? inventory)
    {
        // Native restock relics retain their behavior for this additional slot.
        _stock.RestockCount++;
        _stock.PartId = _secretSlot is { } slot
            ? BlackFlowSpireShopParts.All[new StableRandom(_map.Seed, _map.ActIndex,
                $"secret-restock/{_nodeId}/{slot}/{_stock.RefreshCount}/{_stock.RestockCount}").Next(BlackFlowSpireShopParts.All.Count)].Id
            : Roll(_map, _nodeId, _stock.RestockCount, _stock.RefreshCount).Id;
        _stock.Purchased = false;
        CalcCost();
        Persist();
    }

    private void Persist() => BlackFlowSpireGenerationService.MarkChanged(_state, _map, _progress);
    internal Task Save() => BlackFlowSpirePartService.SaveAndRefresh(_state);

    internal void RefreshStock(bool paid, string? partId = null)
    {
        _stock.RecordRefresh(paid);
        _stock.RestockCount = 0;
        _stock.PartId = partId ?? Roll(_map, _nodeId, 0, _stock.RefreshCount).Id;
        _stock.Purchased = false;
        CalcCost();
        Persist();
    }
}
