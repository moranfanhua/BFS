using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class PhantasmalGardenersStage : ForestPairedStageDefinition
{
    public override string Id => "PHANTASMAL_GARDENERS";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new PhantasmalGardenersNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new PhantasmalGardenersEmergency();
}

#endregion

#region 普通作战

// 四季同席 / Normal. Edit this version independently of the other version.
// 所有敌人数值*0.8
public sealed class PhantasmalGardenersNormal : ForestStageVersionDefinition
{
    public override string Id => "PHANTASMAL_GARDENERS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.PHANTASMAL_GARDENERS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHANTASMAL_GARDENERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHANTASMAL_GARDENERS.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 0.8m,
        AttackDamageMultiplier = 0.8m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.PHANTASMAL_GARDENER", native[0].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[1].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[2].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[3].Slot)
    ];
}

#endregion

#region 紧急作战

// 四季同席 / Emergency. Edit this version independently of the other version.
public sealed class PhantasmalGardenersEmergency : ForestStageVersionDefinition
{
    public override string Id => "PHANTASMAL_GARDENERS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.PHANTASMAL_GARDENERS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHANTASMAL_GARDENERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHANTASMAL_GARDENERS.description";
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
        new("MONSTER.PHANTASMAL_GARDENER", native[0].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[1].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[2].Slot),
        new("MONSTER.PHANTASMAL_GARDENER", native[3].Slot)
    ];
}

#endregion
