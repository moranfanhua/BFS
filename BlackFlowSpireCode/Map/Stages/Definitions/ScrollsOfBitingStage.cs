using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class ScrollsOfBitingStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "SCROLLS_OF_BITING";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new ScrollsOfBitingNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new ScrollsOfBitingEmergency();
}

#endregion

#region 普通作战

// 借阅须知 / Normal. Edit this version independently of the other version.
//*1.2
public sealed class ScrollsOfBitingNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "SCROLLS_OF_BITING.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SCROLLS_OF_BITING_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SCROLLS_OF_BITING.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SCROLLS_OF_BITING.description";
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
        new("MONSTER.SCROLL_OF_BITING", native[0].Slot),
        new("MONSTER.SCROLL_OF_BITING", native[1].Slot),
        new("MONSTER.SCROLL_OF_BITING", native[2].Slot)
    ];
}

#endregion

#region 紧急作战

// 借阅须知 / Emergency. Edit this version independently of the other version.
//*1.5
public sealed class ScrollsOfBitingEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "SCROLLS_OF_BITING.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SCROLLS_OF_BITING_WEAK";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SCROLLS_OF_BITING.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SCROLLS_OF_BITING.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.5m,
        AttackDamageMultiplier = 1.5m
    };

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.SCROLL_OF_BITING", native[0].Slot),
        new("MONSTER.SCROLL_OF_BITING", native[1].Slot),
        new("MONSTER.SCROLL_OF_BITING", native[2].Slot)
    ];
}

#endregion
