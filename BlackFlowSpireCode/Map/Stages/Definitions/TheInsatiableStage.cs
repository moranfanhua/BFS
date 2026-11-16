using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TheInsatiableStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "THE_INSATIABLE";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new TheInsatiableBattle();
}

#endregion

#region 险路恶敌

// 饥壤之下 / Boss. Edit this version independently of the other version.
public sealed class TheInsatiableBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "THE_INSATIABLE.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.THE_INSATIABLE_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_INSATIABLE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_INSATIABLE.description";
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
        new("MONSTER.THE_INSATIABLE", native[0].Slot)
    ];
}

#endregion
