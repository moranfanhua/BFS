using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace BlackFlowSpire.Combat;

internal sealed class ForestBattleCardPackReward(Player player, ForestShopOfferKind kind, CardCreationOptions options,
    IReadOnlySet<CardRarity> available, CardModel cardStyle)
    : Reward(player)
{
    private static readonly (CardRarity? Rarity, int Cost)[] Choices =
        [(null, 0), (CardRarity.Common, 0), (CardRarity.Uncommon, 2), (CardRarity.Rare, 6)];
    private (int Index, List<CardCreationResult> Cards)? _opened;

    protected override RewardType RewardType => RewardType.Card;
    protected override string IconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card.png");
    public override int RewardsSetIndex => 5;
    public override bool IsPopulated => true;
    private string TextKey => ForestCardPack.TextKey(kind);
    public override LocString Description => ForestLocalization.CreateLocString(TextKey + ".title");
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [new HoverTip(Description, ForestLocalization.CreateLocString(TextKey + ".description"))];

    public static ForestBattleCardPackReward? Draw(Player player, RoomType roomType)
    {
        List<ForestShopOfferKind> kinds = [ForestShopOfferKind.PowerPack, ForestShopOfferKind.SkillPack, ForestShopOfferKind.AttackPack];
        var rng = player.PlayerRng.Rewards;
        while (kinds.Count > 0)
        {
            var index = rng.NextInt(kinds.Count);
            var kind = kinds[index];
            var options = ForestCardPack.CreateBattleOptions(player, kind, roomType);
            var pool = ForestCardPack.GetPossibleCards(player, options).ToArray();
            if (pool.Length > 0)
                return new ForestBattleCardPackReward(player, kind, options, pool.Select(card => card.Rarity).ToHashSet(), pool[0]);
            kinds.RemoveAt(index);
        }
        return null;
    }

    public override void Populate() { }

    protected override async Task<bool> OnSelect()
    {
        var run = (RunState)Player.RunState;
        var (choiceId, index) = await ForestCardPackSelection.ChooseIndex(Player,
            () => ForestCardPackRarityScreen.Choose(run, TextKey, Choices, available, _opened?.Index, cardStyle));
        if (index is not int option || option < 0 || option >= Choices.Length) return false;
        if (_opened is { } opened && opened.Index != option) return false;
        var (rarity, cost) = Choices[option];
        if (rarity is { } fixedRarity && !available.Contains(fixedRarity)) return false;
        var cards = _opened?.Cards;
        if (cards == null)
        {
            if (!await ForestTeam.TryPayHope(run, Player, choiceId, cost)) return false;
            try
            {
                cards = ForestCardPack.Draw(Player, options, rarity);
                if (cards.Count == 0) return false;
                _opened = (option, cards);
            }
            finally
            {
                if (!_opened.HasValue) ForestGenerationService.RefundHope(run, cost);
            }
        }
        var selected = await ForestCardPackSelection.ChooseCard(cards, Player);
        if (selected == null) return false;
        var result = await CardPileCmd.Add(selected, PileType.Deck);
        if (!result.success) return false;
        RecordChoices(selected, result.cardAdded);
        return true;
    }

    public override void OnSkipped() => RecordChoices(null, null);

    private void RecordChoices(CardModel? selected, CardModel? added)
    {
        var history = Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId).CardChoices;
        if (history == null || _opened is not { } opened) return;
        if (added != null) history.Add(new CardChoiceHistoryEntry(added, wasPicked: true));
        foreach (var candidate in opened.Cards)
            if (candidate.Card != selected) history.Add(new CardChoiceHistoryEntry(candidate.Card, wasPicked: false));
    }

    public override void MarkContentAsSeen() { }
}
