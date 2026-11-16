using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class KnightsStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "KNIGHTS";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new KnightsNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new KnightsEmergency();
}

#endregion

#region 普通作战

// 宣誓余音 / Normal. Edit this version independently of the other version.
//血量*0.7
public sealed class KnightsNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "KNIGHTS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.KNIGHTS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNIGHTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNIGHTS.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 0.7m,
        AttackDamageMultiplier = 1m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.FLAIL_KNIGHT", native[0].Slot),
        new("MONSTER.SPECTRAL_KNIGHT", native[1].Slot),
        new("MONSTER.MAGI_KNIGHT", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 宣誓余音 / Emergency. Edit this version independently of the other version.
public sealed class KnightsEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "KNIGHTS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.KNIGHTS_ELITE";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNIGHTS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.KNIGHTS.description";
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
        new("MONSTER.FLAIL_KNIGHT", native[0].Slot),
        new("MONSTER.SPECTRAL_KNIGHT", native[1].Slot),
        new("MONSTER.MAGI_KNIGHT", native[2].Slot)
    ];
}

#endregion
