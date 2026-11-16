using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SoulFyshStage : BlackFlowSpireBossStageDefinition
{
    public override string Id => "SOUL_FYSH";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Boss { get; } = new SoulFyshBattle();
}

#endregion

#region 险路恶敌

// 渡魂无岸 / Boss. Edit this version independently of the other version.
public sealed class SoulFyshBattle : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "SOUL_FYSH.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.SOUL_FYSH_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_FYSH.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_FYSH.description";
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
        new("MONSTER.SOUL_FYSH", native[0].Slot)
    ];
}

#endregion
