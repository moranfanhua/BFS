using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class FabricatorStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "FABRICATOR";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new FabricatorNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new FabricatorEmergency();
}

#endregion

#region 普通作战

// 缺员补足 / Normal. Edit this version independently of the other version.
public sealed class FabricatorNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "FABRICATOR.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.FABRICATOR_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FABRICATOR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FABRICATOR.description";
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
        new("MONSTER.FABRICATOR", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 缺员补足 / Emergency. Edit this version independently of the other version.
//组装师血量+20，所有召唤物血量+5
public sealed class FabricatorEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "FABRICATOR.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.FABRICATOR_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FABRICATOR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.FABRICATOR.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m,
        SummonedHealthOffset = 5,
        Monsters = new()
        {
            ["MONSTER.FABRICATOR"] = new() { HealthOffset = 20 }
        }
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.FABRICATOR", native[0].Slot)
    ];
}

#endregion
