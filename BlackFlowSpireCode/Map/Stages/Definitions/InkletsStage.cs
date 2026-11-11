using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class InkletsStage : ForestPairedStageDefinition
{
    public override string Id => "INKLETS";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Normal { get; } = new InkletsNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new InkletsEmergency();
}

#endregion

#region 普通作战

// 未干证词 / Normal. Edit this version independently of the other version.
public sealed class InkletsNormal : ForestStageVersionDefinition
{
    public override string Id => "INKLETS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.INKLETS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.INKLETS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.INKLETS.description";
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
        new("MONSTER.INKLET", native[0].Slot),
        new("MONSTER.INKLET", native[1].Slot),
        new("MONSTER.INKLET", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 未干证词 / Emergency. Edit this version independently of the other version.
// 简单多一个
public sealed class InkletsEmergency : ForestStageVersionDefinition
{
    public override string Id => "INKLETS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.INKLETS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.INKLETS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.INKLETS.description";
    public override int Revision => 2;
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
        ..native,
        new("MONSTER.INKLET", null)
    ];

}

#endregion
