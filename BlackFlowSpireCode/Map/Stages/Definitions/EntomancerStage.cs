using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class EntomancerStage : ForestPairedStageDefinition
{
    public override string Id => "ENTOMANCER";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new EntomancerNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new EntomancerEmergency();
}

#endregion

#region 普通作战

// 蜂拥来信 / Normal. Edit this version independently of the other version.
//初始不带人体蜂房 血量*0.6
public sealed class EntomancerNormal : ForestStageVersionDefinition
{
    public override string Id => "ENTOMANCER.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.ENTOMANCER_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.ENTOMANCER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.ENTOMANCER.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 0.6m,
        AttackDamageMultiplier = 1m
    };

    public override IReadOnlyList<ForestMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.ENTOMANCER",
            RemovedStartingPowers = ["POWER.PERSONAL_HIVE_POWER"]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.ENTOMANCER", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 蜂拥来信 / Emergency. Edit this version independently of the other version.
public sealed class EntomancerEmergency : ForestStageVersionDefinition
{
    public override string Id => "ENTOMANCER.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.ENTOMANCER_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.ENTOMANCER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.ENTOMANCER.description";
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
        new("MONSTER.ENTOMANCER", native[0].Slot)
    ];
}

#endregion
