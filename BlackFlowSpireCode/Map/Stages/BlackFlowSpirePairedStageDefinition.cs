namespace BlackFlowSpire.Map.Stages;

public abstract class BlackFlowSpirePairedStageDefinition : BlackFlowSpireStageDefinition
{
    public sealed override bool IsBoss => false;
    public abstract BlackFlowSpireStageVersionDefinition Normal { get; }
    public abstract BlackFlowSpireStageVersionDefinition Emergency { get; }

    public sealed override BlackFlowSpireStageVersionDefinition GetVersion(BlackFlowSpireBattleVariant variant) => variant switch
    {
        BlackFlowSpireBattleVariant.Normal => Normal,
        BlackFlowSpireBattleVariant.Emergency => Emergency,
        _ => throw new ArgumentException("A paired stage has no Boss version.", nameof(variant))
    };
}
