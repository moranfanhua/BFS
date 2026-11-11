using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SlimesStage : ForestPairedStageDefinition
{
    public override string Id => "SLIMES";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Normal { get; } = new SlimesNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new SlimesEmergency();
}

#endregion
//暂定一层
#region 普通作战

// 清扫未遂 / Normal. Edit this version independently of the other version.
public sealed class SlimesNormal : ForestStageVersionDefinition
{
    public override string Id => "SLIMES.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SLIMES_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLIMES.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLIMES.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // 两只小史莱姆必须一叶一枝；中型史莱姆在树叶／树枝中二选一。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 清扫未遂 / Emergency. Edit this version independently of the other version.
//暂定中型也一叶一枝
public sealed class SlimesEmergency : ForestStageVersionDefinition
{
    public override string Id => "SLIMES.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SLIMES_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLIMES.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLIMES.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // 紧急编成：小型和中型均各一叶一枝。
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.LEAF_SLIME_S", null),
        new("MONSTER.LEAF_SLIME_M", null),
        new("MONSTER.TWIG_SLIME_M", null),
        new("MONSTER.TWIG_SLIME_S", null)
    ];
}

#endregion
