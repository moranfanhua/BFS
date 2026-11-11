using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<ForestConceptPartKind>))]
public enum ForestConceptPartKind
{
    WhiteBird, WhiteDog, WhiteFish, PaintedLiberi, PaintedPerro, PaintedAegir, LorentzEye, WhiteCannot, PaintedCannot
}

public enum ForestConceptTrigger { Combat, NonCombat, AnyNode, Wish, Exchange, Vantage, ValuableNaturalSale, Rest }

public sealed record ForestConceptPartDefinition(ForestConceptPartKind Kind, ForestConceptTrigger Trigger,
    int? Activations = null, int AttackSpeed = 0, int VictoryRelics = 0, int Relics = 0, int RareRelics = 0,
    int Shield = 0, int ProcessedParts = 0, bool RevealAll = false);

public static class ForestConceptParts
{
    public static IReadOnlyList<ForestConceptPartDefinition> All { get; } = Array.AsReadOnly<ForestConceptPartDefinition>(
    [
        new(ForestConceptPartKind.WhiteBird, ForestConceptTrigger.Combat, 1, AttackSpeed: 50, VictoryRelics: 1),
        new(ForestConceptPartKind.WhiteDog, ForestConceptTrigger.NonCombat, 1, Relics: 1),
        new(ForestConceptPartKind.WhiteFish, ForestConceptTrigger.AnyNode, 1, Shield: 3),
        new(ForestConceptPartKind.PaintedLiberi, ForestConceptTrigger.Combat, AttackSpeed: 35),
        new(ForestConceptPartKind.PaintedPerro, ForestConceptTrigger.Wish, Relics: 1),
        new(ForestConceptPartKind.PaintedAegir, ForestConceptTrigger.Exchange, Relics: 2),
        new(ForestConceptPartKind.LorentzEye, ForestConceptTrigger.Vantage, 1, RevealAll: true),
        new(ForestConceptPartKind.WhiteCannot, ForestConceptTrigger.ValuableNaturalSale, 1, RareRelics: 2),
        new(ForestConceptPartKind.PaintedCannot, ForestConceptTrigger.Rest, ProcessedParts: 1)
    ]);

    private static readonly IReadOnlyDictionary<ForestConceptPartKind, ForestConceptPartDefinition> ByKind = All.ToDictionary(part => part.Kind);
    public static ForestConceptPartDefinition Get(ForestConceptPartKind kind) => ByKind.TryGetValue(kind, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class ForestConceptPart
{
    public int Id { get; set; }
    public ForestConceptPartKind Kind { get; set; }
    // null means unlimited, never zero uses waiting to be replenished.
    public int? RemainingActivations { get; set; }
}

public sealed class ForestConceptEffects
{
    public int AttackSpeed { get; set; }
    public int VictoryRelics { get; set; }
    public int Relics { get; set; }
    public int RareRelics { get; set; }
    public int Shield { get; set; }
    public int ProcessedParts { get; set; }
    public bool RevealAll { get; set; }
}

public sealed class ForestConceptBattle
{
    public int ActIndex { get; set; }
    public int NodeId { get; set; }
    public int AttackSpeed { get; set; }
    public int VictoryRelics { get; set; }
}

// Rewards are frozen before the native room-entry checkpoint; inspecting the map
// does not roll relic bags, choose a processed part or consume an activation.
public sealed class ForestConceptReward
{
    public List<string> RelicIds { get; set; } = [];
    public List<ForestProcessedPartKind> ProcessedParts { get; set; } = [];
    public int Shield { get; set; }
}
