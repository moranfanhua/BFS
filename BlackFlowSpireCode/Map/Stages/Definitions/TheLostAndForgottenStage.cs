using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class TheLostAndForgottenStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "THE_LOST_AND_FORGOTTEN";
    public override string Region => "GLORY";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new TheLostAndForgottenNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new TheLostAndForgottenEmergency();
}

#endregion

#region 普通作战

// 失物两件 / Normal. Edit this version independently of the other version.
public sealed class TheLostAndForgottenNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "THE_LOST_AND_FORGOTTEN.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.THE_LOST_AND_FORGOTTEN_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_LOST_AND_FORGOTTEN.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_LOST_AND_FORGOTTEN.description";
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
        new("MONSTER.THE_LOST", native[0].Slot),
        new("MONSTER.THE_FORGOTTEN", native[1].Slot)
    ];
}

#endregion

#region 紧急作战

// 失物两件 / Emergency. Edit this version independently of the other version.
//造成的debuff的行动改为 降低玩家3点力量/敏捷，自身获得3点相应buff
public sealed class TheLostAndForgottenEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "THE_LOST_AND_FORGOTTEN.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.THE_LOST_AND_FORGOTTEN_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_LOST_AND_FORGOTTEN.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.THE_LOST_AND_FORGOTTEN.description";
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
            MonsterId = "MONSTER.THE_LOST",
            NativePropertyOffsets = new() { ["DebilitatingSmogStrengthStealAmount"] = 1 }
        },
        new()
        {
            MonsterId = "MONSTER.THE_FORGOTTEN",
            NativePropertyOffsets = new() { ["DebilitatingSmogDexStealAmount"] = 1 }
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.THE_LOST", native[0].Slot),
        new("MONSTER.THE_FORGOTTEN", native[1].Slot)
    ];
}

#endregion
