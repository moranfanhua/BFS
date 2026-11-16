using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class WaterfallGiantStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "WATERFALL_GIANT";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new WaterfallGiantBattle();
}

#endregion

#region 险路恶敌

// 逆瀑之躯 / Boss. Edit this version independently of the other version.
public sealed class WaterfallGiantBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "WATERFALL_GIANT.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.WATERFALL_GIANT_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.WATERFALL_GIANT.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.WATERFALL_GIANT.description";
    public override int Revision => 1;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.WATERFALL_GIANT", native[0].Slot)
    ];
}

#endregion
