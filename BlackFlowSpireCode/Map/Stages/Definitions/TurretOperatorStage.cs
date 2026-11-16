using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TurretOperatorStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "TURRET_OPERATOR";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new TurretOperatorNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new TurretOperatorEmergency();
}

#endregion

#region 普通作战

// 代人受过 / Normal. Edit this version independently of the other version.
//活体盾的盾墙增加10点
public sealed class TurretOperatorNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TURRET_OPERATOR.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.TURRET_OPERATOR_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TURRET_OPERATOR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TURRET_OPERATOR.description";
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
            MonsterId = "MONSTER.LIVING_SHIELD",
            SelfPowerAmountOffsets = new() { ["POWER.RAMPART_POWER"] = 10m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.LIVING_SHIELD", native[0].Slot),
        new("MONSTER.TURRET_OPERATOR", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 代人受过 / Emergency. Edit this version independently of the other version.
//活体盾的盾墙增加25点
public sealed class TurretOperatorEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "TURRET_OPERATOR.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.TURRET_OPERATOR_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TURRET_OPERATOR.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.TURRET_OPERATOR.description";
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
            MonsterId = "MONSTER.LIVING_SHIELD",
            SelfPowerAmountOffsets = new() { ["POWER.RAMPART_POWER"] = 25m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.LIVING_SHIELD", native[0].Slot),
        new("MONSTER.TURRET_OPERATOR", native[1].Slot)
    ];
}

#endregion
