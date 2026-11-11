namespace BlackFlowSpire.Map.Stages;

// These multipliers adjust initial HP and base move attack damage, respectively.
// Native Strength/Weak and other effects are applied after the base attack scaling.
public sealed record ForestMonsterBalance
{
    public decimal HealthMultiplier { get; init; } = 1m;
    public decimal AttackDamageMultiplier { get; init; } = 1m;
    public int HealthOffset { get; init; }

    public void Validate()
    {
        if (HealthMultiplier is <= 0m or > 10m || AttackDamageMultiplier is <= 0m or > 10m)
            throw new InvalidDataException("Balance multipliers must be greater than zero and at most ten.");
    }
}

public sealed record ForestBalanceProfile
{
    public decimal HealthMultiplier { get; init; } = 1m;
    public decimal AttackDamageMultiplier { get; init; } = 1m;
    public int HealthOffset { get; init; }
    public int SummonedHealthOffset { get; init; }
    // Optional additional multipliers, keyed by full native MONSTER.X IDs.
    public Dictionary<string, ForestMonsterBalance> Monsters { get; init; } = [];

    public void Validate()
    {
        new ForestMonsterBalance { HealthMultiplier = HealthMultiplier, AttackDamageMultiplier = AttackDamageMultiplier }.Validate();
        if (Monsters == null || Monsters.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null))
            throw new InvalidDataException("Invalid per-monster balance overrides.");
        foreach (var item in Monsters.Values) item.Validate();
    }

    public ForestBalanceProfile Copy() => this with
    {
        Monsters = Monsters.ToDictionary(pair => pair.Key, pair => pair.Value with { }, StringComparer.Ordinal)
    };

    public int ScaleHealth(int health, string monsterId, bool summoned = false)
    {
        var extra = Monsters.TryGetValue(monsterId, out var item) ? item.HealthMultiplier : 1m;
        var offset = (decimal)HealthOffset + (item?.HealthOffset ?? 0) + (summoned ? SummonedHealthOffset : 0);
        return (int)Math.Clamp(decimal.Round(health * HealthMultiplier * extra + offset, 0, MidpointRounding.AwayFromZero), 1m, 999999999m);
    }

    public decimal ScaleAttack(decimal damage, string monsterId)
    {
        var extra = Monsters.TryGetValue(monsterId, out var item) ? item.AttackDamageMultiplier : 1m;
        return damage * AttackDamageMultiplier * extra;
    }
}
