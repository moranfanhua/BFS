using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class HauntedShipStage : ForestPairedStageDefinition
{
    public override string Id => "HAUNTED_SHIP";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new HauntedShipNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new HauntedShipEmergency();
}

#endregion

#region 普通作战

// 无岸航线 / Normal. Edit this version independently of the other version.
public sealed class HauntedShipNormal : ForestStageVersionDefinition
{
    public override string Id => "HAUNTED_SHIP.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.HAUNTED_SHIP_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.HAUNTED_SHIP.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.HAUNTED_SHIP.description";
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
        new("MONSTER.HAUNTED_SHIP", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 无岸航线 / Emergency. Edit this version independently of the other version.
//数值*1.2
public sealed class HauntedShipEmergency : ForestStageVersionDefinition
{
    public override string Id => "HAUNTED_SHIP.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.HAUNTED_SHIP_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.HAUNTED_SHIP.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.HAUNTED_SHIP.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.HAUNTED_SHIP", native[0].Slot)
    ];
}

#endregion
