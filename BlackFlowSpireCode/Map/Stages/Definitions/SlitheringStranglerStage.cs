using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages.Definitions;

#region 关卡组

public sealed class SlitheringStranglerStage : ForestPairedStageDefinition
{
    public override string Id => "SLITHERING_STRANGLER";
    public override string Region => "OVERGROWTH";
    public override ForestStageVersionDefinition Normal { get; } = new SlitheringStranglerNormal();
    public override ForestStageVersionDefinition Emergency { get; } = new SlitheringStranglerEmergency();
}

#endregion

#region 普通作战

// 拥抱名额 / Normal. Edit this version independently of the other version.
public sealed class SlitheringStranglerNormal : ForestStageVersionDefinition
{
    public override string Id => "SLITHERING_STRANGLER.NORMAL";
    public override string SourceEncounterId => "ENCOUNTER.SLITHERING_STRANGLER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLITHERING_STRANGLER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLITHERING_STRANGLER.description";
    public override int Revision => 1;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 扼杀者固定一只；搭配贾克斯果一只、中型史莱姆一只、或小型史莱姆两只。两只小史莱姆允许同种。
    // Keep the native seeded random result by default. Replace this return
    // with your own list/seeded selection to customize this version.
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed) => native;
}

#endregion

#region 紧急作战

// 拥抱名额 / Emergency. Edit this version independently of the other version.
//扼杀者，贾克斯果，中型史莱姆各一只，小型史莱姆两只。
public sealed class SlitheringStranglerEmergency : ForestStageVersionDefinition
{
    public override string Id => "SLITHERING_STRANGLER.EMERGENCY";
    public override string SourceEncounterId => "ENCOUNTER.SLITHERING_STRANGLER_NORMAL";
    public override string TitleKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLITHERING_STRANGLER.title";
    public override string DescriptionKey => "BLACK_FLOW_SPIRE.MAP.STAGE.SLITHERING_STRANGLER.description";
    public override int Revision => 2;
    public override ForestBalanceProfile Balance => new()
    {
        HealthMultiplier = 1m,
        AttackDamageMultiplier = 1m
    };

    // 紧急编成同时包含三类搭档；史莱姆类型由本节点种子决定。
    public override IReadOnlyList<ForestEnemySlot> BuildFormation(
        IReadOnlyList<ForestEnemySlot> native, ulong seed)
    {
        // Independent seeded choices; the two small slimes may be the same type.
        var random = new Random(unchecked((int)(seed ^ (seed >> 32))));
        return
        [
            new("MONSTER.SNAPPING_JAXFRUIT", null),
            new(random.Next(2) == 0 ? "MONSTER.LEAF_SLIME_M" : "MONSTER.TWIG_SLIME_M", null),
            new(random.Next(2) == 0 ? "MONSTER.LEAF_SLIME_S" : "MONSTER.TWIG_SLIME_S", null),
            new(random.Next(2) == 0 ? "MONSTER.LEAF_SLIME_S" : "MONSTER.TWIG_SLIME_S", null),
            new("MONSTER.SLITHERING_STRANGLER", null)
        ];
    }
}

#endregion
