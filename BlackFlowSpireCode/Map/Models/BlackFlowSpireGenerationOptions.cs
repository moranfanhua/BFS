namespace BlackFlowSpire.Map.Models;

public sealed record BlackFlowSpireGenerationOptions
{
    public int Width { get; init; } = 9;
    public int Height { get; init; } = 9;
    // Includes the center start and the single exit; per-floor defaults come from BlackFlowSpireFloorPlan.
    public int NodeCount { get; init; } = 49;
    public int InitialActionPoints { get; init; } = 20;
    public int MoveCost { get; init; } = 1;
    public int RevealRadius { get; init; } = 3;
    public int MinimumBossDistance { get; init; } = 6;
    public int ExtraConnections { get; init; } = 4;
    public int EmptyWeight { get; init; } = 35;
    public int CombatWeight { get; init; } = 40;
    public int EventWeight { get; init; } = 25;
    public int ElitePercent { get; init; } = 20;
    public int RegularEventWeight { get; init; } = 60;
    public int RewardWeight { get; init; } = 20;
    public int ShopWeight { get; init; } = 20;
    // Replaces ordinary event/empty slots; never replaces battles, rewards, shops or the exit.
    public int DoorPairChancePercent { get; init; } = 30;
    public int ResidentStrongholdChancePercent { get; init; } = 30;
    public int VantagePointChancePercent { get; init; } = 30;
    public int ShortcutChancePercent { get; init; } = 15;
    public int RestChancePercent { get; init; } = 100;
    public int SacrificeChancePercent { get; init; } = 100;
    // Independent inputs for future Blackflow progression/difficulty systems.
    public int SecrecyLevel { get; init; }
    public bool GameOfLifeSplitUnlocked { get; init; }

    public void Validate()
    {
        if (Width is < 5 or > 31 || Width % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Width must be odd and between 5 and 31.");
        if (Height is < 5 or > 31 || Height % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(Height), "Height must be odd and between 5 and 31.");
        if (NodeCount < 3 || NodeCount > Width * Height)
            throw new ArgumentOutOfRangeException(nameof(NodeCount));
        if (InitialActionPoints is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(InitialActionPoints));
        if (MoveCost is < 1 or > 1_000)
            throw new ArgumentOutOfRangeException(nameof(MoveCost));
        if (RevealRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(RevealRadius));
        if (MinimumBossDistance < 1 || MinimumBossDistance >= NodeCount)
            throw new ArgumentOutOfRangeException(nameof(MinimumBossDistance));
        if (ExtraConnections < 0 || ExtraConnections > 2 * Width * Height)
            throw new ArgumentOutOfRangeException(nameof(ExtraConnections));
        if (ElitePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(ElitePercent));
        if (DoorPairChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(DoorPairChancePercent));
        if (ResidentStrongholdChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(ResidentStrongholdChancePercent));
        if (VantagePointChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(VantagePointChancePercent));
        if (ShortcutChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(ShortcutChancePercent));
        if (RestChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(RestChancePercent));
        if (SacrificeChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(SacrificeChancePercent));
        if (SecrecyLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(SecrecyLevel));
        ValidateWeights(EmptyWeight, CombatWeight, EventWeight);
        ValidateWeights(RegularEventWeight, RewardWeight, ShopWeight);
    }

    private static void ValidateWeights(params int[] weights)
    {
        if (weights.Any(w => w is < 0 or > 1_000_000) || weights.Sum() == 0)
            throw new ArgumentException("Weights must be nonnegative, at most 1,000,000, and have a positive total.");
    }
}
