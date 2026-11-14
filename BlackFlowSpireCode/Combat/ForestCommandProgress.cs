using BlackFlowSpire.Map.Stages;

namespace BlackFlowSpire.Combat;

internal readonly record struct ForestHope(int Balance, int Maximum)
{
    public const int Initial = 6;

    public static ForestHope FromSaved(int balance, int? maximum)
    {
        balance = Math.Max(0, balance);
        return new(balance, Math.Max(balance, maximum ?? Initial));
    }

    public bool TryChange(int delta, out ForestHope result)
    {
        var balance = (long)Balance + delta;
        var maximum = (long)Maximum + Math.Max(delta, 0);
        result = this;
        if (balance < 0 || maximum > int.MaxValue) return false;
        result = new((int)balance, (int)maximum);
        return true;
    }
}

internal readonly record struct ForestCommandLevel(int Level, int Experience, int ExperienceToNext, int HopeEarned)
{
    // Indexed by the level being reached, starting at level 2.
    private static readonly (int Experience, int Hope)[] LevelUps =
    [
        (10, 4), (24, 4), (36, 5), (40, 5), (55, 6), (65, 6), (75, 6), (90, 6), (95, 8)
    ];
    public static readonly int MaximumExperience = LevelUps.Sum(step => step.Experience);

    public static ForestCommandLevel FromExperience(int totalExperience)
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

    private static readonly int[] NormalExperience = [10, 12, 13, 15, 20];
    private static readonly int[] EmergencyExperience = [12, 18, 25, 30, 36];

    // BFS uses the original base rewards by floor. Its earlier boss rooms use the first boss reward.
    public static int BattleExperience(int actIndex, ForestBattleVariant variant) => variant switch
    {
        ForestBattleVariant.Normal => NormalExperience[actIndex],
        ForestBattleVariant.Emergency => EmergencyExperience[actIndex],
        ForestBattleVariant.Boss => actIndex == NormalExperience.Length - 1 ? 50 : 32,
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };
}
