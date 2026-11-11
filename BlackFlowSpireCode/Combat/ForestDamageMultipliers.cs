using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Combat;

namespace BlackFlowSpire.Combat;

public sealed record ForestDamageMultipliers
{
    public decimal Attack { get; }
    public decimal Defense { get; }
    public decimal EnemyAttack { get; }
    public decimal EnemyDefense { get; }

    [JsonConstructor]
    public ForestDamageMultipliers(decimal attack = 1m, decimal defense = 1m,
        decimal enemyAttack = 1m, decimal enemyDefense = 1m)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attack);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defense);
        ArgumentOutOfRangeException.ThrowIfNegative(enemyAttack);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(enemyDefense);
        Attack = attack;
        Defense = defense;
        EnemyAttack = enemyAttack;
        EnemyDefense = enemyDefense;
    }

    internal decimal Apply(CombatSide dealerSide, decimal damage) => dealerSide switch
    {
        CombatSide.Player => Math.Ceiling(damage * Attack / EnemyDefense),
        CombatSide.Enemy => Math.Ceiling(damage * EnemyAttack / Defense),
        _ => damage
    };
}
