using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireShopController
{
    private bool WaitingForClaims => BlackFlowSpireTeam.IsOnline && _inventory.Player.RunState is RunState state
        && BlackFlowSpireGenerationService.TeamData(state).ShopTickets.Any(t =>
            RunManager.Instance.RunLobby?.PlayerIds.Contains(t.Player) == true);
}

// A player's entitlement survives leaving the shop or reconnecting. Present it at the next
// available room UI, outside the native action queue so the potion belt remains usable.
internal partial class BlackFlowSpireShopClaims(RunState state) : Node
{
    private bool _choosing;
    private double _delay;
    public override void _Process(double delta)
    {
        _delay -= delta;
        if (_choosing || _delay > 0) return;
        _delay = 0.25;
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.Idle(state) || RunManager.Instance.IsChangingRooms
            || CombatManager.Instance.IsInProgress || NMapScreen.Instance?.IsOpen == true
            || NOverlayStack.Instance is not { ScreenCount: 0 } || NModalContainer.Instance?.OpenModal != null
            || LocalContext.GetMe(state) is not { } player) return;
        if (BlackFlowSpireGenerationService.TeamData(state).ShopTickets.FirstOrDefault(t => t.Player == player.NetId) is { } ticket)
            TaskHelper.RunSafely(Choose(player, ticket));
    }

    private async Task Choose(Player player, BlackFlowSpireShopTicket ticket)
    {
        _choosing = true;
        try
        {
            int index;
            string? key = null;
            if (ticket.Kind == BlackFlowSpireShopOfferKind.PotionPack)
            {
                var potions = ticket.Options.Select(id => SaveUtil.PotionOrDeprecated(ModelId.Deserialize(id)).ToMutable()).ToList();
                var selected = await BlackFlowSpirePotionPackSelectionScreen.Choose(potions, async p =>
                {
                    if (!player.HasOpenPotionSlots) return BlackFlowSpireText.Get("UI.SHOP.potion_full");
                    return await Submit(ticket, potions.IndexOf(p), null) ? null : BlackFlowSpireText.Get("UI.SHOP.potion_forbidden");
                });
                if (selected != null) return;
                index = -1;
            }
            else if (ticket.Kind is BlackFlowSpireShopOfferKind.RemoveCard or BlackFlowSpireShopOfferKind.UpgradeCard)
            {
                var upgrade = ticket.Kind == BlackFlowSpireShopOfferKind.UpgradeCard;
                var options = player.Deck.Cards.Where(c => upgrade ? c.IsUpgradable : c.IsRemovable).ToList();
                CardModel? selected = null;
                if (options.Count > 0)
                {
                    var prefs = new CardSelectorPrefs(upgrade ? CardSelectorPrefs.UpgradeSelectionPrompt : CardSelectorPrefs.RemoveSelectionPrompt, 1)
                        { Cancelable = true, RequireManualConfirmation = true };
                    if (upgrade) selected = (await NDeckUpgradeSelectScreen.ShowScreen(options, prefs, state).CardsSelected()).FirstOrDefault();
                    else
                    {
                        var picker = NDeckCardSelectScreen.Create(options, prefs);
                        NOverlayStack.Instance!.Push(picker);
                        selected = (await picker.CardsSelected()).FirstOrDefault();
                    }
                }
                index = selected == null ? -1 : player.Deck.Cards.ToList().IndexOf(selected);
                if (selected != null)
                {
                    if (index < 0) return;
                    key = BlackFlowSpireGenerationService.CaptureExpeditionCard(selected).GetRawText();
                }
            }
            else
            {
                var cards = ticket.Cards.Select(saved =>
                {
                    var card = CardModel.FromSerializable(BlackFlowSpireTeam.ReadShopCard(saved));
                    card.Owner = player;
                    return card;
                }).ToList();
                index = await BlackFlowSpireCardPackSelection.ChooseLocal(cards) ?? -1;
            }
            await Submit(ticket, index, key);
        }
        finally { _choosing = false; }
    }

    private async Task<bool> Submit(BlackFlowSpireShopTicket ticket, int index, string? key)
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || !BlackFlowSpireRuntime.CanContinueRun
            || !ReferenceEquals(state, BlackFlowSpireRuntime.State) || !RunManager.Instance.NetService.IsConnected) return false;
        return await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.ClaimOffer, index, ticket: ticket.Purchase, key: key);
    }
}
