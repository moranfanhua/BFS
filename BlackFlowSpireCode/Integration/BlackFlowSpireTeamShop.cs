using System.Text.Json;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BlackFlowSpire.Integration;

internal static partial class BlackFlowSpireTeam
{
    internal static void IssueTickets(RunState state, BlackFlowSpireShopOfferKind kind)
    {
        var tickets = BlackFlowSpireGenerationService.TeamData(state).ShopTickets;
        var purchase = SessionFor(state).Active;
        foreach (var player in state.Players)
        {
            string[] options = [];
            JsonElement[] cards = [];
            if (kind == BlackFlowSpireShopOfferKind.PotionPack)
                options = PotionFactory.CreateRandomPotionsOutOfCombat(player, 3, player.PlayerRng.Shops)
                    .Select(p => p.Id.ToString()).ToArray();
            else if (kind is not (BlackFlowSpireShopOfferKind.RemoveCard or BlackFlowSpireShopOfferKind.UpgradeCard))
            {
                var draw = BlackFlowSpireCardPack.Draw(player, kind, player.PlayerRng.Shops);
                cards = draw.Select(c => BlackFlowSpireGenerationService.CaptureExpeditionCard(c.Card)).ToArray();
                foreach (var candidate in draw) state.RemoveCard(candidate.Card);
            }
            tickets.Add(new(purchase, player.NetId, kind, options, cards));
        }
        BlackFlowSpireGenerationService.SaveTeamChange(state);
    }

    internal static SerializableCard ReadShopCard(JsonElement card) =>
        card.Deserialize<SerializableCard>(JsonSerializationUtility.Options) ?? throw new InvalidDataException("Missing shop card.");

    private static async Task<bool> ClaimTicket(RunState state, BlackFlowSpireTeamCommand command)
    {
        var tickets = BlackFlowSpireGenerationService.TeamData(state).ShopTickets;
        var ticket = tickets.FirstOrDefault(t => t.Player == command.Actor && t.Purchase == command.Ticket);
        if (ticket == null || command.Argument < -1) return false;
        var player = state.Players.Single(p => p.NetId == ticket.Player);
        var index = command.Argument;
        if (index >= 0 && !await ApplyShopChoice(state, player, ticket, index, command.Key)) return false;
        tickets.Remove(ticket);
        BlackFlowSpireGenerationService.SaveTeamChange(state);
        return true;
    }

    private static async Task<bool> ApplyShopChoice(RunState state, Player player, BlackFlowSpireShopTicket ticket, int index, string? key)
    {
        switch (ticket.Kind)
        {
            case BlackFlowSpireShopOfferKind.RemoveCard:
            case BlackFlowSpireShopOfferKind.UpgradeCard:
                if (player.Deck.Cards.ElementAtOrDefault(index) is not { } card
                    || BlackFlowSpireGenerationService.CaptureExpeditionCard(card).GetRawText() != key) return false;
                if (ticket.Kind == BlackFlowSpireShopOfferKind.UpgradeCard && card.IsUpgradable) CardCmd.Upgrade(card);
                else if (ticket.Kind == BlackFlowSpireShopOfferKind.RemoveCard && card.IsRemovable) await CardPileCmd.RemoveFromDeck(card);
                else return false;
                break;
            case BlackFlowSpireShopOfferKind.PotionPack:
                if (index >= ticket.Options.Length) return false;
                var potion = SaveUtil.PotionOrDeprecated(ModelId.Deserialize(ticket.Options[index])).ToMutable();
                if (!(await PotionCmd.TryToProcure(potion, player)).success) return false;
                state.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).BoughtPotions.Add(potion.Id);
                break;
            default:
                if (index >= ticket.Cards.Length) return false;
                var chosen = state.LoadCard(ReadShopCard(ticket.Cards[index]), player);
                if (!(await CardPileCmd.Add(chosen, PileType.Deck)).success)
                {
                    // Native prevention can grant a replacement benefit. Consume this selection once.
                    state.RemoveCard(chosen);
                    return true;
                }
                if (chosen.Pool.IsColorless) state.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).BoughtColorless.Add(chosen.Id);
                break;
        }
        return true;
    }

    internal static bool Idle(RunState state) => !SessionFor(state).CommandQueued;
}
