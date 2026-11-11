namespace BlackFlowSpire.Combat;

internal sealed class ForestCombatHealth
{
    public const int InitialTargetLife = 10;
    private long _teamHpLost;

    public void RecordLoss(int hpLost) => _teamHpLost += hpLost;

    public int GetTargetLifeLoss(long teamMaxHp, int loveGrassCount = 0)
    {
        if (teamMaxHp <= 0) return 0;
        // Subtract the free 10%, then count complete 5% steps using the whole team's HP.
        var steps = Math.Max(0, _teamHpLost * 100 - teamMaxHp * 10) / (teamMaxHp * 5);
        while (loveGrassCount-- > 0 && steps is > 0 and < InitialTargetLife)
            steps *= 2;
        return (int)Math.Min(InitialTargetLife, steps);
    }
}
