using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SeapunkStage : ForestPairedStageDefinition
{
    public override string Id => "SEAPUNK";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new SeapunkNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new SeapunkEmergency();
}

#endregion

#region 普通作战

// 岸上误会 / Normal. Edit this version independently of the other version.
public sealed class SeapunkNormal : ForestStageVersionDefinition
{
    public override string Id => "SEAPUNK.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SEAPUNK_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SEAPUNK.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SEAPUNK.description";
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
        new("MONSTER.CALCIFIED_CULTIST", native[0].Slot),
        new("MONSTER.SEAPUNK", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 岸上误会 / Emergency. Edit this version independently of the other version.
// 钙化邪教徒获得的buff层数增加
public sealed class SeapunkEmergency : ForestStageVersionDefinition
{
    public override string Id => "SEAPUNK.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SEAPUNK_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SEAPUNK.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SEAPUNK.description";
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
            MonsterId = "MONSTER.CALCIFIED_CULTIST",
            SelfPowerAmountOffsets = new() { ["POWER.RITUAL_POWER"] = 2m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.CALCIFIED_CULTIST", native[0].Slot),
        new("MONSTER.SEAPUNK", native[1].Slot)
    ];
}

#endregion
