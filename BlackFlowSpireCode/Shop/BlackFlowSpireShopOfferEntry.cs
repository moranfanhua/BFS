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

internal sealed class BlackFlowSpireShopOfferEntry : MerchantEntry
{
    private readonly BlackFlowSpireShopOfferStock _stock;
    private readonly bool _replicated;
    private List<CardCreationResult>? _cards;

    public BlackFlowSpireShopOfferKind Kind => _stock.Kind;
    public override bool IsStocked => !_stock.Purchased;
    public bool WasClaimed { get; private set; }
    public string TextKey => Kind switch
    {
        BlackFlowSpireShopOfferKind.RemoveCard => "UI.SHOP.OFFER.REMOVE_CARD",
        BlackFlowSpireShopOfferKind.UpgradeCard => "UI.SHOP.OFFER.UPGRADE_CARD",
        BlackFlowSpireShopOfferKind.PotionPack => "UI.SHOP.OFFER.POTION_PACK",
        _ => BlackFlowSpireCardPack.TextKey(Kind)
    };
    public string Title => BlackFlowSpireText.Get(TextKey + ".title");
    public string Description => BlackFlowSpireText.Get(TextKey + ".description");

    public BlackFlowSpireShopOfferEntry(Player player, BlackFlowSpireShopOfferStock stock, bool replicated = false) : base(player)
    {
        _stock = stock;
        _replicated = replicated;
        CalcCost();
    }

    public override void CalcCost() => _cost = Kind switch
    {
        BlackFlowSpireShopOfferKind.ColorlessPack => 100,
        BlackFlowSpireShopOfferKind.AttackPack => 40,
        BlackFlowSpireShopOfferKind.SkillPack => 60,
        BlackFlowSpireShopOfferKind.PowerPack => 80,
        BlackFlowSpireShopOfferKind.RemoveCard => 50,
        BlackFlowSpireShopOfferKind.UpgradeCard => 30,
        BlackFlowSpireShopOfferKind.PotionPack => 70,
        _ => throw new ArgumentOutOfRangeException()
    };

    protected override async Task<(bool, int)> OnTryPurchaseInternal(MerchantInventory? inventory, bool ignoreCost)
    {
        if (BlackFlowSpireTeam.IsGuest && !_replicated) return (false, 0);
        var price = ignoreCost ? 0 : Cost;
        if (!CanPay(price)) return (false, 0);
        // Buying and claiming are separate: commit payment and stock immediately.
        // Canceling the subsequent selection does not undo this purchase.
        _stock.Purchased = true;
        WasClaimed = false;
        await PlayerCmd.LoseGold(price, _player, GoldLossType.Spent);
        if (!_replicated) RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
        Persist();
        if (_replicated)
        {
            BlackFlowSpireTeam.IssueTickets((RunState)_player.RunState, Kind);
            OnMerchantInventoryUpdated();
            return (true, price);
        }
        if (LocalContext.IsMe(_player)) await Save();
        OnMerchantInventoryUpdated();
        if (Kind == BlackFlowSpireShopOfferKind.PotionPack)
        {
            var potions = PotionFactory.CreateRandomPotionsOutOfCombat(_player, 3, _player.PlayerRng.Shops)
                .Select(potion => potion.ToMutable()).ToList();
            await ClaimPotion(potions);
        }
        else if (Kind is BlackFlowSpireShopOfferKind.RemoveCard or BlackFlowSpireShopOfferKind.UpgradeCard)
        {
            var prompt = Kind == BlackFlowSpireShopOfferKind.RemoveCard
                ? CardSelectorPrefs.RemoveSelectionPrompt : CardSelectorPrefs.UpgradeSelectionPrompt;
            var prefs = new CardSelectorPrefs(prompt, 1) { Cancelable = true, RequireManualConfirmation = true };
            var selection = Kind == BlackFlowSpireShopOfferKind.RemoveCard
                ? await CardSelectCmd.FromDeckForRemoval(_player, prefs)
                : await CardSelectCmd.FromDeckForUpgrade(_player, prefs);
            var card = selection.FirstOrDefault();
            if (card != null)
            {
                if (Kind == BlackFlowSpireShopOfferKind.RemoveCard) await CardPileCmd.RemoveFromDeck(card);
                else CardCmd.Upgrade(card);
                WasClaimed = true;
            }
        }
        else
        {
            _cards ??= BlackFlowSpireCardPack.Draw(_player, Kind, _player.PlayerRng.Shops);
            var card = await BlackFlowSpireCardPackSelection.ChooseCard(_cards, _player);
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

    private async Task ClaimPotion(IReadOnlyList<PotionModel> potions)
    {
        var potion = await BlackFlowSpirePotionPackSelectionScreen.Choose(potions, async option =>
        {
            var result = await PotionCmd.TryToProcure(option, _player);
            if (result.success) return null;
            return BlackFlowSpireText.Get(result.failureReason == PotionProcureFailureReason.TooFull
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
        if (_player.RunState is not RunState state || !BlackFlowSpireGenerationService.IsEnabled(state)) return;
        BlackFlowSpireGenerationService.MarkChanged(state, BlackFlowSpireGenerationService.GetOrGenerate(state), BlackFlowSpireGenerationService.GetProgress(state));
    }

    internal Task Save() => _player.RunState is RunState state && BlackFlowSpireGenerationService.IsEnabled(state)
        ? BlackFlowSpirePartService.SaveAndRefresh(state) : Task.CompletedTask;

    protected override void ClearAfterPurchase()
    {
        _stock.Purchased = true;
        Persist();
    }

    protected override void RestockAfterPurchase(MerchantInventory? inventory)
    {
        // Like native removal, deck services remain single-use even with restock relics.
        _stock.Purchased = Kind is BlackFlowSpireShopOfferKind.RemoveCard or BlackFlowSpireShopOfferKind.UpgradeCard;
        _cards = null;
        Persist();
    }
}
