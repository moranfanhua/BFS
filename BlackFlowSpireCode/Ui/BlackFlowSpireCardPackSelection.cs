using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireCardPackSelection
{
    public static async Task<CardModel?> ChooseCard(IReadOnlyList<CardCreationResult> options, Player player)
    {
        if (options.Count == 0) return null;
        var cards = options.Select(option => option.Card).ToList();
        var (_, index) = await ChooseIndex(player, async () =>
        {
            foreach (var card in cards) SaveManager.Instance.MarkCardAsSeen(card);
            // The command wrapper limits this screen to three cards. The native screen can display all four.
            var screen = NChooseACardSelectionScreen.ShowScreen(cards, canSkip: true)!;
            var row = screen.GetNode<Control>("CardRow");
            void FitRow() => row.Scale = Vector2.One * Mathf.Min(1f, (screen.Size.X - 80) / (cards.Count * 340f));
            FitRow();
            screen.Resized += FitRow;
            void RefreshText() => screen.GetNode<MegaLabel>("SkipButton/Label").SetTextAutoSize(
                new LocString("gameplay_ui", "CHOOSE_CARD_SKIP_BUTTON").GetFormattedText());
            RefreshText();
            BlackFlowSpireLocalization.RefreshWithLocale(screen, RefreshText);
            var selected = (await screen.CardsSelected()).FirstOrDefault();
            return selected == null ? null : cards.IndexOf(selected);
        });
        return index is int value && value >= 0 && value < cards.Count ? cards[value] : null;
    }

    public static async Task<(uint ChoiceId, int? Index)> ChooseIndex(Player player, Func<Task<int?>> chooseLocal)
    {
        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        var context = new BlockingPlayerChoiceContext();
        var choiceId = synchronizer.ReserveChoiceId(player);
        await context.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
        try
        {
            if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != NetGameType.Replay)
            {
                var index = await chooseLocal();
                synchronizer.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(index));
                return (choiceId, index);
            }

            return (choiceId, (await synchronizer.WaitForRemoteChoice(player, choiceId)).AsIndexOrNull());
        }
        finally
        {
            await context.SignalPlayerChoiceEnded();
        }
    }
}
