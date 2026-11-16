using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

// Only parts with an original purchase price belong here. Purchase price is
// independent of a natural part's appraisal or its eventual resale value.
public sealed record BlackFlowSpireShopPartDefinition(string Id, int PurchaseOriginium,
    BlackFlowSpireProcessedPartKind? Processed = null, BlackFlowSpireNaturalPartKind? Natural = null, BlackFlowSpireConceptPartKind? Concept = null)
{
    public void Grant(BlackFlowSpireEngineInventory inventory)
    {
        if (Processed is { } processed) inventory.Add(processed);
        else if (Natural is { } natural) inventory.AddNatural(natural);
        else if (Concept is { } concept) inventory.AddConcept(concept);
        else throw new InvalidOperationException("Shop part has no kind.");
    }
}

public static class BlackFlowSpireShopParts
{
    // Source: PRTS engine-parts catalogue, purchase/cart prices. IDs also bind
    // the original local artwork; no acquisition restrictions are inferred.
    public static IReadOnlyList<BlackFlowSpireShopPartDefinition> All { get; } = Array.AsReadOnly<BlackFlowSpireShopPartDefinition>(
    [
        new("rogue_6_scrap_M_01", 2, Processed: BlackFlowSpireProcessedPartKind.ScrapWheel),
        new("rogue_6_scrap_M_02", 2, Processed: BlackFlowSpireProcessedPartKind.ScrapProsthetic),
        new("rogue_6_scrap_M_03", 4, Processed: BlackFlowSpireProcessedPartKind.StandardEngine),
        new("rogue_6_scrap_M_04", 6, Processed: BlackFlowSpireProcessedPartKind.HeavySpring),
        new("rogue_6_scrap_M_05", 4, Processed: BlackFlowSpireProcessedPartKind.AirCushionBase),
        new("rogue_6_scrap_M_06", 4, Processed: BlackFlowSpireProcessedPartKind.PrototypeExoskeleton),
        new("rogue_6_scrap_M_07", 4, Processed: BlackFlowSpireProcessedPartKind.XiaoBajie),
        new("rogue_6_scrap_M_08", 8, Processed: BlackFlowSpireProcessedPartKind.DisposableJetpack),
        new("rogue_6_scrap_M_09", 8, Processed: BlackFlowSpireProcessedPartKind.MotherSnowmelt),
        new("rogue_6_scrap_M_10", 12, Processed: BlackFlowSpireProcessedPartKind.CannotsTentacle),
        new("rogue_6_scrap_M_13", 4, Processed: BlackFlowSpireProcessedPartKind.EmergencyEjectionSeat),
        new("rogue_6_scrap_M_14", 12, Processed: BlackFlowSpireProcessedPartKind.ForceFeedbackSteeringWheel),
        new("rogue_6_scrap_M_15", 2, Processed: BlackFlowSpireProcessedPartKind.Horn),
        new("rogue_6_scrap_M_16", 16, Processed: BlackFlowSpireProcessedPartKind.CannotExpress),
        new("rogue_6_scrap_G_01", 4, Natural: BlackFlowSpireNaturalPartKind.Seed),
        new("rogue_6_scrap_G_02", 4, Natural: BlackFlowSpireNaturalPartKind.BloodMushroom),
        new("rogue_6_scrap_G_03", 4, Natural: BlackFlowSpireNaturalPartKind.FogTumbleweed),
        new("rogue_6_scrap_G_04", 8, Natural: BlackFlowSpireNaturalPartKind.EchoCorn),
        new("rogue_6_scrap_G_05", 6, Natural: BlackFlowSpireNaturalPartKind.Wave),
        new("rogue_6_scrap_G_06", 12, Natural: BlackFlowSpireNaturalPartKind.FrostCrystalTree),
        new("rogue_6_scrap_G_07", 12, Natural: BlackFlowSpireNaturalPartKind.GrowingMoss),
        new("rogue_6_scrap_G_13", 6, Natural: BlackFlowSpireNaturalPartKind.ThinBurialGrass),
        new("rogue_6_scrap_G_14", 6, Natural: BlackFlowSpireNaturalPartKind.LifeAndDeath),
        new("rogue_6_scrap_G_15", 4, Natural: BlackFlowSpireNaturalPartKind.BloodOrange),
        new("rogue_6_scrap_G_17", 8, Natural: BlackFlowSpireNaturalPartKind.LoveGrass),
        new("rogue_6_scrap_G_19", 6, Natural: BlackFlowSpireNaturalPartKind.SeedPod),
        new("rogue_6_scrap_P_01", 4, Concept: BlackFlowSpireConceptPartKind.WhiteBird),
        new("rogue_6_scrap_P_02", 4, Concept: BlackFlowSpireConceptPartKind.WhiteDog),
        new("rogue_6_scrap_P_03", 4, Concept: BlackFlowSpireConceptPartKind.WhiteFish),
        new("rogue_6_scrap_P_04", 8, Concept: BlackFlowSpireConceptPartKind.PaintedLiberi),
        new("rogue_6_scrap_P_05", 8, Concept: BlackFlowSpireConceptPartKind.PaintedPerro),
        new("rogue_6_scrap_P_06", 8, Concept: BlackFlowSpireConceptPartKind.PaintedAegir),
        new("rogue_6_scrap_P_07", 4, Concept: BlackFlowSpireConceptPartKind.LorentzEye),
        new("rogue_6_scrap_P_08", 4, Concept: BlackFlowSpireConceptPartKind.WhiteCannot),
        new("rogue_6_scrap_P_09", 8, Concept: BlackFlowSpireConceptPartKind.PaintedCannot)
    ]);

    private static readonly IReadOnlyDictionary<string, BlackFlowSpireShopPartDefinition> ById = All.ToDictionary(part => part.Id);

    public static BlackFlowSpireShopPartDefinition Get(string id) => ById.TryGetValue(id, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(id), id, "Part is not sold at traders.");

    // Artwork also covers parts absent from the purchase pool. Every held part
    // may be sold; adding its artwork never makes it eligible for shop stock.
    public static string IconId(BlackFlowSpireProcessedPartKind kind) => kind switch
    {
        BlackFlowSpireProcessedPartKind.StructuralPrinciple => "rogue_6_scrap_M_11",
        BlackFlowSpireProcessedPartKind.SimpleRemote => "rogue_6_scrap_M_12",
        _ => All.First(part => part.Processed == kind).Id
    };
    public static string IconId(BlackFlowSpireNaturalPartKind kind) => kind switch
    {
        BlackFlowSpireNaturalPartKind.DriedMossBall => "rogue_6_scrap_G_08",
        BlackFlowSpireNaturalPartKind.BoardVine => "rogue_6_scrap_G_09",
        BlackFlowSpireNaturalPartKind.HomesickFruit => "rogue_6_scrap_G_10",
        BlackFlowSpireNaturalPartKind.BrilliantTruffle => "rogue_6_scrap_G_11",
        BlackFlowSpireNaturalPartKind.CageController => "rogue_6_scrap_G_12",
        BlackFlowSpireNaturalPartKind.TravelFluff => "rogue_6_scrap_G_16",
        BlackFlowSpireNaturalPartKind.UnfatedFruit => "rogue_6_scrap_G_18",
        _ => All.First(part => part.Natural == kind).Id
    };
    public static string IconId(BlackFlowSpireConceptPartKind kind) => All.First(part => part.Concept == kind).Id;
}

public sealed class BlackFlowSpireShopPartStock
{
    public string PartId { get; set; } = "";
    public bool Purchased { get; set; }
    public int RestockCount { get; set; }
    public int RefreshCount { get; set; }
    public int FreeRefreshCount { get; set; }

    [JsonIgnore] public int PaidRefreshCount => RefreshCount - FreeRefreshCount;

    public void RecordRefresh(bool paid)
    {
        RefreshCount++;
        if (!paid) FreeRefreshCount++;
    }
}
