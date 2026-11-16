using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class FlyconidStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "FLYCONID";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new FlyconidNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new FlyconidEmergency();
}

#endregion

#region 普通作战

// 菌邻关系 / Normal. Edit this version independently of the other version.
public sealed class FlyconidNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "FLYCONID.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.FLYCONID_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FLYCONID.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FLYCONID.description";
    public override int Revision => 1;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 飞蝇菌子固定一只，中型树叶／树枝史莱姆二选一。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 菌邻关系 / Emergency. Edit this version independently of the other version.
// 暂定飞蝇菌子，中型树叶，树枝史莱姆各一个
public sealed class FlyconidEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "FLYCONID.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.FLYCONID_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FLYCONID.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FLYCONID.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 紧急编成：飞蝇菌子、中型树叶／树枝史莱姆各一只。
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.LEAF_SLIME_M", null),
        new("MONSTER.TWIG_SLIME_M", null),
        new("MONSTER.FLYCONID", null)
    ];
}

#endregion
