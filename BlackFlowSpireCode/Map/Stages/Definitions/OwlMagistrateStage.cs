using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class OwlMagistrateStage : ForestPairedStageDefinition
{
    public override string Id => "OWL_MAGISTRATE";
    public override string Region => "GLORY";
    public override ForestStageVersionDefinition Normal { get; } = new OwlMagistrateNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new OwlMagistrateEmergency();
}

#endregion

#region 普通作战

// 有罪先行 / Normal. Edit this version independently of the other version.
public sealed class OwlMagistrateNormal : ForestStageVersionDefinition
{
    public override string Id => "OWL_MAGISTRATE.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.OWL_MAGISTRATE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OWL_MAGISTRATE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OWL_MAGISTRATE.description";
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
        new("MONSTER.OWL_MAGISTRATE", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 有罪先行 / Emergency. Edit this version independently of the other version.
//初始即处于翱翔 在首回合使用“Verdict”
public sealed class OwlMagistrateEmergency : ForestStageVersionDefinition
{
    public override string Id => "OWL_MAGISTRATE.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.OWL_MAGISTRATE_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OWL_MAGISTRATE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.OWL_MAGISTRATE.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    public override IReadOnlyList<ForestMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.OWL_MAGISTRATE",
            FirstMoveId = "VERDICT",
            RoomEntryMoveId = "JUDICIAL_FLIGHT"
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.OWL_MAGISTRATE", native[0].Slot)
    ];
}

#endregion
