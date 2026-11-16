using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class KnowledgeDemonStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "KNOWLEDGE_DEMON";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new KnowledgeDemonBattle();
}

#endregion

#region 险路恶敌

// 求知代价 / Boss. Edit this version independently of the other version.
public sealed class KnowledgeDemonBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "KNOWLEDGE_DEMON.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.KNOWLEDGE_DEMON_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNOWLEDGE_DEMON.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNOWLEDGE_DEMON.description";
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
        new("MONSTER.KNOWLEDGE_DEMON", native[0].Slot)
    ];
}

#endregion
