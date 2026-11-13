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

internal sealed class ForestShopPartEntry : MerchantEntry
{
    private readonly RunState _state;
    private readonly ForestMap _map;
    private readonly ForestExplorationProgress _progress;
    private readonly int _nodeId;
    private readonly ForestShopPartStock _stock;
    private readonly int? _secretSlot;

    public ForestShopPartDefinition Part => ForestShopParts.Get(_stock.PartId);
    public override bool IsStocked => !_stock.Purchased;
    public int RefreshCount => _stock.RefreshCount;
    public int PaidRefreshCount => _stock.PaidRefreshCount;
    public string NameKey => ForestText.PartKey(Part.Id) + ".name";
    public string Title => ForestText.Get(NameKey);
    public string Description => Part.Processed is { } processed ? ForestPartsUi.Description(processed)
        : Part.Natural is { } natural ? ForestPartsUi.NaturalDescription(natural)
        : ForestPartsUi.ConceptDescription(Part.Concept!.Value);
    public string ArtworkPath => $"res://BlackFlowSpire/Images/Parts/{Part.Id}.png";

    public static ForestShopPartEntry? Create(MerchantInventory inventory)
    {
        if (inventory.Player.RunState is not RunState state || !ForestGenerationService.IsEnabled(state)) return null;
        var map = ForestGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        if (node.Content != ForestNodeContent.Shop || node.Details?.VisualId == ForestNodeVisuals.SCRAP_SHOP) return null;
        var progress = ForestGenerationService.GetProgress(state);
        if (!progress.ShopParts.TryGetValue(node.Id, out var stock))
        {
            stock = new ForestShopPartStock { PartId = Roll(map, node.Id, 0).Id };
            progress.ShopParts.Add(node.Id, stock);
            ForestGenerationService.MarkChanged(state, map, progress);
        }
        return new ForestShopPartEntry(inventory.Player, state, map, progress, node.Id, stock);
    }

    public static IReadOnlyList<ForestShopPartEntry> CreateSecret(MerchantInventory inventory)
    {
        if (inventory.Player.RunState is not RunState state || !ForestSecretShop.IsCurrent(state)) return [];
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        return ForestSecretShop.GetStock(state).Goods.Select((stock, slot) =>
            new ForestShopPartEntry(inventory.Player, state, map, progress, map.PositionNodeId, stock, slot)).ToArray();
    }

    private ForestShopPartEntry(Player player, RunState state, ForestMap map, ForestExplorationProgress progress,
        int nodeId, ForestShopPartStock stock, int? secretSlot = null) : base(player)
    {
        _state = state;
        _map = map;
        _progress = progress;
        _nodeId = nodeId;
        _stock = stock;
        _secretSlot = secretSlot;
        CalcCost();
    }

    private static ForestShopPartDefinition Roll(ForestMap map, int nodeId, int restocks, int refreshes = 0)
    {
        // Stable per trader, also before the first checkpoint after displaying it.
        // UI refresh, reopening and restoring the room cannot reroll the item.
        var stream = refreshes == 0 ? $"trader-part/{nodeId}/{restocks}" : $"trader-part/{nodeId}/refresh/{refreshes}/{restocks}";
        var random = new StableRandom(map.Seed, map.ActIndex, stream);
        return ForestShopParts.All[random.Next(ForestShopParts.All.Count)];
    }

    public override void CalcCost() => _cost = checked(Part.PurchaseOriginium * ForestPartService.GoldPerOriginium);

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        if (!ForestTeam.CanManage(_state)) return (false, 0);
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
        ForestGenerationService.ModifyEngine(_state, part.Grant);
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
            ? ForestShopParts.All[new StableRandom(_map.Seed, _map.ActIndex,
                $"secret-restock/{_nodeId}/{slot}/{_stock.RefreshCount}/{_stock.RestockCount}").Next(ForestShopParts.All.Count)].Id
            : Roll(_map, _nodeId, _stock.RestockCount, _stock.RefreshCount).Id;
        _stock.Purchased = false;
        CalcCost();
        Persist();
    }

    private void Persist() => ForestGenerationService.MarkChanged(_state, _map, _progress);
    internal Task Save() => ForestPartService.SaveAndRefresh(_state);

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
