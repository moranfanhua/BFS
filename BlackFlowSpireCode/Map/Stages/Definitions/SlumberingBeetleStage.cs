using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SlumberingBeetleStage : ForestPairedStageDefinition
{
    public override string Id => "SLUMBERING_BEETLE";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new SlumberingBeetleNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new SlumberingBeetleEmergency();
}

#endregion

#region 普通作战

// 请勿叫醒 / Normal. Edit this version independently of the other version.
public sealed class SlumberingBeetleNormal : ForestStageVersionDefinition
{
    public override string Id => "SLUMBERING_BEETLE.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SLUMBERING_BEETLE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLUMBERING_BEETLE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLUMBERING_BEETLE.description";
    public override int Revision => 1;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.BOWLBUG_ROCK", native[0].Slot),
        new("MONSTER.BOWLBUG_SILK", native[1].Slot),
        new("MONSTER.SLUMBERING_BEETLE", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 请勿叫醒 / Emergency. Edit this version independently of the other version.
//熟睡甲虫提前一回合起床
public sealed class SlumberingBeetleEmergency : ForestStageVersionDefinition
{
    public override string Id => "SLUMBERING_BEETLE.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SLUMBERING_BEETLE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLUMBERING_BEETLE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLUMBERING_BEETLE.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    public override IReadOnlyList<ForestMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.SLUMBERING_BEETLE",
            SelfPowerAmountOffsets = new() { ["POWER.SLUMBER_POWER"] = -1m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.BOWLBUG_ROCK", native[0].Slot),
        new("MONSTER.BOWLBUG_SILK", native[1].Slot),
        new("MONSTER.SLUMBERING_BEETLE", native[2].Slot)
    ];
}

#endregion
