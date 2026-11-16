using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class BowlbugPairStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "BOWLBUG_PAIR";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new BowlbugPairNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new BowlbugPairEmergency();
}

#endregion

#region 普通作战

// 小份劳作 / Normal. Edit this version independently of the other version.
//*1.2
public sealed class BowlbugPairNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "BOWLBUG_PAIR.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.BOWLBUGS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_PAIR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_PAIR.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // 盛碗虫（石）固定一只，另从卵／蜜中选一只；不会选丝。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 小份劳作 / Emergency. Edit this version independently of the other version.
//*1.5
public sealed class BowlbugPairEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "BOWLBUG_PAIR.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.BOWLBUGS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_PAIR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_PAIR.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.5m,
        AttackDamageMultiplier = 1.5m
    };

    // 盛碗虫（石）固定一只，另从卵／蜜中选一只；不会选丝。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) => native;
}

#endregion
