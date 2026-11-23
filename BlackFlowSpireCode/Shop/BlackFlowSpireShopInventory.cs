using System.Runtime.CompilerServices;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Parts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Runs;
using BlackFlowSpire.Persistence;

namespace BlackFlowSpire.Shop;

// Prepared once per native inventory, after its original hidden slots initialize.
// Reopening a picker or refreshing the UI never draws another pair of offers.
internal sealed class BlackFlowSpireShopInventory
{
    public const int RefreshPriceStepOriginium = 4;
    private static readonly ConditionalWeakTable<MerchantInventory, BlackFlowSpireShopInventory> Inventories = new();
    private readonly MerchantInventory _inventory;
    private readonly List<MerchantEntry> _subscribed = [];

    public IReadOnlyList<BlackFlowSpireShopOfferEntry> Offers { get; private set; } = [];
    public BlackFlowSpireShopPartEntry? Part { get; private set; }
    public IReadOnlyList<BlackFlowSpireShopPartEntry> SecretParts { get; private set; } = [];
    public IReadOnlyList<BlackFlowSpireShopCollectibleEntry> Collectibles { get; private set; } = [];
    public bool IsSecret => SecretParts.Count > 0;
    public bool CanRefresh => Part != null || IsSecret;
    public int RefreshGold => checked(RefreshPriceStepOriginium
        * (1 + (Part?.PaidRefreshCount ?? SecretParts.FirstOrDefault()?.PaidRefreshCount ?? 0))
        * BlackFlowSpirePartService.GoldPerOriginium);

    public static BlackFlowSpireShopInventory Prepare(MerchantInventory inventory) =>
        Inventories.GetValue(inventory, static value => new BlackFlowSpireShopInventory(value));

    private BlackFlowSpireShopInventory(MerchantInventory inventory)
    {
        _inventory = inventory;
        ((IList<MerchantCardEntry>)inventory.CharacterCardEntries).Clear();
        ((IList<MerchantCardEntry>)inventory.ColorlessCardEntries).Clear();
        ((IList<MerchantPotionEntry>)inventory.PotionEntries).Clear();
        AccessTools.PropertySetter(typeof(MerchantInventory), nameof(MerchantInventory.CardRemovalEntry))
            .Invoke(inventory, [null]);

        SecretParts = BlackFlowSpireShopPartEntry.CreateSecret(inventory);
        if (!IsSecret)
        {
            Part = BlackFlowSpireShopPartEntry.Create(inventory);
            DrawOffers();
            ReadCollectibles();
        }
        Subscribe();
    }

    private void DrawOffers(bool refresh = false)
    {
        var state = (RunState)_inventory.Player.RunState;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (refresh || !progress.ShopOffers.TryGetValue(map.PositionNodeId, out var stocks))
        {
            var kinds = Enum.GetValues<BlackFlowSpireShopOfferKind>().ToList();
            new StableRandom(map.Seed, map.ActIndex, $"trader-offers/{map.PositionNodeId}/{Part?.RefreshCount ?? 0}").Shuffle(kinds);
            stocks = kinds.Take(2).Select(kind => new BlackFlowSpireShopOfferStock { Kind = kind }).ToList();
            progress.ShopOffers[map.PositionNodeId] = stocks;
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        }
        Offers = stocks.Select(stock => new BlackFlowSpireShopOfferEntry(_inventory.Player, stock)).ToArray();
    }

    private void Subscribe()
    {
        _subscribed.AddRange(Collectibles);
        _subscribed.AddRange(Offers);
        if (Part != null) _subscribed.Add(Part);
        _subscribed.AddRange(SecretParts);
        foreach (var entry in _subscribed) entry.PurchaseCompleted += RefreshEntries;
    }

    public void RefreshStock(bool paid)
    {
        if (!CanRefresh) throw new InvalidOperationException("This trader does not support refreshing stock.");
        foreach (var entry in _subscribed) entry.PurchaseCompleted -= RefreshEntries;
        _subscribed.Clear();
        if (IsSecret)
        {
            var state = (RunState)_inventory.Player.RunState;
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            var stock = BlackFlowSpireSecretShop.GetStock(state);
            var ids = BlackFlowSpireSecretShop.DrawStock(map, map.PositionNodeId, ++stock.RefreshCount);
            for (var index = 0; index < SecretParts.Count; index++) SecretParts[index].RefreshStock(paid, ids[index]);
        }
        else
        {
            Part!.RefreshStock(paid);
            DrawOffers(refresh: true);
            ReadCollectibles(refresh: true);
        }
        Subscribe();
    }

    private void ReadCollectibles(bool refresh = false)
    {
        var state = (RunState)_inventory.Player.RunState;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (refresh || !progress.ShopCollectibles.TryGetValue(map.PositionNodeId, out var stocks))
        {
            var engine = BlackFlowSpireGenerationService.GetEngine(state);
            stocks = BlackFlowSpireTraderStock.RollCollectibles(map.Seed, map.ActIndex, map.PositionNodeId, Part?.RefreshCount ?? 0, engine);
            progress.ShopCollectibles[map.PositionNodeId] = stocks;
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        }
        Collectibles = stocks.Select(slot => new BlackFlowSpireShopCollectibleEntry(_inventory.Player, state, slot)).ToArray();
    }

    internal void ReadSharedStock()
    {
        foreach (var entry in _subscribed) entry.PurchaseCompleted -= RefreshEntries;
        _subscribed.Clear();
        if (IsSecret) SecretParts = BlackFlowSpireShopPartEntry.CreateSecret(_inventory);
        else
        {
            Part = BlackFlowSpireShopPartEntry.Create(_inventory);
            DrawOffers();
            ReadCollectibles();
        }
        Subscribe();
    }

    private void RefreshEntries(PurchaseStatus _, MerchantEntry __)
    {
        foreach (var entry in _inventory.AllEntries) entry.OnMerchantInventoryUpdated();
        foreach (var collectible in Collectibles) collectible.OnMerchantInventoryUpdated();
        foreach (var offer in Offers) offer.OnMerchantInventoryUpdated();
        Part?.OnMerchantInventoryUpdated();
        foreach (var part in SecretParts) part.OnMerchantInventoryUpdated();
    }
}
