using System.Runtime.CompilerServices;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Parts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
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
        if (IsSecret)
        {
            ((IList<MerchantRelicEntry>)inventory.RelicEntries).Clear();
        }
        else
        {
            Part = BlackFlowSpireShopPartEntry.Create(inventory);
            DrawOffers();
            // Keep the native three relics (including the shop relic), plus two random rarities.
            while (inventory.RelicEntries.Count < 5)
                AddRelic(RelicFactory.RollRarity(inventory.Player.PlayerRng.Shops));
        }
        Subscribe();
    }

    private void DrawOffers(bool refresh = false)
    {
        List<BlackFlowSpireShopOfferStock> stocks;
        if (Part != null && _inventory.Player.RunState is RunState state)
        {
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            var progress = BlackFlowSpireGenerationService.GetProgress(state);
            if (refresh || !progress.ShopOffers.TryGetValue(map.PositionNodeId, out stocks!))
            {
                var kinds = Enum.GetValues<BlackFlowSpireShopOfferKind>().ToList();
                new StableRandom(map.Seed, map.ActIndex, $"trader-offers/{map.PositionNodeId}/{Part.RefreshCount}").Shuffle(kinds);
                stocks = kinds.Take(2).Select(kind => new BlackFlowSpireShopOfferStock { Kind = kind }).ToList();
                progress.ShopOffers[map.PositionNodeId] = stocks;
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            }
        }
        else
        {
            var kinds = Enum.GetValues<BlackFlowSpireShopOfferKind>().ToList();
            kinds.UnstableShuffle(_inventory.Player.PlayerRng.Shops);
            stocks = kinds.Take(2).Select(kind => new BlackFlowSpireShopOfferStock { Kind = kind }).ToList();
        }
        Offers = stocks.Select(stock => new BlackFlowSpireShopOfferEntry(_inventory.Player, stock)).ToArray();
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
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            var stock = BlackFlowSpireSecretShop.GetStock(state);
            var ids = BlackFlowSpireSecretShop.DrawStock(map, map.PositionNodeId, ++stock.RefreshCount);
            for (var index = 0; index < SecretParts.Count; index++) SecretParts[index].RefreshStock(paid, ids[index]);
        }
        else
        {
            Part!.RefreshStock(paid);
            DrawOffers(refresh: true);
            ((IList<MerchantRelicEntry>)_inventory.RelicEntries).Clear();
            for (var index = 0; index < 5; index++)
                AddRelic(index == 2 ? RelicRarity.Shop : RelicFactory.RollRarity(_inventory.Player.PlayerRng.Shops));
        }
        Subscribe();
    }

    private void AddRelic(RelicRarity rarity) =>
        ((IList<MerchantRelicEntry>)_inventory.RelicEntries).Add(new MerchantRelicEntry(rarity, _inventory.Player));

    internal void ReadSharedStock()
    {
        foreach (var entry in _subscribed) entry.PurchaseCompleted -= RefreshEntries;
        _subscribed.Clear();
        if (IsSecret) SecretParts = BlackFlowSpireShopPartEntry.CreateSecret(_inventory);
        else
        {
            Part = BlackFlowSpireShopPartEntry.Create(_inventory);
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
