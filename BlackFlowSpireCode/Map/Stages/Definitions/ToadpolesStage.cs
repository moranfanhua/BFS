using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class ToadpolesStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "TOADPOLES";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new ToadpolesNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new ToadpolesEmergency();
}

#endregion

#region 普通作战

// 浅滩新客 / Normal. Edit this version independently of the other version.
public sealed class ToadpolesNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TOADPOLES.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.TOADPOLES_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TOADPOLES.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TOADPOLES.description";
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
        new("MONSTER.TOADPOLE", native[0].Slot),
        new("MONSTER.TOADPOLE", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 浅滩新客 / Emergency. Edit this version independently of the other version.
//数值*1.5
public sealed class ToadpolesEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TOADPOLES.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.TOADPOLES_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TOADPOLES.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TOADPOLES.description";
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
        new("MONSTER.TOADPOLE", native[0].Slot),
        new("MONSTER.TOADPOLE", native[1].Slot)
    ];
}

#endregion
