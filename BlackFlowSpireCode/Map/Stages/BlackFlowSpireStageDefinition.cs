using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages;

// A family only groups versions for map selection; it imposes no shared source,
// formation, localization or balance configuration on its versions.
public abstract class BlackFlowSpireStageDefinition
{
    public abstract string Id { get; }
    public abstract string Region { get; }
    public virtual bool IsBoss => false;
    public abstract BlackFlowSpireStageVersionDefinition GetVersion(BlackFlowSpireBattleVariant variant);

    public BlackFlowSpireStageSnapshot CreateSnapshot(BlackFlowSpireBattleVariant variant)
    {
        if (!Enum.IsDefined(variant) || IsBoss != (variant == BlackFlowSpireBattleVariant.Boss))
            throw new ArgumentException("Boss and regular stage variants must remain separate.", nameof(variant));
        var version = GetVersion(variant);
        version.Validate();
        var result = new BlackFlowSpireStageSnapshot
        {
            StageId = Id, Variant = variant, VersionId = version.Id, Revision = version.Revision,
            SourceEncounterId = version.SourceEncounterId, TitleKey = version.TitleKey,
            DescriptionKey = version.DescriptionKey, Balance = version.Balance.Copy(),
            MonsterRules = version.MonsterRules.Select(rule => rule.Copy()).ToList(),
            UseGenericEnemyLayout = version.UseGenericEnemyLayout
        };
        result.Validate();
        return result;
    }

    public BlackFlowSpireNodeDetailsSource CreateDetails(BlackFlowSpireBattleVariant variant) => CreateSnapshot(variant).CreateDetails();
}

