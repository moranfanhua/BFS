namespace BlackFlowSpire.Combat;

internal sealed class ForestPlayerCombatLife(int baseMaximum, decimal multiplier)
{
    private const int MaxSupportedLife = 999_999_999;

    public int BaseMaximum { get; private set; } = baseMaximum;

    public int CombatMaximum
    {
        get
        {
            if (BaseMaximum == 0) return 0;
            if (multiplier >= (decimal)MaxSupportedLife / BaseMaximum) return MaxSupportedLife;
            return Math.Max(1, (int)decimal.Round(BaseMaximum * multiplier, 0, MidpointRounding.AwayFromZero));
        }
    }

    public int AdjustBaseMaximum(int previousCombatMaximum, decimal requestedMaximum)
    {
        // Native gain/loss commands request a new maximum relative to the displayed maximum.
        // Keep that difference as a permanent change, then recalculate the combat bonus.
        var maximum = decimal.Truncate(requestedMaximum);
        var change = Math.Clamp(maximum - previousCombatMaximum, -MaxSupportedLife, MaxSupportedLife);
        BaseMaximum = maximum == 0m ? 0 : (int)Math.Clamp(BaseMaximum + change, 1m, MaxSupportedLife);
        return CombatMaximum;
    }
}
