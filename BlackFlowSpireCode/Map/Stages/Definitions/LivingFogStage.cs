using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class LivingFogStage : ForestPairedStageDefinition
{
    public override string Id => "LIVING_FOG";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new LivingFogNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new LivingFogEmergency();
}

#endregion

#region 普通作战

// 泄漏报告 / Normal. Edit this version independently of the other version.
public sealed class LivingFogNormal : ForestStageVersionDefinition
{
    public override string Id => "LIVING_FOG.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.LIVING_FOG_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LIVING_FOG.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LIVING_FOG.description";
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
        new("MONSTER.LIVING_FOG", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 泄漏报告 / Emergency. Edit this version independently of the other version.
public sealed class LivingFogEmergency : ForestStageVersionDefinition
{
    public override string Id => "LIVING_FOG.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.LIVING_FOG_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LIVING_FOG.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LIVING_FOG.description";
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
        new("MONSTER.LIVING_FOG", native[0].Slot)
    ];
}

#endregion
