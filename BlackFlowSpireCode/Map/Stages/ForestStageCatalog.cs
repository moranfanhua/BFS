namespace BlackFlowSpire.Map.Stages;

public static class ForestStageCatalog
{
    private static readonly Lazy<Dictionary<string, ForestStageDefinition>> Stages = new(Load);
    public static IReadOnlyList<ForestStageDefinition> Definitions => Stages.Value.Values.ToArray();
    public static ForestStageDefinition Get(string id) => Find(id) ?? throw new InvalidDataException($"Unknown forest stage: {id}");
    public static ForestStageDefinition? Find(string id) => Stages.Value.GetValueOrDefault(id);

    public static IEnumerable<ForestBattleVariant> Variants(ForestStageDefinition definition) => definition.IsBoss
        ? new[] { ForestBattleVariant.Boss } : new[] { ForestBattleVariant.Normal, ForestBattleVariant.Emergency };

    public static ForestStageDefinition? FindBossByEncounter(string encounterId)
    {
        var matches = Stages.Value.Values.Where(stage =>
            stage.IsBoss && stage.GetVersion(ForestBattleVariant.Boss).SourceEncounterId == encounterId).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static IReadOnlyList<ForestStageDefinition> ForRegion(string region) => Stages.Value.Values
        .Where(stage => !stage.IsBoss && string.Equals(stage.Region, region, StringComparison.OrdinalIgnoreCase))
        .OrderBy(stage => stage.Id, StringComparer.Ordinal).ToArray();

    // Register during initialization. Source IDs may repeat; family/version IDs may not.
    public static void Register(ForestStageDefinition definition)
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

    private static Dictionary<string, ForestStageDefinition> Load()
    {
        var result = new Dictionary<string, ForestStageDefinition>(StringComparer.Ordinal);
        var versionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in ForestBuiltInStages.Create())
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
