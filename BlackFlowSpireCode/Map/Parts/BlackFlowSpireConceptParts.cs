using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<BlackFlowSpireConceptPartKind>))]
public enum BlackFlowSpireConceptPartKind
{
    WhiteBird, WhiteDog, WhiteFish, PaintedLiberi, PaintedPerro, PaintedAegir, LorentzEye, WhiteCannot, PaintedCannot
}

public enum BlackFlowSpireConceptTrigger { Combat, NonCombat, AnyNode, Wish, Exchange, Vantage, ValuableNaturalSale, Rest }

public sealed record BlackFlowSpireConceptPartDefinition(BlackFlowSpireConceptPartKind Kind, BlackFlowSpireConceptTrigger Trigger,
    int? Activations = null, int AttackSpeed = 0, int VictoryRelics = 0, int Relics = 0, int RareRelics = 0,
    int Shield = 0, int ProcessedParts = 0, bool RevealAll = false);

public static class BlackFlowSpireConceptParts
{
    public static IReadOnlyList<BlackFlowSpireConceptPartDefinition> All { get; } = Array.AsReadOnly<BlackFlowSpireConceptPartDefinition>(
    [
        new(BlackFlowSpireConceptPartKind.WhiteBird, BlackFlowSpireConceptTrigger.Combat, 1, AttackSpeed: 50, VictoryRelics: 1),
        new(BlackFlowSpireConceptPartKind.WhiteDog, BlackFlowSpireConceptTrigger.NonCombat, 1, Relics: 1),
        new(BlackFlowSpireConceptPartKind.WhiteFish, BlackFlowSpireConceptTrigger.AnyNode, 1, Shield: 3),
        new(BlackFlowSpireConceptPartKind.PaintedLiberi, BlackFlowSpireConceptTrigger.Combat, AttackSpeed: 35),
        new(BlackFlowSpireConceptPartKind.PaintedPerro, BlackFlowSpireConceptTrigger.Wish, Relics: 1),
        new(BlackFlowSpireConceptPartKind.PaintedAegir, BlackFlowSpireConceptTrigger.Exchange, Relics: 2),
        new(BlackFlowSpireConceptPartKind.LorentzEye, BlackFlowSpireConceptTrigger.Vantage, 1, RevealAll: true),
        new(BlackFlowSpireConceptPartKind.WhiteCannot, BlackFlowSpireConceptTrigger.ValuableNaturalSale, 1, RareRelics: 2),
        new(BlackFlowSpireConceptPartKind.PaintedCannot, BlackFlowSpireConceptTrigger.Rest, ProcessedParts: 1)
    ]);

    private static readonly IReadOnlyDictionary<BlackFlowSpireConceptPartKind, BlackFlowSpireConceptPartDefinition> ByKind = All.ToDictionary(part => part.Kind);
    public static BlackFlowSpireConceptPartDefinition Get(BlackFlowSpireConceptPartKind kind) => ByKind.TryGetValue(kind, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class BlackFlowSpireConceptPart
{
    public int Id { get; set; }
    public BlackFlowSpireConceptPartKind Kind { get; set; }
    // null means unlimited, never zero uses waiting to be replenished.
    public int? RemainingActivations { get; set; }
}

public sealed class BlackFlowSpireConceptEffects
{
    public int AttackSpeed { get; set; }
    public int VictoryRelics { get; set; }
    public int Relics { get; set; }
    public int RareRelics { get; set; }
    public int Shield { get; set; }
    public int ProcessedParts { get; set; }
    public bool RevealAll { get; set; }
}

public sealed class BlackFlowSpireConceptBattle
{
    public int ActIndex { get; set; }
    public int NodeId { get; set; }
    public int AttackSpeed { get; set; }
    public int VictoryRelics { get; set; }
}

// Rewards are frozen before the native room-entry checkpoint; inspecting the map
// does not roll relic bags, choose a processed part or consume an activation.
public sealed class BlackFlowSpireConceptReward
{
    public List<string> CollectibleIds { get; set; } = [];
    public List<BlackFlowSpireProcessedPartKind> ProcessedParts { get; set; } = [];
    public int Shield { get; set; }
}
