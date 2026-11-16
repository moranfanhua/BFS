using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Shop;

internal sealed class ForestShopOfferEntry : MerchantEntry
{
    private readonly ForestShopOfferStock _stock;
    private readonly bool _replicated;
    private List<CardCreationResult>? _cards;
    private List<PotionModel>? _deferredPotions;

    public ForestShopOfferKind Kind => _stock.Kind;
    public override bool IsStocked => !_stock.Purchased;
    public bool WasClaimed { get; private set; }
    public string TextKey => Kind switch
    {
        ForestShopOfferKind.RemoveCard => "UI.SHOP.OFFER.REMOVE_CARD",
        ForestShopOfferKind.UpgradeCard => "UI.SHOP.OFFER.UPGRADE_CARD",
        ForestShopOfferKind.PotionPack => "UI.SHOP.OFFER.POTION_PACK",
        _ => ForestCardPack.TextKey(Kind)
    };
    public string Title => ForestText.Get(TextKey + ".title");
    public string Description => ForestText.Get(TextKey + ".description");

    public ForestShopOfferEntry(Player player, ForestShopOfferStock stock, bool replicated = false) : base(player)
    {
        _stock = stock;
        _replicated = replicated;
        CalcCost();
    }

    public override void CalcCost() => _cost = Kind switch
    {
        ForestShopOfferKind.ColorlessPack => 100,
        ForestShopOfferKind.AttackPack => 40,
        ForestShopOfferKind.SkillPack => 60,
        ForestShopOfferKind.PowerPack => 80,
        ForestShopOfferKind.RemoveCard => 50,
        ForestShopOfferKind.UpgradeCard => 30,
        ForestShopOfferKind.PotionPack => 70,
        _ => throw new ArgumentOutOfRangeException()
    };

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        if (ForestTeam.IsGuest && !_replicated) return (false, 0);
        var price = ignoreCost ? 0 : Cost;
        if (!CanPay(price)) return (false, 0);
        // Buying and claiming are separate: commit payment and stock immediately.
        // Canceling the subsequent selection does not undo this purchase.
        _stock.Purchased = true;
        WasClaimed = false;
        await PlayerCmd.LoseGold(price, _player, GoldLossType.Spent);
        if (!_replicated) RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
        Persist();
        if (LocalContext.IsMe(_player)) await Save();
        OnMerchantInventoryUpdated();
        if (Kind == ForestShopOfferKind.PotionPack)
        {
            var potions = PotionFactory.CreateRandomPotionsOutOfCombat(_player, 3, _player.PlayerRng.Shops)
                .Select(potion => potion.ToMutable()).ToList();
            // Potion discards use the native action queue. Open this picker after
            // the synchronized purchase so that queue stays available.
            if (!_replicated) await ClaimPotion(potions);
            else if (LocalContext.IsMe(_player)) _deferredPotions = potions;
        }
        else if (Kind is ForestShopOfferKind.RemoveCard or ForestShopOfferKind.UpgradeCard)
        {
            var prompt = Kind == ForestShopOfferKind.RemoveCard
                ? CardSelectorPrefs.RemoveSelectionPrompt : CardSelectorPrefs.UpgradeSelectionPrompt;
            var prefs = new CardSelectorPrefs(prompt, 1) { Cancelable = true, RequireManualConfirmation = true };
            var selection = Kind == ForestShopOfferKind.RemoveCard
                ? await CardSelectCmd.FromDeckForRemoval(_player, prefs)
                : await CardSelectCmd.FromDeckForUpgrade(_player, prefs);
            var card = selection.FirstOrDefault();
            if (card != null)
            {
                if (Kind == ForestShopOfferKind.RemoveCard) await CardPileCmd.RemoveFromDeck(card);
                else CardCmd.Upgrade(card);
                WasClaimed = true;
            }
        }
        else
        {
            _cards ??= ForestCardPack.Draw(_player, Kind, _player.PlayerRng.Shops);
            var card = await ForestCardPackSelection.ChooseCard(_cards, _player);
            if (card != null)
            {
                if ((await CardPileCmd.Add(card, PileType.Deck)).success)
                {
                    WasClaimed = true;
                    if (!_replicated) RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(card);
                    if (card.Pool.IsColorless)
                        _player.RunState.CurrentMapPointHistoryEntry?.GetEntry(_player.NetId).BoughtColorless.Add(card.Id);
                }
            }
        }
        return (true, price);
    }

    internal async Task CompleteDeferredClaim()
    {
        if (_deferredPotions is not { } potions) return;
        _deferredPotions = null;
        await ClaimPotion(potions);
    }

    private async Task ClaimPotion(IReadOnlyList<PotionModel> potions)
    {
        var potion = await ForestPotionPackSelectionScreen.Choose(potions, async option =>
        {
            var result = await PotionCmd.TryToProcure(option, _player);
            if (result.success) return null;
            return ForestText.Get(result.failureReason == PotionProcureFailureReason.TooFull
                ? "UI.SHOP.potion_full" : "UI.SHOP.potion_forbidden");
        });
        if (potion == null) return;
        WasClaimed = true;
        _player.RunState.CurrentMapPointHistoryEntry?.GetEntry(_player.NetId).BoughtPotions.Add(potion.Id);
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedPotion(potion);
    }

    private bool CanPay(int price)
    {
        if (_player.Gold >= price) return true;
        InvokePurchaseFailed(PurchaseStatus.FailureGold);
        return false;
    }

    private void Persist()
    {
        if (_player.RunState is not RunState state || !ForestGenerationService.IsEnabled(state)) return;
        ForestGenerationService.MarkChanged(state, ForestGenerationService.GetOrGenerate(state), ForestGenerationService.GetProgress(state));
    }

    internal Task Save() => _player.RunState is RunState state && ForestGenerationService.IsEnabled(state)
        ? ForestPartService.SaveAndRefresh(state) : Task.CompletedTask;

    protected override void ClearAfterPurchase()
    {
        _stock.Purchased = true;
        Persist();
    }

    protected override void RestockAfterPurchase(MerchantInventory? inventory)
    {
        // Like native removal, deck services remain single-use even with restock relics.
        _stock.Purchased = Kind is ForestShopOfferKind.RemoveCard or ForestShopOfferKind.UpgradeCard;
        _cards = null;
        Persist();
    }
}
