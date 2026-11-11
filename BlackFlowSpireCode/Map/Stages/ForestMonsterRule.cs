namespace BlackFlowSpire.Map.Stages;

// A native power granted on room entry. AmountProperty reads a native monster
// property when the amount depends on ascension, such as IncantationAmount.
public sealed record ForestStartingPower(string PowerId, decimal Amount = 0m, string? AmountProperty = null);

// Written in each stage's C# version and copied into its generated snapshot.
// FormationIndex distinguishes one extra enemy from others of the same type.
public sealed record ForestMonsterRule
{
    public string MonsterId { get; init; } = "";
    public bool InitialOnly { get; init; } = true;
    public int? FormationIndex { get; init; }
    public string? FirstMoveId { get; init; }
    public string? RoomEntryMoveId { get; init; }
    public decimal StartingBlock { get; init; }
    public int InitialStockBonus { get; init; }
    public int StatusCardCountOffset { get; init; }
    public Dictionary<string, int> NativePropertyOffsets { get; init; } = [];
    public List<string> MoveCycle { get; init; } = [];
    public List<ForestStartingPower> StartingPowers { get; init; } = [];
    public List<string> RemovedStartingPowers { get; init; } = [];
    public Dictionary<string, decimal> SelfPowerAmountOffsets { get; init; } = [];
    public int? DeathSpawnCount { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MonsterId) || FormationIndex is < 0 || DeathSpawnCount is < 1
            || StartingBlock < 0m || InitialStockBonus < 0 || NativePropertyOffsets == null
            || MoveCycle == null || StartingPowers == null || RemovedStartingPowers == null || SelfPowerAmountOffsets == null
            || MoveCycle.Any(string.IsNullOrWhiteSpace) || RemovedStartingPowers.Any(string.IsNullOrWhiteSpace)
            || SelfPowerAmountOffsets.Keys.Any(string.IsNullOrWhiteSpace) || NativePropertyOffsets.Keys.Any(string.IsNullOrWhiteSpace)
            || StartingPowers.Any(power => power == null || string.IsNullOrWhiteSpace(power.PowerId)
                || (power.AmountProperty == null && power.Amount <= 0m)))
            throw new InvalidDataException("Invalid forest monster behavior rule.");
    }

    public ForestMonsterRule Copy() => this with
    {
        MoveCycle = [.. MoveCycle], StartingPowers = [.. StartingPowers],
        RemovedStartingPowers = [.. RemovedStartingPowers],
        SelfPowerAmountOffsets = new(SelfPowerAmountOffsets, StringComparer.Ordinal),
        NativePropertyOffsets = new(NativePropertyOffsets, StringComparer.Ordinal)
    };
}
