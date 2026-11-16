using BlackFlowSpire.Map.Parts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Shop;

internal static class BlackFlowSpireCardPack
{
    private const int CardCount = 4;

    public static string TextKey(BlackFlowSpireShopOfferKind kind) => "UI.SHOP.OFFER." + (kind switch
    {
        BlackFlowSpireShopOfferKind.ColorlessPack => "COLORLESS_PACK",
        BlackFlowSpireShopOfferKind.AttackPack => "ATTACK_PACK",
        BlackFlowSpireShopOfferKind.SkillPack => "SKILL_PACK",
        BlackFlowSpireShopOfferKind.PowerPack => "POWER_PACK",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    });

    public static List<CardCreationResult> Draw(Player player, BlackFlowSpireShopOfferKind kind, Rng rng) =>
        Draw(player, CreateOptions(player, kind, rng, RoomType.Shop));

    public static CardCreationOptions CreateBattleOptions(Player player, BlackFlowSpireShopOfferKind kind, RoomType roomType) =>
        CreateOptions(player, kind, player.PlayerRng.Rewards, roomType);

    private static CardCreationOptions CreateOptions(Player player, BlackFlowSpireShopOfferKind kind, Rng rng, RoomType roomType)
    {
        CardType? type = kind switch
        {
            BlackFlowSpireShopOfferKind.AttackPack => CardType.Attack,
            BlackFlowSpireShopOfferKind.SkillPack => CardType.Skill,
            BlackFlowSpireShopOfferKind.PowerPack => CardType.Power,
            BlackFlowSpireShopOfferKind.ColorlessPack => null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        bool colorless = kind == BlackFlowSpireShopOfferKind.ColorlessPack;
        var pool = colorless ? ModelDb.CardPool<ColorlessCardPool>() : player.Character.CardPool;
        var roomOptions = CardCreationOptions.ForRoom(player, roomType);
        var options = new CardCreationOptions([pool], roomOptions.Source, roomOptions.RarityOdds,
                card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                    && (!type.HasValue || card.Type == type.Value)
                    && (!colorless || card.Pool.IsColorless))
            .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.NoUpgradeRoll | CardCreationFlags.NoModifyHooks)
            .WithRngOverride(rng);
        if (colorless) options.WithFlags(CardCreationFlags.NoCardPoolModifications);
        if (roomType != RoomType.Shop)
            options.WithFlags(CardCreationFlags.IsFromCombat | CardCreationFlags.NoRarityModification);

        return Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);
    }

    public static IEnumerable<CardModel> GetPossibleCards(Player player, CardCreationOptions options) =>
        options.GetPossibleCards(player).Where(card => card.IsAllowedInRun(player.RunState)).DistinctBy(card => card.Id);

    public static List<CardCreationResult> Draw(Player player, CardCreationOptions options, CardRarity? rarity = null)
    {
        if (rarity is { } fixedRarity)
        {
            // Uniform within a fixed tier leaves the native encounter rarity counter untouched.
            var packFilter = options.CardPoolFilter;
            options = new CardCreationOptions(options.CardPools, options.Source, CardRarityOddsType.Uniform,
                    card => card.Rarity == fixedRarity && (packFilter?.Invoke(card) ?? true))
                .WithFlags(options.Flags).WithRngOverride(options.RngOverride!);
        }
        var possibleCount = GetPossibleCards(player, options).Count();
        if (possibleCount == 0) return [];

        // Native generation excludes duplicates within a batch. Tiny modded pools
        // may need a second batch to provide the requested four choices.
        List<CardCreationResult> cards = [];
        while (cards.Count < CardCount)
            cards.AddRange(CardFactory.CreateForReward(player, Math.Min(CardCount - cards.Count, possibleCount), options));
        Hook.ModifyMerchantCardCreationResults(player.RunState, player, cards);
        return cards;
    }
}
