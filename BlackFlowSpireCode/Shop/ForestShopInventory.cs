using System.Runtime.CompilerServices;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Parts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Runs;
using BlackFlowSpire.Persistence;

namespace BlackFlowSpire.Shop;

// Prepared once per native inventory, after its original hidden slots initialize.
// Reopening a picker or refreshing the UI never draws another pair of offers.
internal sealed class ForestShopInventory
{
    public const int RefreshPriceStepOriginium = 4;
    private static readonly ConditionalWeakTable<MerchantInventory, ForestShopInventory> Inventories = new();
    private readonly MerchantInventory _inventory;
    private readonly List<MerchantEntry> _subscribed = [];

    public IReadOnlyList<ForestShopOfferEntry> Offers { get; private set; } = [];
    public ForestShopPartEntry? Part { get; private set; }
    public IReadOnlyList<ForestShopPartEntry> SecretParts { get; private set; } = [];
    public bool IsSecret => SecretParts.Count > 0;
    public bool CanRefresh => Part != null || IsSecret;
    public int RefreshGold => checked(RefreshPriceStepOriginium
        * (1 + (Part?.PaidRefreshCount ?? SecretParts.FirstOrDefault()?.PaidRefreshCount ?? 0))
        * ForestPartService.GoldPerOriginium);

    public static ForestShopInventory Prepare(MerchantInventory inventory) =>
        Inventories.GetValue(inventory, static value => new ForestShopInventory(value));

    private ForestShopInventory(MerchantInventory inventory)
    {
        _inventory = inventory;
        ((IList<MerchantCardEntry>)inventory.CharacterCardEntries).Clear();
        ((IList<MerchantCardEntry>)inventory.ColorlessCardEntries).Clear();
        ((IList<MerchantPotionEntry>)inventory.PotionEntries).Clear();
        AccessTools.PropertySetter(typeof(MerchantInventory), nameof(MerchantInventory.CardRemovalEntry))
            .Invoke(inventory, [null]);

        SecretParts = ForestShopPartEntry.CreateSecret(inventory);
        if (IsSecret)
        {
            ((IList<MerchantRelicEntry>)inventory.RelicEntries).Clear();
        }
        else
        {
            Part = ForestShopPartEntry.Create(inventory);
            DrawOffers();
            // Keep the native three relics (including the shop relic), plus two random rarities.
            while (inventory.RelicEntries.Count < 5)
                inventory.AddRelicEntry(new MerchantRelicEntry(RelicFactory.RollRarity(inventory.Player), inventory.Player));
        }
        Subscribe();
    }

    private void DrawOffers(bool refresh = false)
    {
        List<ForestShopOfferStock> stocks;
        if (Part != null && _inventory.Player.RunState is RunState state)
        {
            var map = ForestGenerationService.GetOrGenerate(state);
            var progress = ForestGenerationService.GetProgress(state);
            if (refresh || !progress.ShopOffers.TryGetValue(map.PositionNodeId, out stocks!))
            {
                var kinds = Enum.GetValues<ForestShopOfferKind>().ToList();
                new StableRandom(map.Seed, map.ActIndex, $"trader-offers/{map.PositionNodeId}/{Part.RefreshCount}").Shuffle(kinds);
                stocks = kinds.Take(2).Select(kind => new ForestShopOfferStock { Kind = kind }).ToList();
                progress.ShopOffers[map.PositionNodeId] = stocks;
                ForestGenerationService.MarkChanged(state, map, progress);
            }
        }
        else
        {
            var kinds = Enum.GetValues<ForestShopOfferKind>().ToList();
            _inventory.Player.PlayerRng.Shops.Shuffle(kinds);
            stocks = kinds.Take(2).Select(kind => new ForestShopOfferStock { Kind = kind }).ToList();
        }
        Offers = stocks.Select(stock => new ForestShopOfferEntry(_inventory.Player, stock)).ToArray();
    }

    private void Subscribe()
    {
        _subscribed.AddRange(_inventory.RelicEntries);
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
            var map = ForestGenerationService.GetOrGenerate(state);
            var stock = ForestSecretShop.GetStock(state);
            var ids = ForestSecretShop.DrawStock(map, map.PositionNodeId, ++stock.RefreshCount);
            for (var index = 0; index < SecretParts.Count; index++) SecretParts[index].RefreshStock(paid, ids[index]);
        }
        else
        {
            Part!.RefreshStock(paid);
            DrawOffers(refresh: true);
            ((IList<MerchantRelicEntry>)_inventory.RelicEntries).Clear();
            for (var index = 0; index < 5; index++)
                _inventory.AddRelicEntry(new MerchantRelicEntry(index == 2 ? RelicRarity.Shop : RelicFactory.RollRarity(_inventory.Player), _inventory.Player));
        }
        Subscribe();
    }

    internal void ReadSharedStock()
    {
        foreach (var entry in _subscribed) entry.PurchaseCompleted -= RefreshEntries;
        _subscribed.Clear();
        if (IsSecret) SecretParts = ForestShopPartEntry.CreateSecret(_inventory);
        else
        {
            Part = ForestShopPartEntry.Create(_inventory);
            DrawOffers();
        }
        Subscribe();
    }

    private void RefreshEntries(PurchaseStatus _, MerchantEntry __)
    {
        foreach (var entry in _inventory.AllEntries) entry.OnMerchantInventoryUpdated();
        foreach (var offer in Offers) offer.OnMerchantInventoryUpdated();
        Part?.OnMerchantInventoryUpdated();
        foreach (var part in SecretParts) part.OnMerchantInventoryUpdated();
    }
}
