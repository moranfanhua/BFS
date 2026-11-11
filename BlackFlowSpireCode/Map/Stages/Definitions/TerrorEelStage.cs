using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TerrorEelStage : ForestPairedStageDefinition
{
    public override string Id => "TERROR_EEL";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new TerrorEelNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new TerrorEelEmergency();
}

#endregion

#region 普通作战

// 深水禁声 / Normal. Edit this version independently of the other version.
//数值*0.8
public sealed class TerrorEelNormal : ForestStageVersionDefinition
{
    public override string Id => "TERROR_EEL.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.TERROR_EEL_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TERROR_EEL.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TERROR_EEL.description";
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
        new("MONSTER.TERROR_EEL", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 深水禁声 / Emergency. Edit this version independently of the other version.
public sealed class TerrorEelEmergency : ForestStageVersionDefinition
{
    public override string Id => "TERROR_EEL.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.TERROR_EEL_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TERROR_EEL.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TERROR_EEL.description";
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
        new("MONSTER.TERROR_EEL", native[0].Slot)
    ];
}

#endregion
