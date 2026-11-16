using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class LagavulinMatriarchStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "LAGAVULIN_MATRIARCH";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new LagavulinMatriarchBattle();
}

#endregion

#region 险路恶敌

// 旧梦守门 / Boss. Edit this version independently of the other version.
public sealed class LagavulinMatriarchBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "LAGAVULIN_MATRIARCH.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.LAGAVULIN_MATRIARCH_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LAGAVULIN_MATRIARCH.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.LAGAVULIN_MATRIARCH.description";
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
        new("MONSTER.LAGAVULIN_MATRIARCH", native[0].Slot)
    ];
}

#endregion
