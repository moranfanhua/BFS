using BlackFlowSpire.Combat;

namespace BlackFlowSpire.Persistence;

public sealed partial class BlackFlowSpireRunData
{
    public Dictionary<ulong, BlackFlowSpireHope> PlayerHope { get; set; } = [];

    internal BlackFlowSpireHope GetHope(ulong playerId) =>
        PlayerHope.GetValueOrDefault(playerId, BlackFlowSpireHope.Initial);

    internal bool TryChangeHope(ulong playerId, int delta)
    {
        if (!GetHope(playerId).TryChange(delta, out var result)) return false;
        PlayerHope[playerId] = result;
        return true;
    }

    internal void RefundHope(ulong playerId, int amount)
    {
        var hope = GetHope(playerId);
        PlayerHope[playerId] = hope with { Balance = (int)Math.Min((long)hope.Balance + amount, hope.Maximum) };
    }

    internal void GrantHopeToPlayers(IEnumerable<ulong> playerIds, int amount)
    {
        if (amount == 0) return;
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        foreach (var playerId in playerIds)
        {
            var hope = GetHope(playerId);
            hope.TryChange(Math.Min(amount, int.MaxValue - hope.Maximum), out var result);
            PlayerHope[playerId] = result;
        }
    }
}
