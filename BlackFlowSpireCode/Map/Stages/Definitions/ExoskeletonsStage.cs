using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class ExoskeletonsStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "EXOSKELETONS";
    public override string Region => "HIVE";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new ExoskeletonsNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new ExoskeletonsEmergency();
}

#endregion

#region 普通作战

// 空壳列队 / Normal. Edit this version independently of the other version.
public sealed class ExoskeletonsNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "EXOSKELETONS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.EXOSKELETONS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.EXOSKELETONS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.EXOSKELETONS.description";
    public override int Revision => 1;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.EXOSKELETON", native[0].Slot),
        new("MONSTER.EXOSKELETON", native[1].Slot),
        new("MONSTER.EXOSKELETON", native[2].Slot),
        new("MONSTER.EXOSKELETON", native[3].Slot)
    ];
}

#endregion

#region 紧急作战

// 空壳列队 / Emergency. Edit this version independently of the other version.
//初始带两点力量
public sealed class ExoskeletonsEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "EXOSKELETONS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.EXOSKELETONS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.EXOSKELETONS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.EXOSKELETONS.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    public override IReadOnlyList<BlackFlowSpireMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.EXOSKELETON",
            StartingPowers = [new("POWER.STRENGTH_POWER", 2m)]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.EXOSKELETON", native[0].Slot),
        new("MONSTER.EXOSKELETON", native[1].Slot),
        new("MONSTER.EXOSKELETON", native[2].Slot),
        new("MONSTER.EXOSKELETON", native[3].Slot)
    ];
}

#endregion
