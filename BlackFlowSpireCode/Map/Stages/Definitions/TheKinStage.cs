using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TheKinStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "THE_KIN";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new TheKinBattle();
}

#endregion

#region 险路恶敌

// 合家之礼 / Boss. Edit this version independently of the other version.
public sealed class TheKinBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "THE_KIN.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.THE_KIN_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_KIN.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_KIN.description";
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
        new("MONSTER.KIN_FOLLOWER", native[0].Slot),
        new("MONSTER.KIN_FOLLOWER", native[1].Slot),
        new("MONSTER.KIN_PRIEST", native[2].Slot)
    ];
}

#endregion
