using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class AxebotsStage : ForestPairedStageDefinition
{
    public override string Id => "AXEBOTS";
    public override string Region => "GLORY";
    public override ForestStageVersionDefinition Normal { get; } = new AxebotsNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new AxebotsEmergency();
}

#endregion

#region 普通作战

// 备用姓名 / Normal. Edit this version independently of the other version.
public sealed class AxebotsNormal : ForestStageVersionDefinition
{
    public override string Id => "AXEBOTS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.AXEBOTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.description";
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
        new("MONSTER.AXEBOT", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 备用姓名 / Emergency. Edit this version independently of the other version.
//库存+1
public sealed class AxebotsEmergency : ForestStageVersionDefinition
{
    public override string Id => "AXEBOTS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.AXEBOTS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.AXEBOTS.description";
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
            MonsterId = "MONSTER.AXEBOT",
            InitialOnly = false,
            InitialStockBonus = 1
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.AXEBOT", native[0].Slot)
    ];
}

#endregion
