using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TestSubjectStage : ForestBossStageDefinition
{
    public override string Id => "TEST_SUBJECT";
    public override string Region => "GLORY";
    public override ForestStageVersionDefinition Boss { get; } = new TestSubjectBattle();
}

#endregion

#region 险路恶敌

// 结案之前 / Boss. Edit this version independently of the other version.
public sealed class TestSubjectBattle : ForestStageVersionDefinition
{
    public override string Id => "TEST_SUBJECT.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.TEST_SUBJECT_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TEST_SUBJECT.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TEST_SUBJECT.description";
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
        new("MONSTER.TEST_SUBJECT", native[0].Slot)
    ];
}

#endregion
