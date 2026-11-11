namespace BlackFlowSpire.Map.Stages;

public abstract class ForestPairedStageDefinition : ForestStageDefinition
{
    public sealed override bool IsBoss => false;
    public abstract ForestStageVersionDefinition Normal { get; }
    public abstract ForestStageVersionDefinition Emergency { get; }

    public sealed override ForestStageVersionDefinition GetVersion(ForestBattleVariant variant) => variant switch
    {
        ForestBattleVariant.Normal => Normal,
        ForestBattleVariant.Emergency => Emergency,
        _ => throw new ArgumentException("A paired stage has no Boss version.", nameof(variant))
    };
}
