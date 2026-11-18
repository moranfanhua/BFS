namespace BlackFlowSpire.Combat;

internal readonly record struct BlackFlowSpireHope(int Balance, int Maximum)
{
    public const int Initial = 6;

    public bool TryChange(int delta, out BlackFlowSpireHope result)
    {
        var balance = (long)Balance + delta;
        var maximum = (long)Maximum + Math.Max(delta, 0);
        result = this;
        if (balance < 0 || maximum > int.MaxValue) return false;
        result = new((int)balance, (int)maximum);
        return true;
    }
}

internal readonly record struct BlackFlowSpireCommandLevel(int Level, int Experience, int ExperienceToNext, int HopeEarned)
{
    // Indexed by the level being reached, starting at level 2.
    private static readonly (int Experience, int Hope)[] LevelUps =
    [
        (10, 4), (24, 4), (36, 5), (40, 5), (55, 6), (65, 6), (75, 6), (90, 6), (95, 8)
    ];
    public static readonly int MaximumExperience = LevelUps.Sum(step => step.Experience);

    public static BlackFlowSpireCommandLevel FromExperience(int totalExperience)
    {
        var experience = Math.Max(0, totalExperience);
        var hope = 0;
        for (var index = 0; index < LevelUps.Length; index++)
        {
            var step = LevelUps[index];
            if (experience < step.Experience) return new(index + 1, experience, step.Experience, hope);
            experience -= step.Experience;
            hope += step.Hope;
        }
        return new(LevelUps.Length + 1, 0, 0, hope);
    }
}
