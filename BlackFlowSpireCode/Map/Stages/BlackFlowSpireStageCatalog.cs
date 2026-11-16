namespace BlackFlowSpire.Map.Stages;

public static class BlackFlowSpireStageCatalog
{
    private static readonly Lazy<Dictionary<string, BlackFlowSpireStageDefinition>> Stages = new(Load);
    public static IReadOnlyList<BlackFlowSpireStageDefinition> Definitions => Stages.Value.Values.ToArray();
    public static BlackFlowSpireStageDefinition Get(string id) => Find(id) ?? throw new InvalidDataException($"Unknown forest stage: {id}");
    public static BlackFlowSpireStageDefinition? Find(string id) => Stages.Value.GetValueOrDefault(id);

    public static IEnumerable<BlackFlowSpireBattleVariant> Variants(BlackFlowSpireStageDefinition definition) => definition.IsBoss
        ? new[] { BlackFlowSpireBattleVariant.Boss } : new[] { BlackFlowSpireBattleVariant.Normal, BlackFlowSpireBattleVariant.Emergency };

    public static BlackFlowSpireStageDefinition? FindBossByEncounter(string encounterId)
    {
        var matches = Stages.Value.Values.Where(stage =>
            stage.IsBoss && stage.GetVersion(BlackFlowSpireBattleVariant.Boss).SourceEncounterId == encounterId).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static IReadOnlyList<BlackFlowSpireStageDefinition> ForRegion(string region) => Stages.Value.Values
        .Where(stage => !stage.IsBoss && string.Equals(stage.Region, region, StringComparison.OrdinalIgnoreCase))
        .OrderBy(stage => stage.Id, StringComparer.Ordinal).ToArray();

    // Register during initialization. Source IDs may repeat; family/version IDs may not.
    public static void Register(BlackFlowSpireStageDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var versions = Variants(definition).Select(mode => definition.CreateSnapshot(mode)).ToArray();
        if (versions.Select(version => version.VersionId).Distinct(StringComparer.Ordinal).Count() != versions.Length
            || Stages.Value.ContainsKey(definition.Id)
            || Stages.Value.Values.SelectMany(stage => Variants(stage).Select(stage.GetVersion))
                .Any(existing => versions.Any(version => version.VersionId == existing.Id)))
            throw new ArgumentException("Stage family/version ID already registered.", nameof(definition));
        Stages.Value.Add(definition.Id, definition);
    }

    private static Dictionary<string, BlackFlowSpireStageDefinition> Load()
    {
        var result = new Dictionary<string, BlackFlowSpireStageDefinition>(StringComparer.Ordinal);
        var versionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in BlackFlowSpireBuiltInStages.Create())
        {
            if (!result.TryAdd(definition.Id, definition)) throw new InvalidDataException("Duplicate forest stage family ID.");
            foreach (var mode in Variants(definition))
            {
                definition.GetVersion(mode).Validate();
                if (!versionIds.Add(definition.GetVersion(mode).Id)) throw new InvalidDataException("Duplicate forest stage version ID.");
            }
        }
        return result;
    }
}
