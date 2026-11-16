using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class RubyRaidersStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "RUBY_RAIDERS";
    public override string Region => "OVERGROWTH";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new RubyRaidersNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new RubyRaidersEmergency();
}

#endregion

#region 普通作战

// 三席议价 / Normal. Edit this version independently of the other version.
public sealed class RubyRaidersNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "RUBY_RAIDERS.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.RUBY_RAIDERS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.RUBY_RAIDERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.RUBY_RAIDERS.description";
    public override int Revision => 1;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 五种劫掠者中选三种，每种最多一只；忽略站位后共10种。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 三席议价 / Emergency. Edit this version independently of the other version.
//数值*1.2
public sealed class RubyRaidersEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "RUBY_RAIDERS.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.RUBY_RAIDERS_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.RUBY_RAIDERS.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.RUBY_RAIDERS.description";
    public override int Revision => 2;
    public override BlackFlowSpireBalanceProfile Balance => new()
    {
        HealthMultiplier = 1.2m,
        AttackDamageMultiplier = 1.2m
    };

    // 五种劫掠者中选三种，每种最多一只；忽略站位后共10种。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) => native;
}

#endregion
