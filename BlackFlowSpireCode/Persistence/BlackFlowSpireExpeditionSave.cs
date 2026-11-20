using System.Text.Json;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BlackFlowSpire.Persistence;

public static partial class BlackFlowSpireGenerationService
{
    internal static bool CanDispatchCard(CardModel card) => card.Rarity is not
        (CardRarity.Basic or CardRarity.Curse or CardRarity.Status or CardRarity.Quest)
        && card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    internal static bool HasExpeditionChoice(RunState state, ulong id) => _slot!.Get(state).ExpeditionChoices.ContainsKey(id);

    internal static bool ExpeditionReady(RunState state) => BlackFlowSpireTeam.IsHost
        ? RunManager.Instance.RunLobby is { } lobby && _slot!.Get(state).HasAllExpeditionChoices(lobby.PlayerIds)
        : LocalContext.GetMe(state) is { } player && HasExpeditionChoice(state, player.NetId);

    internal static bool RecordExpeditionChoice(RunState state, Player player, int act, int node, BlackFlowSpireExpeditionSelection choice)
    {
        var map = GetOrGenerate(state);
        var progress = GetProgress(state);
        if (map.ActIndex != act || map.PositionNodeId != node || !BlackFlowSpireExpeditionRules.IsPending(map, progress)
            || progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro || !ChoiceMatches(player, choice)) return false;
        var added = false;
        _slot!.Modify(state, data => { added = data.ExpeditionChoices.TryAdd(player.NetId, choice); });
        return added;
    }

    private static bool ChoiceMatches(Player player, BlackFlowSpireExpeditionSelection choice)
    {
        if (choice.Card == null) return choice.DeckIndex == BlackFlowSpireExpeditionRules.Rest;
        if (choice.DeckIndex < 0 || choice.DeckIndex >= player.Deck.Cards.Count) return false;
        var card = player.Deck.Cards[choice.DeckIndex];
        // SerializableCard.Equals omits saved card properties and the floor where the card was obtained.
        return CanDispatchCard(card) && JsonElement.DeepEquals(CaptureExpeditionCard(card), choice.Card.Value);
    }

    internal static JsonElement CaptureExpeditionCard(CardModel card) =>
        JsonSerializer.SerializeToElement(card.ToSerializable(), JsonSerializationUtility.Options);

    internal static bool TryResolveExpedition(RunState state, BlackFlowSpireMap map,
        BlackFlowSpireExplorationProgress progress, int command)
    {
        if (!BlackFlowSpireExpeditionRules.IsPending(map, progress)) return false;
        var resolved = false;
        BlackFlowSpireExpeditionDispatch? departure = null;
        _slot!.Modify(state, data =>
        {
            if (command == BlackFlowSpireExpeditionRules.Leave)
            {
                if (progress.ExpeditionScene == BlackFlowSpireExpeditionScene.Intro) return;
                map.GetNode(map.PositionNodeId).Completed = true;
                progress.PendingNodeId = null;
                progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Intro;
            }
            else
            {
                if (command != BlackFlowSpireExpeditionRules.ConfirmParty || progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro
                    || !ExpeditionReady(state)) return;
                // A stored choice must still identify the same card, including after a rejoin.
                foreach (var player in state.Players)
                    if (data.ExpeditionChoices.TryGetValue(player.NetId, out var pick) && !ChoiceMatches(player, pick))
                        data.ExpeditionChoices.Remove(player.NetId);
                if (!ExpeditionReady(state)) return;
                data.ConfirmExpedition(map.ActIndex, map.PositionNodeId, progress, BlackFlowSpireExpeditionRules.DrawReward(map, map.PositionNodeId),
                    state.Players.Select(player => player.NetId), party =>
                    {
                        ApplyExpeditionDeck(state, party, returned: false);
                        BlackFlowSpireTeam.RecordDeck(state, party, returned: false);
                        departure = party;
                    });
            }
            data.Maps[map.ActIndex] = map;
            data.Progress[map.ActIndex] = progress;
            resolved = true;
        });
        if (departure != null) RefreshExpeditionDecks(state, departure);
        return resolved;
    }

    internal static void SettleExpeditionReturn(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (_slot!.Get(state).ExpeditionDispatch is not { } party || party.SourceActIndex >= map.ActIndex) return;
        _slot.Modify(state, data =>
        {
            var returned = ApplyExpeditionDeck(state, party, returned: true);
            data.CompleteExpeditionReturn(map.ActIndex, progress, returned, state.Players.Select(player => player.NetId).ToArray());
            data.Progress[map.ActIndex] = progress;
            BlackFlowSpireTeam.RecordDeck(state, party, returned: true);
        });
        RefreshExpeditionDecks(state, party);
    }

    internal static Dictionary<ulong, BlackFlowSpireReturnedCard> ApplyExpeditionDeck(RunState state,
        BlackFlowSpireExpeditionDispatch party, bool returned)
    {
        var returnedCards = new Dictionary<ulong, BlackFlowSpireReturnedCard>();
        var changed = new List<(Player Owner, CardModel Card, int Index)>();
        try
        {
            foreach (var (id, pick) in party.Cards)
            {
                var owner = state.Players.Single(player => player.NetId == id);
                if (returned)
                {
                    var saved = pick.Card?.Deserialize<SerializableCard>(JsonSerializationUtility.Options)
                        ?? throw new InvalidDataException("Missing expedition card.");
                    var card = state.LoadCard(saved, owner);
                    changed.Add((owner, card, pick.DeckIndex));
                    var upgrade = card.IsUpgradable;
                    if (upgrade)
                    {
                        card.UpgradeInternal();
                        card.FinalizeUpgradeInternal();
                    }
                    owner.Deck.AddInternal(card, Math.Min(pick.DeckIndex, owner.Deck.Cards.Count), silent: true);
                    returnedCards.Add(id, new(card.Id.Category, card.Id.Entry, upgrade));
                }
                else
                {
                    if (pick.Card == null || !ChoiceMatches(owner, pick)) throw new InvalidDataException("The expedition card has changed.");
                    var card = owner.Deck.Cards[pick.DeckIndex];
                    // Temporary removal must not trigger permanent card-removal hooks.
                    owner.Deck.RemoveInternal(card, silent: true);
                    state.RemoveCard(card);
                    changed.Add((owner, card, pick.DeckIndex));
                }
            }
        }
        catch
        {
            foreach (var (owner, card, index) in changed)
            {
                if (returned)
                {
                    if (owner.Deck.Cards.Contains(card)) owner.Deck.RemoveInternal(card, silent: true);
                    state.RemoveCard(card);
                }
                else
                {
                    state.AddCard(card, owner);
                    owner.Deck.AddInternal(card, index, silent: true);
                }
            }
            throw;
        }
        return returnedCards;
    }

    internal static void RefreshExpeditionDecks(RunState state, BlackFlowSpireExpeditionDispatch party)
    {
        foreach (var owner in state.Players)
            if (party.Cards.ContainsKey(owner.NetId)) owner.Deck.InvokeContentsChanged();
    }
}
