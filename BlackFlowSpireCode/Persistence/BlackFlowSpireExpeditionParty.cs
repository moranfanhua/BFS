using System.Text.Json;
using System.Text.Json.Serialization;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Persistence;

public sealed record BlackFlowSpireExpeditionSelection(int DeckIndex, JsonElement? Card);

public sealed record BlackFlowSpireExpeditionDispatch(int SourceActIndex, int SourceNodeId,
    Dictionary<ulong, BlackFlowSpireExpeditionSelection> Cards, BlackFlowSpireProcessedPartKind Reward);

public sealed partial class BlackFlowSpireRunData
{
    public Dictionary<ulong, BlackFlowSpireExpeditionSelection> ExpeditionChoices { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlackFlowSpireExpeditionDispatch? ExpeditionDispatch { get; set; }

    internal bool HasAllExpeditionChoices(IEnumerable<ulong> players) =>
        ExpeditionChoices.Count > 0 && players.All(ExpeditionChoices.ContainsKey);

    internal void ConfirmExpedition(int act, int node, BlackFlowSpireExplorationProgress progress,
        BlackFlowSpireProcessedPartKind reward, IEnumerable<ulong> players, Action<BlackFlowSpireExpeditionDispatch> sendCards)
    {
        if (progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro || ExpeditionChoices.Count == 0
            || ExpeditionDispatch != null) throw new InvalidOperationException("No expedition choices to confirm.");
        var scouts = ExpeditionChoices.Where(pair => pair.Value.Card != null).ToDictionary();
        if (scouts.Count > 0)
        {
            var party = new BlackFlowSpireExpeditionDispatch(act, node, scouts, reward);
            sendCards(party);
            ExpeditionDispatch = party;
            progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Departed;
        }
        else
        {
            GrantHopeToPlayers(players, BlackFlowSpireExpeditionRules.RestHope);
            progress.ExpeditionScene = BlackFlowSpireExpeditionScene.Rested;
        }
        ExpeditionChoices.Clear();
    }

    internal bool CompleteExpeditionReturn(int act, BlackFlowSpireExplorationProgress progress,
        Dictionary<ulong, BlackFlowSpireReturnedCard> cards, IReadOnlyCollection<ulong> players)
    {
        if (ExpeditionDispatch is not { } party || party.SourceActIndex >= act) return false;
        if (cards.Count != party.Cards.Count || !party.Cards.Keys.All(cards.ContainsKey))
            throw new InvalidOperationException("Not every scouting card has returned.");
        Engine.Add(party.Reward);
        var hope = players.Count > 1 ? BlackFlowSpireExpeditionRules.RestHope : 0;
        GrantHopeToPlayers(players, hope);
        progress.ExpeditionReturn = new(party.SourceActIndex, party.SourceNodeId, cards, party.Reward, hope);
        ExpeditionDispatch = null;
        return true;
    }
}
