using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages;

internal readonly record struct BlackFlowSpireBattleLoot(int Originium, int Experience, bool CardPack,
    int CollectibleChance = 0, int CollectibleChoices = 1, int PartChance = 0, int PartGroups = 1,
    int SinglePartChance = 0)
{
    private static readonly int[] NormalIngots = [1, 2, 2, 2, 2];
    private static readonly int[] EmergencyIngots = [2, 2, 3, 3, 5];
    private static readonly int[] NormalExperience = [10, 12, 13, 15, 20];
    private static readonly int[] EmergencyExperience = [12, 18, 25, 30, 36];

    internal static BlackFlowSpireBattleLoot For(BlackFlowSpireMap map, BlackFlowSpireCombatSetup setup, bool finalFloor)
    {
        var floor = map.ActIndex;
        if (setup.Special == BlackFlowSpireSpecialEncounter.Resident)
            return map.GetNode(map.PositionNodeId).Content == BlackFlowSpireNodeContent.BattleSavage
                ? new(5, 25, true, CollectibleChance: 10000, PartChance: 10000, PartGroups: 2)
                : new(3, 15, true, CollectibleChance: 3250, PartChance: 10000);

        var variant = setup.Stage?.Variant ?? BlackFlowSpireBattleVariant.Normal;
        if (setup.Special == BlackFlowSpireSpecialEncounter.Hunt)
        {
            var exit = map.GetNode(map.BossNodeId);
            if (exit.Content != BlackFlowSpireNodeContent.Boss || exit.Completed) return new(0, 0, true);
            variant = BlackFlowSpireBattleVariant.Boss;
        }

        var experience = BattleExperience(floor, variant);
        return variant switch
        {
            BlackFlowSpireBattleVariant.Boss => finalFloor
                ? new(experience / 6, experience, false)
                : new(experience / 6, experience, true, CollectibleChance: 10000, CollectibleChoices: 2, PartChance: 10000),
            BlackFlowSpireBattleVariant.Emergency => new(EmergencyIngots[floor], experience, true,
                CollectibleChance: 10000, PartChance: 10000),
            _ => new(NormalIngots[floor], experience, true, CollectibleChance: 892, PartChance: 3250, SinglePartChance: 1000)
        };
    }

    internal static int BattleExperience(int floor, BlackFlowSpireBattleVariant variant) => variant switch
    {
        BlackFlowSpireBattleVariant.Normal => NormalExperience[floor],
        BlackFlowSpireBattleVariant.Emergency => EmergencyExperience[floor],
        // II and IV use the Mod's existing 2.5x normal XP for stages with no original boss.
        BlackFlowSpireBattleVariant.Boss when floor is 1 or 3 => NormalExperience[floor] * 5 / 2,
        BlackFlowSpireBattleVariant.Boss => floor == NormalExperience.Length - 1 ? 50 : 32,
        _ => throw new ArgumentOutOfRangeException(nameof(variant))
    };

    internal static bool Roll(StableRandom random, int chance) => chance == 10000 || chance > 0 && random.Next(10000) < chance;

    internal int RollPartChoices(StableRandom random) => !Roll(random, PartChance) ? 0
        : Roll(random, SinglePartChance) ? 1 : 2;
}
