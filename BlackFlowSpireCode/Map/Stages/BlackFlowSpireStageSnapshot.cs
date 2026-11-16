using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages;

// Freeze the complete version, including source and localization keys. Later
// edits to either version cannot replace a battle already assigned to a map.
public sealed class BlackFlowSpireStageSnapshot
{
    public string StageId { get; set; } = "";
    public string VersionId { get; set; } = "";
    public BlackFlowSpireBattleVariant Variant { get; set; }
    public int Revision { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool UsesCustomFormation { get; set; }
    public string SourceEncounterId { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public BlackFlowSpireBalanceProfile Balance { get; set; } = new();
    public List<BlackFlowSpireMonsterRule> MonsterRules { get; set; } = [];
    public bool UseGenericEnemyLayout { get; set; }

    public void Validate()
    {
        if (new[] { StageId, VersionId, SourceEncounterId, TitleKey, DescriptionKey }.Any(string.IsNullOrWhiteSpace)
            || !Enum.IsDefined(Variant) || Revision < 1 || Balance == null)
            throw new InvalidDataException("Invalid saved stage version snapshot.");
        Balance.Validate();
        if (MonsterRules == null) throw new InvalidDataException("Missing saved monster rules.");
        foreach (var rule in MonsterRules) rule.Validate();
    }

    public BlackFlowSpireNodeDetailsSource CreateDetails() => new() { TitleKey = TitleKey, DescriptionKey = DescriptionKey };

    public BlackFlowSpireStageSnapshot Copy() => new()
    {
        StageId = StageId, VersionId = VersionId, Variant = Variant, Revision = Revision,
        UsesCustomFormation = UsesCustomFormation, SourceEncounterId = SourceEncounterId, TitleKey = TitleKey, DescriptionKey = DescriptionKey,
        Balance = Balance.Copy(), MonsterRules = MonsterRules.Select(rule => rule.Copy()).ToList(),
        UseGenericEnemyLayout = UseGenericEnemyLayout
    };
}
