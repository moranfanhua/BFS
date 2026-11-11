namespace BlackFlowSpire.Map.Stages;

public abstract class ForestBossStageDefinition : ForestStageDefinition
{
    public sealed override bool IsBoss => true;
    public abstract ForestStageVersionDefinition Boss { get; }

    public sealed override ForestStageVersionDefinition GetVersion(ForestBattleVariant variant) =>
        variant == ForestBattleVariant.Boss ? Boss
            : throw new ArgumentException("A boss has no normal/emergency version.", nameof(variant));
}
