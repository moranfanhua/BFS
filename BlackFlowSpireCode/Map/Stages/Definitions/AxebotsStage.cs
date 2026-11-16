using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class AxebotsStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "AXEBOTS";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new AxebotsNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new AxebotsEmergency();
}

#endregion

#region 普通作战

// 备用姓名 / Normal. Edit this version independently of the other version.
public sealed class AxebotsNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "AXEBOTS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.AXEBOTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.description";
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
        new("MONSTER.AXEBOT", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 备用姓名 / Emergency. Edit this version independently of the other version.
//库存+1
public sealed class AxebotsEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "AXEBOTS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.AXEBOTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.description";
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
            MonsterId = "MONSTER.AXEBOT",
            InitialOnly = false,
            InitialStockBonus = 1
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.AXEBOT", native[0].Slot)
    ];
}

#endregion
