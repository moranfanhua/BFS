using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class FrogKnightStage : ForestPairedStageDefinition
{
    public override string Id => "FROG_KNIGHT";
    public override string Region => "GLORY";
    public override ForestStageVersionDefinition Normal { get; } = new FrogKnightNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new FrogKnightEmergency();
}

#endregion

#region 普通作战

// 湿地封爵 / Normal. Edit this version independently of the other version.
public sealed class FrogKnightNormal : ForestStageVersionDefinition
{
    public override string Id => "FROG_KNIGHT.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.FROG_KNIGHT_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FROG_KNIGHT.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FROG_KNIGHT.description";
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
        new("MONSTER.FROG_KNIGHT", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 湿地封爵 / Emergency. Edit this version independently of the other version.
//初始额外获得五点覆甲
public sealed class FrogKnightEmergency : ForestStageVersionDefinition
{
    public override string Id => "FROG_KNIGHT.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.FROG_KNIGHT_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FROG_KNIGHT.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FROG_KNIGHT.description";
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
            MonsterId = "MONSTER.FROG_KNIGHT",
            SelfPowerAmountOffsets = new() { ["POWER.PLATING_POWER"] = 5m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.FROG_KNIGHT", native[0].Slot)
    ];
}

#endregion
