using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class VantomStage : ForestBossStageDefinition
{
    public override string Id => "VANTOM";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Boss { get; } = new VantomBattle();
}

#endregion

#region 险路恶敌

// 墨夜留形 / Boss. Edit this version independently of the other version.
public sealed class VantomBattle : ForestStageVersionDefinition
{
    public override string Id => "VANTOM.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.VANTOM_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VANTOM.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VANTOM.description";
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
        new("MONSTER.VANTOM", native[0].Slot)
    ];
}

#endregion
