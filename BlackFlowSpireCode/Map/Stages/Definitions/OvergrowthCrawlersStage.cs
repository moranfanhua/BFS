using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class OvergrowthCrawlersStage : ForestPairedStageDefinition
{
    public override string Id => "OVERGROWTH_CRAWLERS";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Normal { get; } = new OvergrowthCrawlersNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new OvergrowthCrawlersEmergency();
}

#endregion

#region 普通作战

// 尺度偏差 / Normal. Edit this version independently of the other version.
public sealed class OvergrowthCrawlersNormal : ForestStageVersionDefinition
{
    public override string Id => "OVERGROWTH_CRAWLERS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.OVERGROWTH_CRAWLERS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVERGROWTH_CRAWLERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVERGROWTH_CRAWLERS.description";
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
        new("MONSTER.SHRINKER_BEETLE", native[0].Slot),
        new("MONSTER.FUZZY_WURM_CRAWLER", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 尺度偏差 / Emergency. Edit this version independently of the other version.
public sealed class OvergrowthCrawlersEmergency : ForestStageVersionDefinition
{
    public override string Id => "OVERGROWTH_CRAWLERS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.OVERGROWTH_CRAWLERS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVERGROWTH_CRAWLERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OVERGROWTH_CRAWLERS.description";
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
        new("MONSTER.SHRINKER_BEETLE", native[0].Slot),
        new("MONSTER.FUZZY_WURM_CRAWLER", native[1].Slot)
    ];
}

#endregion
