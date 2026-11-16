using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class BygoneEffigyStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "BYGONE_EFFIGY";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new BygoneEffigyNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new BygoneEffigyEmergency();
}

#endregion

#region 普通作战

// 敬礼之后 / Normal. Edit this version independently of the other version.
public sealed class BygoneEffigyNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "BYGONE_EFFIGY.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.BYGONE_EFFIGY_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BYGONE_EFFIGY.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BYGONE_EFFIGY.description";
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
        new("MONSTER.BYGONE_EFFIGY", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 敬礼之后 / Emergency. Edit this version independently of the other version.
//攻击提前一回合。
public sealed class BygoneEffigyEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "BYGONE_EFFIGY.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.BYGONE_EFFIGY_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BYGONE_EFFIGY.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BYGONE_EFFIGY.description";
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
            MonsterId = "MONSTER.BYGONE_EFFIGY",
            FirstMoveId = "WAKE_MOVE"
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.BYGONE_EFFIGY", native[0].Slot)
    ];
}

#endregion
