using System.Text.Json.Serialization;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<BlackFlowSpireProcessedPartKind>))]
public enum BlackFlowSpireProcessedPartKind
{
    ScrapWheel, ScrapProsthetic, StandardEngine, HeavySpring, AirCushionBase, PrototypeExoskeleton,
    XiaoBajie, DisposableJetpack, MotherSnowmelt, CannotsTentacle, StructuralPrinciple, SimpleRemote,
    EmergencyEjectionSeat, ForceFeedbackSteeringWheel, Horn, CannotExpress
}

public enum BlackFlowSpirePartRange { Straight, AdjacentEight, DiamondTwo, Anywhere, SameRowOrColumn }
public enum BlackFlowSpirePartDestination { Any, NonCombat, Trader, Exit, Combat, Incident }

public sealed record BlackFlowSpireProcessedPartDefinition(BlackFlowSpireProcessedPartKind Kind,
    int Uses, BlackFlowSpirePartRange Range, BlackFlowSpirePartDestination Destination = BlackFlowSpirePartDestination.Any,
    int Distance = 0, int ActionPointCost = 1, int ActionPointGain = 0, int HopeGain = 0, int OriginiumGain = 0,
    bool ExpiresOnFloorChange = false, bool RandomDestination = false, bool RequiresObservedTarget = false,
    bool ConvertsIncidentToShop = false);

// PRTS engine-parts catalogue: mechanics only. Text lives in the localization tables.
public static class BlackFlowSpireProcessedParts
{
    public static IReadOnlyList<BlackFlowSpireProcessedPartDefinition> All { get; } = Array.AsReadOnly<BlackFlowSpireProcessedPartDefinition>(
    [
        new(BlackFlowSpireProcessedPartKind.ScrapWheel, 1, BlackFlowSpirePartRange.Straight, Distance: 2),
        new(BlackFlowSpireProcessedPartKind.ScrapProsthetic, 1, BlackFlowSpirePartRange.AdjacentEight),
        new(BlackFlowSpireProcessedPartKind.StandardEngine, 3, BlackFlowSpirePartRange.AdjacentEight),
        new(BlackFlowSpireProcessedPartKind.HeavySpring, 1, BlackFlowSpirePartRange.DiamondTwo,
            ActionPointCost: 0, ExpiresOnFloorChange: true),
        new(BlackFlowSpireProcessedPartKind.AirCushionBase, 2, BlackFlowSpirePartRange.DiamondTwo),
        new(BlackFlowSpireProcessedPartKind.PrototypeExoskeleton, 2, BlackFlowSpirePartRange.Straight, Distance: 3),
        new(BlackFlowSpireProcessedPartKind.XiaoBajie, 1, BlackFlowSpirePartRange.Anywhere, BlackFlowSpirePartDestination.NonCombat,
            ActionPointCost: 0, ExpiresOnFloorChange: true, RandomDestination: true),
        new(BlackFlowSpireProcessedPartKind.DisposableJetpack, 1, BlackFlowSpirePartRange.Anywhere, ActionPointCost: 0),
        new(BlackFlowSpireProcessedPartKind.MotherSnowmelt, 1, BlackFlowSpirePartRange.Anywhere,
            BlackFlowSpirePartDestination.NonCombat, HopeGain: 2),
        new(BlackFlowSpireProcessedPartKind.CannotsTentacle, 2, BlackFlowSpirePartRange.Anywhere,
            BlackFlowSpirePartDestination.Trader, OriginiumGain: 4, RequiresObservedTarget: true),
        new(BlackFlowSpireProcessedPartKind.StructuralPrinciple, 3, BlackFlowSpirePartRange.Anywhere),
        new(BlackFlowSpireProcessedPartKind.SimpleRemote, 1, BlackFlowSpirePartRange.AdjacentEight,
            ActionPointCost: 0, ActionPointGain: 3),
        new(BlackFlowSpireProcessedPartKind.EmergencyEjectionSeat, 1, BlackFlowSpirePartRange.Anywhere,
            BlackFlowSpirePartDestination.Exit),
        new(BlackFlowSpireProcessedPartKind.ForceFeedbackSteeringWheel, 2,
            BlackFlowSpirePartRange.SameRowOrColumn),
        new(BlackFlowSpireProcessedPartKind.Horn, 1, BlackFlowSpirePartRange.DiamondTwo, BlackFlowSpirePartDestination.Combat),
        new(BlackFlowSpireProcessedPartKind.CannotExpress, 1, BlackFlowSpirePartRange.Anywhere,
            BlackFlowSpirePartDestination.Incident, ConvertsIncidentToShop: true)
    ]);

    private static readonly IReadOnlyDictionary<BlackFlowSpireProcessedPartKind, BlackFlowSpireProcessedPartDefinition> ByKind =
        All.ToDictionary(part => part.Kind);

    public static BlackFlowSpireProcessedPartDefinition Get(BlackFlowSpireProcessedPartKind kind) => ByKind.TryGetValue(kind, out var definition)
        ? definition : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class BlackFlowSpireProcessedPart
{
    public int Id { get; set; }
    public BlackFlowSpireProcessedPartKind Kind { get; set; }
    public int RemainingUses { get; set; }
}

// Run-wide inventory: moving to a new floor preserves surviving parts and loadout.
public sealed class BlackFlowSpireEngineInventory
{
    public BlackFlowSpireCollectibleInventory Collectibles { get; set; } = new();
    public int Capacity { get; set; } = 10;
    public int NextId { get; set; }
    public int LastFloorIndex { get; set; } = -1;
    public int? LoadedPartId { get; set; }
    public List<BlackFlowSpireProcessedPart> Parts { get; set; } = [];
    public List<BlackFlowSpireNaturalPart> NaturalParts { get; set; } = [];
    public List<BlackFlowSpireConceptPart> ConceptParts { get; set; } = [];
    public BlackFlowSpireConceptBattle? ConceptBattle { get; set; }
    public List<BlackFlowSpireConceptReward> PendingConceptRewards { get; set; } = [];
    public int ConceptRewardCount { get; set; }
    public int NaturalMovementCount { get; set; }

    [JsonIgnore] public int TotalPartCount => Parts.Count + NaturalParts.Count + ConceptParts.Count;
    [JsonIgnore] public bool IsOverloaded => TotalPartCount > Capacity;
    [JsonIgnore] public BlackFlowSpireProcessedPart? LoadedPart => Parts.FirstOrDefault(part => part.Id == LoadedPartId);

    public BlackFlowSpireProcessedPart Add(BlackFlowSpireProcessedPartKind kind)
    {
        var definition = BlackFlowSpireProcessedParts.Get(kind);
        var part = new BlackFlowSpireProcessedPart { Id = NextId++, Kind = kind, RemainingUses = definition.Uses };
        BlackFlowSpireNaturalRules.PartObtained(NaturalParts);
        Parts.Add(part);
        return part;
    }

    public BlackFlowSpireNaturalPart AddNatural(BlackFlowSpireNaturalPartKind kind)
    {
        var definition = BlackFlowSpireNaturalParts.Get(kind);
        var part = new BlackFlowSpireNaturalPart { Id = NextId++, Kind = kind, Appraisal = definition.BaseAppraisal };
        BlackFlowSpireNaturalRules.PartObtained(NaturalParts);
        NaturalParts.Add(part);
        if (kind == BlackFlowSpireNaturalPartKind.GrowingMoss)
            for (var index = 0; index < 3; index++) AddNatural(BlackFlowSpireNaturalPartKind.DriedMossBall);
        return part;
    }

    public void DiscardNatural(int id)
    {
        if (NaturalParts.RemoveAll(part => part.Id == id) == 0) throw new ArgumentException("Unknown natural part.", nameof(id));
    }

    public BlackFlowSpireConceptPart AddConcept(BlackFlowSpireConceptPartKind kind)
    {
        var definition = BlackFlowSpireConceptParts.Get(kind);
        var part = new BlackFlowSpireConceptPart { Id = NextId++, Kind = kind, RemainingActivations = definition.Activations };
        BlackFlowSpireNaturalRules.PartObtained(NaturalParts);
        ConceptParts.Add(part);
        return part;
    }

    public void DiscardConcept(int id)
    {
        if (ConceptParts.RemoveAll(part => part.Id == id) == 0) throw new ArgumentException("Unknown concept part.", nameof(id));
    }

    public void Load(int? id)
    {
        if (id.HasValue && !Parts.Any(part => part.Id == id && part.RemainingUses > 0))
            throw new ArgumentException("The part is not in the inventory or has no remaining uses.", nameof(id));
        LoadedPartId = id;
    }

    public void Discard(int id)
    {
        if (Parts.RemoveAll(part => part.Id == id) == 0) throw new ArgumentException("Unknown part.", nameof(id));
        if (LoadedPartId == id) LoadedPartId = null;
    }

    public void EnterFloor(int actIndex)
    {
        if (LastFloorIndex == actIndex) return;
        if (LastFloorIndex >= 0)
        {
            foreach (var part in Parts.Where(part => BlackFlowSpireProcessedParts.Get(part.Kind).ExpiresOnFloorChange).ToArray())
                Discard(part.Id);
            var seeds = NaturalParts.Count(part => part.Kind == BlackFlowSpireNaturalPartKind.SeedPod);
            for (var index = 0; index < seeds; index++) AddNatural(BlackFlowSpireNaturalPartKind.Seed);
        }
        LastFloorIndex = actIndex;
    }

    public void ConsumeLoadedPart()
    {
        var part = LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        if (--part.RemainingUses == 0) Discard(part.Id);
    }
}
