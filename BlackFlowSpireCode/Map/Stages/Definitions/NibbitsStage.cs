using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class NibbitsStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "NIBBITS";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new NibbitsNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new NibbitsEmergency();
}

#endregion

#region 普通作战

// 独享午餐 / Normal. Edit this version independently of the other version.
//暂定1层
public sealed class NibbitsNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "NIBBITS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.NIBBITS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.NIBBITS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.NIBBITS.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.NIBBIT", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 独享午餐 / Emergency. Edit this version independently of the other version.
// 暂定2再生
public sealed class NibbitsEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "NIBBITS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.NIBBITS_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.NIBBITS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.NIBBITS.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    public override IReadOnlyList<BlackFlowSpireMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.NIBBIT",
            StartingPowers = [new("POWER.REGEN_POWER", 2m)]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.NIBBIT", native[0].Slot)
    ];
}

#endregion
