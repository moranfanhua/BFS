using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class OvicopterStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "OVICOPTER";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new OvicopterNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new OvicopterEmergency();
}

#endregion

#region 普通作战

// 育儿车间 / Normal. Edit this version independently of the other version.
public sealed class OvicopterNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "OVICOPTER.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.OVICOPTER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVICOPTER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVICOPTER.description";
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
        new("MONSTER.OVICOPTER", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 育儿车间 / Emergency. Edit this version independently of the other version.
public sealed class OvicopterEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "OVICOPTER.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.OVICOPTER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVICOPTER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVICOPTER.description";
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
        new("MONSTER.OVICOPTER", native[0].Slot)
    ];
}

#endregion
