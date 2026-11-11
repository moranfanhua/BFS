using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class CultistsStage : ForestPairedStageDefinition
{
    public override string Id => "CULTISTS";
    public override string Region => "UNDERDOCKS";
    public override ForestStageVersionDefinition Normal { get; } = new CultistsNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new CultistsEmergency();
}

#endregion

#region 普通作战

// 受潮福音 / Normal. Edit this version independently of the other version.
public sealed class CultistsNormal : ForestStageVersionDefinition
{
    public override string Id => "CULTISTS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.CULTISTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CULTISTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CULTISTS.description";
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
        new("MONSTER.DAMP_CULTIST", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 受潮福音 / Emergency. Edit this version independently of the other version.
//潮湿邪教徒初始即携带其BUFF增益，且从首回合开始攻击。
public sealed class CultistsEmergency : ForestStageVersionDefinition
{
    public override string Id => "CULTISTS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.CULTISTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CULTISTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.CULTISTS.description";
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
            MonsterId = "MONSTER.DAMP_CULTIST",
            FirstMoveId = "DARK_STRIKE_MOVE",
            StartingPowers = [new("POWER.RITUAL_POWER", AmountProperty: "IncantationAmount")]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.CALCIFIED_CULTIST", native[0].Slot),
        new("MONSTER.DAMP_CULTIST", native[1].Slot)
    ];
}

#endregion
