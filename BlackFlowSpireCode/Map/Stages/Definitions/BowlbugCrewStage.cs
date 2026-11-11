using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class BowlbugCrewStage : ForestPairedStageDefinition
{
    public override string Id => "BOWLBUG_CREW";
    public override string Region => "HIVE";
    public override ForestStageVersionDefinition Normal { get; } = new BowlbugCrewNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new BowlbugCrewEmergency();
}

#endregion

#region 普通作战

// 分工会议 / Normal. Edit this version independently of the other version.

public sealed class BowlbugCrewNormal : ForestStageVersionDefinition
{
    public override string Id => "BOWLBUG_CREW.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.BOWLBUGS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_CREW.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_CREW.description";
    public override int Revision => 1;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 盛碗虫（石）固定一只，另从卵／丝／蜜中选不同的两种，每种最多一只。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 分工会议 / Emergency. Edit this version independently of the other version.
//盛碗虫四只 石，卵，蜜，丝各一只
public sealed class BowlbugCrewEmergency : ForestStageVersionDefinition
{
    public override string Id => "BOWLBUG_CREW.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.BOWLBUGS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_CREW.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.BOWLBUG_CREW.description";
    public override int Revision => 2;
    public override bool UseGenericEnemyLayout => true;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 紧急编成固定为四种各一只，使用原版自动排布。
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.BOWLBUG_ROCK", null),
        new("MONSTER.BOWLBUG_EGG", null),
        new("MONSTER.BOWLBUG_NECTAR", null),
        new("MONSTER.BOWLBUG_SILK", null)
    ];
}

#endregion
