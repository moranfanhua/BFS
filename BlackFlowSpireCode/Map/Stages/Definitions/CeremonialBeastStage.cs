using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class CeremonialBeastStage : ForestBossStageDefinition
{
    public override string Id => "CEREMONIAL_BEAST";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Boss { get; } = new CeremonialBeastBattle();
}

#endregion

#region 险路恶敌

// 祭礼未终 / Boss. Edit this version independently of the other version.
public sealed class CeremonialBeastBattle : ForestStageVersionDefinition
{
    public override string Id => "CEREMONIAL_BEAST.BOSS";
    public override string SourceEncounterId => "ENCOUNTER.CEREMONIAL_BEAST_BOSS";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CEREMONIAL_BEAST.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CEREMONIAL_BEAST.description";
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
        new("MONSTER.CEREMONIAL_BEAST", native[0].Slot)
    ];
}

#endregion
