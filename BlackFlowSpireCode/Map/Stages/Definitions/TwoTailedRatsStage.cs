using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TwoTailedRatsStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "TWO_TAILED_RATS";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new TwoTailedRatsNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new TwoTailedRatsEmergency();
}

#endregion

#region 普通作战

// 鼠口相传 / Normal. Edit this version independently of the other version.
public sealed class TwoTailedRatsNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TWO_TAILED_RATS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.TWO_TAILED_RATS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TWO_TAILED_RATS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TWO_TAILED_RATS.description";
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
        new("MONSTER.TWO_TAILED_RAT", native[0].Slot),
        new("MONSTER.TWO_TAILED_RAT", native[1].Slot),
        new("MONSTER.TWO_TAILED_RAT", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 鼠口相传 / Emergency. Edit this version independently of the other version.
// 所有初始老鼠获得2点力量
public sealed class TwoTailedRatsEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TWO_TAILED_RATS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.TWO_TAILED_RATS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TWO_TAILED_RATS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TWO_TAILED_RATS.description";
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
            MonsterId = "MONSTER.TWO_TAILED_RAT",
            InitialOnly = true,
            StartingPowers = [new("POWER.STRENGTH_POWER", 2m)]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.TWO_TAILED_RAT", native[0].Slot),
        new("MONSTER.TWO_TAILED_RAT", native[1].Slot),
        new("MONSTER.TWO_TAILED_RAT", native[2].Slot)
    ];
}

#endregion
