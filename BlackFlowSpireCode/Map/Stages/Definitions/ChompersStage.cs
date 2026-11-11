using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class ChompersStage : ForestPairedStageDefinition
{
    public override string Id => "CHOMPERS";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new ChompersNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new ChompersEmergency();
}

#endregion

#region 普通作战

// 轮班开席 / Normal. Edit this version independently of the other version.
public sealed class ChompersNormal : ForestStageVersionDefinition
{
    public override string Id => "CHOMPERS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.CHOMPERS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CHOMPERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CHOMPERS.description";
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
        new("MONSTER.CHOMPER", native[0].Slot),
        new("MONSTER.CHOMPER", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 轮班开席 / Emergency. Edit this version independently of the other version.
//施加状态时多给一张debuff
public sealed class ChompersEmergency : ForestStageVersionDefinition
{
    public override string Id => "CHOMPERS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.CHOMPERS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CHOMPERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CHOMPERS.description";
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
            MonsterId = "MONSTER.CHOMPER",
            StatusCardCountOffset = 1
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.CHOMPER", native[0].Slot),
        new("MONSTER.CHOMPER", native[1].Slot)
    ];
}

#endregion
