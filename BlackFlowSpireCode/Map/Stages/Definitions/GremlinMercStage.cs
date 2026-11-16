using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class GremlinMercStage : BlackFlowSpirePairedStageDefinition
{
    public override string Id => "GREMLIN_MERC";
    public override string Region => "UNDERDOCKS";
    public override BlackFlowSpireStageVersionDefinition Normal { get; } = new GremlinMercNormal();
    public override BlackFlowSpireStageVersionDefinition Emergency { get; } = new GremlinMercEmergency();
}

#endregion

#region 普通作战

// 双人编制 / Normal. Edit this version independently of the other version.
public sealed class GremlinMercNormal : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "GREMLIN_MERC.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.GREMLIN_MERC_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.GREMLIN_MERC.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.GREMLIN_MERC.description";
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
        new("MONSTER.GREMLIN_MERC", native[0].Slot)
    ];
}

#endregion

#region 紧急作战

// 双人编制 / Emergency. Edit this version independently of the other version.
//地精佣兵初始获得3点力量
public sealed class GremlinMercEmergency : BlackFlowSpireStageVersionDefinition
{
    public override string Id => "GREMLIN_MERC.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.GREMLIN_MERC_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.GREMLIN_MERC.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.GREMLIN_MERC.description";
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
            MonsterId = "MONSTER.GREMLIN_MERC",
            StartingPowers = [new("POWER.STRENGTH_POWER", 3m)]
        }
    ];

    // Native slots keep scene positioning and matching native monster setup flags.
    // Add/remove/change entries here to customize this version only.
    public override IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> native, ulong seed) =>
    [
        new("MONSTER.GREMLIN_MERC", native[0].Slot)
    ];
}

#endregion
