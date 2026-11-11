using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class VineShamblerStage : ForestPairedStageDefinition
{
    public override string Id => "VINE_SHAMBLER";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Normal { get; } = new VineShamblerNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new VineShamblerEmergency();
}

#endregion

#region 普通作战

// 旧屋攀客 / Normal. Edit this version independently of the other version.
public sealed class VineShamblerNormal : ForestStageVersionDefinition
{
    public override string Id => "VINE_SHAMBLER.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.VINE_SHAMBLER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VINE_SHAMBLER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VINE_SHAMBLER.description";
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
        new("MONSTER.VINE_SHAMBLER", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 旧屋攀客 / Emergency. Edit this version independently of the other version.
// 增加一个缩小虫
public sealed class VineShamblerEmergency : ForestStageVersionDefinition
{
    public override string Id => "VINE_SHAMBLER.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.VINE_SHAMBLER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VINE_SHAMBLER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.VINE_SHAMBLER.description";
    public override int Revision => 2;
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
        ..native,
        new("MONSTER.SHRINKER_BEETLE", null)
    ];

}

#endregion
