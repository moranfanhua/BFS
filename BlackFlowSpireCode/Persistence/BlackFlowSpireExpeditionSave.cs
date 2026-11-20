using System.Text.Json.Serialization;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BlackFlowSpire.Persistence;

public sealed record BlackFlowSpireExpeditionDispatch(int SourceActIndex, int SourceNodeId,
    ulong PlayerId, int DeckIndex, SerializableCard Card, BlackFlowSpireProcessedPartKind Reward);

public sealed partial class BlackFlowSpireRunData
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlackFlowSpireExpeditionDispatch? ExpeditionDispatch { get; set; }
}

public static partial class BlackFlowSpireGenerationService
{
    internal static bool CanDispatchCard(CardModel card) => card.Rarity is not
        (CardRarity.Basic or CardRarity.Curse or CardRarity.Status or CardRarity.Quest)
        && card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    internal static bool TryResolveExpedition(RunState state, BlackFlowSpireMap map,
        BlackFlowSpireExplorationProgress progress, Player? owner, int choice)
    {
        if (!BlackFlowSpireExpeditionRules.IsPending(map, progress)) return false;
        var resolved = false;
        var refreshDeck = false;
        _slot!.Modify(state, data =>
        {
            var node = map.GetNode(map.PositionNodeId);
            if (choice == BlackFlowSpireExpeditionRules.Leave)
            {
                if (progress.ExpeditionScene == BlackFlowSpireExpeditionScene.Intro) return;
                node.Completed = true;
                progress.PendingNodeId = null;
                progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Intro;
            }
            else
            {
                if (progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro) return;
                if (choice == BlackFlowSpireExpeditionRules.Rest)
                {
                    GrantTeamHope(state, data, BlackFlowSpireExpeditionRules.RestHope);
                    progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Rested;
                }
                else
                {
                    if (owner == null || data.ExpeditionDispatch != null || choice < 0 || choice >= owner.Deck.Cards.Count) return;
                    var card = owner.Deck.Cards[choice];
                    if (!CanDispatchCard(card)) return;
                    var dispatch = new BlackFlowSpireExpeditionDispatch(map.ActIndex, node.Id, owner.NetId, choice,
                        card.ToSerializable(), BlackFlowSpireExpeditionRules.DrawReward(map, node.Id));
                    // Keep temporary removal out of the native permanent removal commands and their hooks.
                    owner.Deck.RemoveInternal(card, silent: true);
                    state.RemoveCard(card);
                    data.ExpeditionDispatch = dispatch;
                    BlackFlowSpireTeam.RecordDeck(state, dispatch, returned: false);
                    progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Departed;
                    refreshDeck = true;
                }
            }
            data.Maps[map.ActIndex] = map;
            data.Progress[map.ActIndex] = progress;
            resolved = true;
        });
        if (refreshDeck) owner!.Deck.InvokeContentsChanged();
        return resolved;
    }

    internal static void SettleExpeditionReturn(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        if (_slot!.Get(state).ExpeditionDispatch is not { } dispatch || dispatch.SourceActIndex >= map.ActIndex) return;
        var owner = state.Players.FirstOrDefault(player => player.NetId == dispatch.PlayerId)
            ?? throw new InvalidDataException("The scouting card's owner is missing from the run.");
        _slot.Modify(state, data =>
        {
            var (card, upgraded) = ReturnCard(state, owner, dispatch);
            try
            {
                // Put a newly found part in the shared box after its normal floor-entry cleanup.
                data.Engine.Add(dispatch.Reward);
                progress.ExpeditionReturn = new(dispatch.SourceActIndex, dispatch.SourceNodeId,
                    card.Id.Category, card.Id.Entry, upgraded, dispatch.Reward);
                data.ExpeditionDispatch = null;
                data.Progress[map.ActIndex] = progress;
                BlackFlowSpireTeam.RecordDeck(state, dispatch, returned: true);
            }
            catch
            {
                if (owner.Deck.Cards.Contains(card)) owner.Deck.RemoveInternal(card, silent: true);
                state.RemoveCard(card);
                throw;
            }
        });
        owner.Deck.InvokeContentsChanged();
    }

    internal static void ApplyExpeditionDeck(RunState state, BlackFlowSpireExpeditionDispatch dispatch, bool returned)
    {
        var owner = state.Players.Single(player => player.NetId == dispatch.PlayerId);
        if (returned) ReturnCard(state, owner, dispatch);
        else
        {
            var card = owner.Deck.Cards[dispatch.DeckIndex];
            if (!card.ToSerializable().Equals(dispatch.Card)) throw new InvalidDataException("Different expedition card on the guest.");
            owner.Deck.RemoveInternal(card, silent: true);
            state.RemoveCard(card);
        }
        owner.Deck.InvokeContentsChanged();
    }

    private static (CardModel Card, bool Upgraded) ReturnCard(RunState state, Player owner, BlackFlowSpireExpeditionDispatch dispatch)
    {
        var card = state.LoadCard(dispatch.Card, owner);
        try
        {
            var upgraded = card.IsUpgradable;
            if (upgraded)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            owner.Deck.AddInternal(card, Math.Min(dispatch.DeckIndex, owner.Deck.Cards.Count), silent: true);
            return (card, upgraded);
        }
        catch
        {
            if (owner.Deck.Cards.Contains(card)) owner.Deck.RemoveInternal(card, silent: true);
            state.RemoveCard(card);
            throw;
        }
    }
}
