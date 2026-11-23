using BlackFlowSpire.Map.Generation;

namespace BlackFlowSpire.Map.Exploration;

public sealed class BlackFlowSpireOperatorTurn
{
    public ulong? PlayerId { get; set; }
    public long Number { get; set; }
    public List<ulong> Remaining { get; set; } = [];
    public bool EnteredNode { get; set; }

    internal void Draw(string seed, int act, IEnumerable<ulong> connected)
    {
        var players = connected.Distinct().Order().ToList();
        if (players.Count == 0) return;
        Remaining.RemoveAll(id => !players.Contains(id));
        if (Remaining.Count == 0)
        {
            new StableRandom(seed, act, $"operators/{Number}").Shuffle(players);
            if (players.Count > 1 && players[0] == PlayerId)
                (players[0], players[1]) = (players[1], players[0]);
            Remaining = players;
        }
        PlayerId = Remaining[0];
        Remaining.RemoveAt(0);
        Number++;
    }
}
