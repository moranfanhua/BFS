using System.Text.Json.Serialization;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<ForestProcessedPartKind>))]
public enum ForestProcessedPartKind
{
    ScrapWheel, ScrapProsthetic, StandardEngine, HeavySpring, AirCushionBase, PrototypeExoskeleton,
    XiaoBajie, DisposableJetpack, MotherSnowmelt, CannotsTentacle, StructuralPrinciple, SimpleRemote,
    EmergencyEjectionSeat, ForceFeedbackSteeringWheel, Horn, CannotExpress
}

public enum ForestPartRange { Straight, AdjacentEight, DiamondTwo, Anywhere, SameRowOrColumn }
public enum ForestPartDestination { Any, NonCombat, Trader, Exit, Combat, Incident }

public sealed record ForestProcessedPartDefinition(ForestProcessedPartKind Kind,
    int Uses, ForestPartRange Range, ForestPartDestination Destination = ForestPartDestination.Any,
    int Distance = 0, int ActionPointCost = 1, int ActionPointGain = 0, int HopeGain = 0, int OriginiumGain = 0,
    bool ExpiresOnFloorChange = false, bool RandomDestination = false, bool RequiresObservedTarget = false,
    bool ConvertsIncidentToShop = false);

// PRTS engine-parts catalogue: mechanics only. Text lives in the localization tables.
public static class ForestProcessedParts
{
    public static IReadOnlyList<ForestProcessedPartDefinition> All { get; } = Array.AsReadOnly<ForestProcessedPartDefinition>(
    [
        new(ForestProcessedPartKind.ScrapWheel, 1, ForestPartRange.Straight, Distance: 2),
        new(ForestProcessedPartKind.ScrapProsthetic, 1, ForestPartRange.AdjacentEight),
        new(ForestProcessedPartKind.StandardEngine, 3, ForestPartRange.AdjacentEight),
        new(ForestProcessedPartKind.HeavySpring, 1, ForestPartRange.DiamondTwo,
            ActionPointCost: 0, ExpiresOnFloorChange: true),
        new(ForestProcessedPartKind.AirCushionBase, 2, ForestPartRange.DiamondTwo),
        new(ForestProcessedPartKind.PrototypeExoskeleton, 2, ForestPartRange.Straight, Distance: 3),
        new(ForestProcessedPartKind.XiaoBajie, 1, ForestPartRange.Anywhere, ForestPartDestination.NonCombat,
            ActionPointCost: 0, ExpiresOnFloorChange: true, RandomDestination: true),
        new(ForestProcessedPartKind.DisposableJetpack, 1, ForestPartRange.Anywhere, ActionPointCost: 0),
        new(ForestProcessedPartKind.MotherSnowmelt, 1, ForestPartRange.Anywhere,
            ForestPartDestination.NonCombat, HopeGain: 2),
        new(ForestProcessedPartKind.CannotsTentacle, 2, ForestPartRange.Anywhere,
            ForestPartDestination.Trader, OriginiumGain: 4, RequiresObservedTarget: true),
        new(ForestProcessedPartKind.StructuralPrinciple, 3, ForestPartRange.Anywhere),
        new(ForestProcessedPartKind.SimpleRemote, 1, ForestPartRange.AdjacentEight,
            ActionPointCost: 0, ActionPointGain: 3),
        new(ForestProcessedPartKind.EmergencyEjectionSeat, 1, ForestPartRange.Anywhere,
            ForestPartDestination.Exit),
        new(ForestProcessedPartKind.ForceFeedbackSteeringWheel, 2,
            ForestPartRange.SameRowOrColumn),
        new(ForestProcessedPartKind.Horn, 1, ForestPartRange.DiamondTwo, ForestPartDestination.Combat),
        new(ForestProcessedPartKind.CannotExpress, 1, ForestPartRange.Anywhere,
            ForestPartDestination.Incident, ConvertsIncidentToShop: true)
    ]);

    private static readonly IReadOnlyDictionary<ForestProcessedPartKind, ForestProcessedPartDefinition> ByKind =
        All.ToDictionary(part => part.Kind);

    public static ForestProcessedPartDefinition Get(ForestProcessedPartKind kind) => ByKind.TryGetValue(kind, out var definition)
        ? definition : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class ForestProcessedPart
{
    public int Id { get; set; }
    public ForestProcessedPartKind Kind { get; set; }
    public int RemainingUses { get; set; }
}

// Run-wide inventory: moving to a new floor preserves surviving parts and loadout.
public sealed class ForestEngineInventory
{
    public ForestCollectibleInventory Collectibles { get; set; } = new();
    public int Capacity { get; set; } = 10;
    public int NextId { get; set; }
    public int LastFloorIndex { get; set; } = -1;
    public int? LoadedPartId { get; set; }
    public List<ForestProcessedPart> Parts { get; set; } = [];
    public List<ForestNaturalPart> NaturalParts { get; set; } = [];
    public List<ForestConceptPart> ConceptParts { get; set; } = [];
    public ForestConceptBattle? ConceptBattle { get; set; }
    public List<ForestConceptReward> PendingConceptRewards { get; set; } = [];
    public int ConceptRewardCount { get; set; }
    public int NaturalMovementCount { get; set; }

    [JsonIgnore] public int TotalPartCount => Parts.Count + NaturalParts.Count + ConceptParts.Count;
    [JsonIgnore] public bool IsOverloaded => TotalPartCount > Capacity;
    [JsonIgnore] public ForestProcessedPart? LoadedPart => Parts.FirstOrDefault(part => part.Id == LoadedPartId);

    public ForestProcessedPart Add(ForestProcessedPartKind kind)
    {
        var definition = ForestProcessedParts.Get(kind);
        var part = new ForestProcessedPart { Id = NextId++, Kind = kind, RemainingUses = definition.Uses };
        ForestNaturalRules.PartObtained(NaturalParts);
        Parts.Add(part);
        return part;
    }

    public ForestNaturalPart AddNatural(ForestNaturalPartKind kind)
    {
        var definition = ForestNaturalParts.Get(kind);
        var part = new ForestNaturalPart { Id = NextId++, Kind = kind, Appraisal = definition.BaseAppraisal };
        ForestNaturalRules.PartObtained(NaturalParts);
        NaturalParts.Add(part);
        if (kind == ForestNaturalPartKind.GrowingMoss)
            for (var index = 0; index < 3; index++) AddNatural(ForestNaturalPartKind.DriedMossBall);
        return part;
    }

    public void DiscardNatural(int id)
    {
        if (NaturalParts.RemoveAll(part => part.Id == id) == 0) throw new ArgumentException("Unknown natural part.", nameof(id));
    }

    public ForestConceptPart AddConcept(ForestConceptPartKind kind)
    {
        var definition = ForestConceptParts.Get(kind);
        var part = new ForestConceptPart { Id = NextId++, Kind = kind, RemainingActivations = definition.Activations };
        ForestNaturalRules.PartObtained(NaturalParts);
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
            foreach (var part in Parts.Where(part => ForestProcessedParts.Get(part.Kind).ExpiresOnFloorChange).ToArray())
                Discard(part.Id);
            var seeds = NaturalParts.Count(part => part.Kind == ForestNaturalPartKind.SeedPod);
            for (var index = 0; index < seeds; index++) AddNatural(ForestNaturalPartKind.Seed);
        }
        LastFloorIndex = actIndex;
    }

    public void ConsumeLoadedPart()
    {
        var part = LoadedPart ?? throw new InvalidOperationException("No part is loaded.");
        if (--part.RemainingUses == 0) Discard(part.Id);
    }
}
