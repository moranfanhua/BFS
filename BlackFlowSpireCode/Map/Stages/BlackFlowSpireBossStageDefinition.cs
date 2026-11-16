namespace BlackFlowSpire.Map.Stages;

public abstract class BlackFlowSpireBossStageDefinition : BlackFlowSpireStageDefinition
{
    public sealed override bool IsBoss => true;
    public abstract BlackFlowSpireStageVersionDefinition Boss { get; }

    public sealed override BlackFlowSpireStageVersionDefinition GetVersion(BlackFlowSpireBattleVariant variant) =>
        variant == BlackFlowSpireBattleVariant.Boss ? Boss
            : throw new ArgumentException("A boss has no normal/emergency version.", nameof(variant));
}
