using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class PhrogParasiteStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "PHROG_PARASITE";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new PhrogParasiteNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new PhrogParasiteEmergency();
}

#endregion

#region 普通作战

// 腹中来客 / Normal. Edit this version independently of the other version.
// 生成的扭动虫减少一个，异蛙寄生虫初始血量降低
public sealed class PhrogParasiteNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "PHROG_PARASITE.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.PHROG_PARASITE_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHROG_PARASITE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHROG_PARASITE.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        Monsters = new()
        {
            ["MONSTER.PHROG_PARASITE"] = new() { HealthMultiplier = 0.8m }
        }
    };

    public override IReadOnlyList<BlackFlowSpireMonsterRule> MonsterRules =>
    [
        new()
        {
            MonsterId = "MONSTER.PHROG_PARASITE",
            DeathSpawnCount = 3,
            SelfPowerAmountOffsets = new() { ["POWER.INFESTED_POWER"] = -1m }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.PHROG_PARASITE", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 腹中来客 / Emergency. Edit this version independently of the other version.
public sealed class PhrogParasiteEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "PHROG_PARASITE.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.PHROG_PARASITE_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHROG_PARASITE.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.PHROG_PARASITE.description";
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
        new("MONSTER.PHROG_PARASITE", native[0].Slot)
    ];
}

#endregion
