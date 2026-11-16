using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class ConstructMenagerieStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "CONSTRUCT_MENAGERIE";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new ConstructMenagerieNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new ConstructMenagerieEmergency();
}

#endregion

#region 普通作战

// 标准误差 / Normal. Edit this version independently of the other version.
public sealed class ConstructMenagerieNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "CONSTRUCT_MENAGERIE.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.CONSTRUCT_MENAGERIE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CONSTRUCT_MENAGERIE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CONSTRUCT_MENAGERIE.description";
    public override int Revision => 1;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.PUNCH_CONSTRUCT", native[0].Slot),
        new("MONSTER.CUBEX_CONSTRUCT", native[1].Slot),
        new("MONSTER.CUBEX_CONSTRUCT", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 标准误差 / Emergency. Edit this version independently of the other version.
//方柱构装体初始额外获得10点格挡
public sealed class ConstructMenagerieEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "CONSTRUCT_MENAGERIE.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.CONSTRUCT_MENAGERIE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CONSTRUCT_MENAGERIE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CONSTRUCT_MENAGERIE.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    public override IReadOnlyList<BlackFlowSpireMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.CUBEX_CONSTRUCT",
            StartingBlock = 10m
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.PUNCH_CONSTRUCT", native[0].Slot),
        new("MONSTER.CUBEX_CONSTRUCT", native[1].Slot),
        new("MONSTER.CUBEX_CONSTRUCT", native[2].Slot)
    ];
}

#endregion
