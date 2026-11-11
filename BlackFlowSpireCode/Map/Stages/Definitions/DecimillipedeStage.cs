using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class DecimillipedeStage : ForestPairedStageDefinition
{
    public override string Id => "DECIMILLIPEDE";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new DecimillipedeNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new DecimillipedeEmergency();
}

#endregion

#region 普通作战

// 三段通行 / Normal. Edit this version independently of the other version.
//血量*0.6
public sealed class DecimillipedeNormal : ForestStageVersionDefinition
{
    public override string Id => "DECIMILLIPEDE.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.DECIMILLIPEDE_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.DECIMILLIPEDE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.DECIMILLIPEDE.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 0.6m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.DECIMILLIPEDE_SEGMENT_FRONT", native[0].Slot),
        new("MONSTER.DECIMILLIPEDE_SEGMENT_MIDDLE", native[1].Slot),
        new("MONSTER.DECIMILLIPEDE_SEGMENT_BACK", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 三段通行 / Emergency. Edit this version independently of the other version.
public sealed class DecimillipedeEmergency : ForestStageVersionDefinition
{
    public override string Id => "DECIMILLIPEDE.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.DECIMILLIPEDE_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.DECIMILLIPEDE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.DECIMILLIPEDE.description";
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
        new("MONSTER.DECIMILLIPEDE_SEGMENT_FRONT", native[0].Slot),
        new("MONSTER.DECIMILLIPEDE_SEGMENT_MIDDLE", native[1].Slot),
        new("MONSTER.DECIMILLIPEDE_SEGMENT_BACK", native[2].Slot)
    ];
}

#endregion
