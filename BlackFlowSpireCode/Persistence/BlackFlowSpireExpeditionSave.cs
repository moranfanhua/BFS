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
                    if (!data.TryChangeHope(BlackFlowSpireExpeditionRules.RestHope)) return;
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
                // Put a newly found part in the shared box after its normal floor-entry cleanup.
                data.Engine.Add(dispatch.Reward);
                progress.ExpeditionReturn = new(dispatch.SourceActIndex, dispatch.SourceNodeId,
                    card.Id.Category, card.Id.Entry, upgraded, dispatch.Reward);
                data.ExpeditionDispatch = null;
                data.Progress[map.ActIndex] = progress;
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
}
