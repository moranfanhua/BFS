using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class CorpseSlugsStage : ForestPairedStageDefinition
{
    public override string Id => "CORPSE_SLUGS";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new CorpseSlugsNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new CorpseSlugsEmergency();
}

#endregion

#region 普通作战

// 迟来送葬 / Normal. Edit this version independently of the other version.
public sealed class CorpseSlugsNormal : ForestStageVersionDefinition
{
    public override string Id => "CORPSE_SLUGS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.CORPSE_SLUGS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CORPSE_SLUGS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CORPSE_SLUGS.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.CORPSE_SLUG", native[0].Slot),
        new("MONSTER.CORPSE_SLUG", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 迟来送葬 / Emergency. Edit this version independently of the other version.
// 增加一只始终攻击的噬尸蛞蝓
public sealed class CorpseSlugsEmergency : ForestStageVersionDefinition
{
    public override string Id => "CORPSE_SLUGS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.CORPSE_SLUGS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CORPSE_SLUGS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CORPSE_SLUGS.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    public override IReadOnlyList<ForestMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.CORPSE_SLUG",
            FormationIndex = 2,
            MoveCycle = ["WHIP_SLAP_MOVE", "GLOMP_MOVE"],
            RemovedStartingPowers = ["POWER.RAVENOUS_POWER"]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        ..native,
        new("MONSTER.CORPSE_SLUG", null)
    ];

}

#endregion
