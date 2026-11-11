using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TheObscuraStage : ForestPairedStageDefinition
{
    public override string Id => "THE_OBSCURA";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new TheObscuraNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new TheObscuraEmergency();
}

#endregion

#region 普通作战

// 光后来客 / Normal. Edit this version independently of the other version.
public sealed class TheObscuraNormal : ForestStageVersionDefinition
{
    public override string Id => "THE_OBSCURA.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.THE_OBSCURA_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_OBSCURA.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_OBSCURA.description";
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
        new("MONSTER.THE_OBSCURA", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 光后来客 / Emergency. Edit this version independently of the other version.
//生命值增加20
public sealed class TheObscuraEmergency : ForestStageVersionDefinition
{
    public override string Id => "THE_OBSCURA.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.THE_OBSCURA_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_OBSCURA.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_OBSCURA.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m,
        HealthOffset = 20
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.THE_OBSCURA", native[0].Slot)
    ];
}

#endregion
