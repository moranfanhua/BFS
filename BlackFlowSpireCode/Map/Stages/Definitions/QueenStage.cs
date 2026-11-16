using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class QueenStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "QUEEN";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new QueenBattle();
}

#endregion

#region 险路恶敌

// 末席之臣 / Boss. Edit this version independently of the other version.
public sealed class QueenBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "QUEEN.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.QUEEN_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.QUEEN.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.QUEEN.description";
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
        new("MONSTER.TORCH_HEAD_AMALGAM", native[0].Slot),
        new("MONSTER.QUEEN", native[1].Slot)
    ];
}

#endregion
