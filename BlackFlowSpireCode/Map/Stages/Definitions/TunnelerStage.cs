using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TunnelerStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "TUNNELER";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new TunnelerNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new TunnelerEmergency();
}

#endregion

#region 普通作战

// 邻里暗道 / Normal. Edit this version independently of the other version.
//*1.2
public sealed class TunnelerNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TUNNELER.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.TUNNELER_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TUNNELER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TUNNELER.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.TUNNELER", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 邻里暗道 / Emergency. Edit this version independently of the other version.
//*1.5
public sealed class TunnelerEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TUNNELER.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.TUNNELER_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TUNNELER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TUNNELER.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.5m,
        AttackDamageMultiplier = 1.5m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.TUNNELER", native[0].Slot)
    ];
}

#endregion
