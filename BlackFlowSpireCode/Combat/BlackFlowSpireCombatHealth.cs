namespace BlackFlowSpire.Combat;

internal sealed class BlackFlowSpireCombatHealth
{
    public const int InitialTargetLife = 10;
    private long _teamHpLost;

    public void RecordLoss(int hpLost) => _teamHpLost += hpLost;

    public (int LifeLost, int ShieldUsed) GetLosses(long teamMaxHp, int targetLife, int shield = 0, int loveGrassCount = 0)
    {
        if (teamMaxHp <= 0) return default;
        // Subtract the free 10%, then count complete 5% steps using the whole team's HP.
        var steps = Math.Max(0, _teamHpLost * 100 - teamMaxHp * 10) / (teamMaxHp * 5);
        var totalAvailable = (long)targetLife + shield;
        while (loveGrassCount-- > 0 && steps > 0 && steps < totalAvailable)
            steps *= 2;
        var totalLost = Math.Min(totalAvailable, steps);
        var shieldUsed = (int)Math.Min(shield, totalLost);
        return ((int)(totalLost - shieldUsed), shieldUsed);
    }
}
