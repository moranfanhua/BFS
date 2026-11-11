using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SoulNexusStage : ForestPairedStageDefinition
{
    public override string Id => "SOUL_NEXUS";
    public override string Region => "GLORY";
    public override ForestStageVersionDefinition Normal { get; } = new SoulNexusNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new SoulNexusEmergency();
}

#endregion

#region 普通作战

// 寄存亡声 / Normal. Edit this version independently of the other version.
//*0.7
public sealed class SoulNexusNormal : ForestStageVersionDefinition
{
    public override string Id => "SOUL_NEXUS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SOUL_NEXUS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_NEXUS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_NEXUS.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 0.7m,
        AttackDamageMultiplier = 0.7m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.SOUL_NEXUS", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 寄存亡声 / Emergency. Edit this version independently of the other version.
public sealed class SoulNexusEmergency : ForestStageVersionDefinition
{
    public override string Id => "SOUL_NEXUS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SOUL_NEXUS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_NEXUS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SOUL_NEXUS.description";
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
        new("MONSTER.SOUL_NEXUS", native[0].Slot)
    ];
}

#endregion
