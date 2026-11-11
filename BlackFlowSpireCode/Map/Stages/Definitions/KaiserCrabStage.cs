using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class KaiserCrabStage : ForestBossStageDefinition
{
    public override string Id => "KAISER_CRAB";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Boss { get; } = new KaiserCrabBattle();
}

#endregion

#region 险路恶敌

// 钳下王土 / Boss. Edit this version independently of the other version.
public sealed class KaiserCrabBattle : ForestStageVersionDefinition
{
    public override string Id => "KAISER_CRAB.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.KAISER_CRAB_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KAISER_CRAB.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KAISER_CRAB.description";
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
        new("MONSTER.CRUSHER", native[0].Slot),
        new("MONSTER.ROCKET", native[1].Slot)
    ];
}

#endregion
