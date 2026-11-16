using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class AeonglassStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "AEONGLASS";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new AeonglassBattle();
}

#endregion

#region 险路恶敌

// 余刻无穷 / Boss. Edit this version independently of the other version.
public sealed class AeonglassBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "AEONGLASS.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.AEONGLASS_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AEONGLASS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AEONGLASS.description";
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
        new("MONSTER.AEONGLASS", native[0].Slot)
    ];
}

#endregion
