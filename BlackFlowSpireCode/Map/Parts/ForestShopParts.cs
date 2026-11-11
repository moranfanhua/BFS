using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

// Only parts with an original purchase price belong here. Purchase price is
// independent of a natural part's appraisal or its eventual resale value.
public sealed record ForestShopPartDefinition(string Id, int PurchaseOriginium,
    ForestProcessedPartKind? Processed = null, ForestNaturalPartKind? Natural = null, ForestConceptPartKind? Concept = null)
{
    public void Grant(ForestEngineInventory inventory)
    {
        if (Processed is { } processed) inventory.Add(processed);
        else if (Natural is { } natural) inventory.AddNatural(natural);
        else if (Concept is { } concept) inventory.AddConcept(concept);
        else throw new InvalidOperationException("Shop part has no kind.");
    }
}

public static class ForestShopParts
{
    // Source: PRTS engine-parts catalogue, purchase/cart prices. IDs also bind
    // the original local artwork; no acquisition restrictions are inferred.
    public static IReadOnlyList<ForestShopPartDefinition> All { get; } = Array.AsReadOnly<ForestShopPartDefinition>(
    [
        new("rogue_6_scrap_M_01", 2, Processed: ForestProcessedPartKind.ScrapWheel),
        new("rogue_6_scrap_M_02", 2, Processed: ForestProcessedPartKind.ScrapProsthetic),
        new("rogue_6_scrap_M_03", 4, Processed: ForestProcessedPartKind.StandardEngine),
        new("rogue_6_scrap_M_04", 6, Processed: ForestProcessedPartKind.HeavySpring),
        new("rogue_6_scrap_M_05", 4, Processed: ForestProcessedPartKind.AirCushionBase),
        new("rogue_6_scrap_M_06", 4, Processed: ForestProcessedPartKind.PrototypeExoskeleton),
        new("rogue_6_scrap_M_07", 4, Processed: ForestProcessedPartKind.XiaoBajie),
        new("rogue_6_scrap_M_08", 8, Processed: ForestProcessedPartKind.DisposableJetpack),
        new("rogue_6_scrap_M_09", 8, Processed: ForestProcessedPartKind.MotherSnowmelt),
        new("rogue_6_scrap_M_10", 12, Processed: ForestProcessedPartKind.CannotsTentacle),
        new("rogue_6_scrap_M_13", 4, Processed: ForestProcessedPartKind.EmergencyEjectionSeat),
        new("rogue_6_scrap_M_14", 12, Processed: ForestProcessedPartKind.ForceFeedbackSteeringWheel),
        new("rogue_6_scrap_M_15", 2, Processed: ForestProcessedPartKind.Horn),
        new("rogue_6_scrap_M_16", 16, Processed: ForestProcessedPartKind.CannotExpress),
        new("rogue_6_scrap_G_01", 4, Natural: ForestNaturalPartKind.Seed),
        new("rogue_6_scrap_G_02", 4, Natural: ForestNaturalPartKind.BloodMushroom),
        new("rogue_6_scrap_G_03", 4, Natural: ForestNaturalPartKind.FogTumbleweed),
        new("rogue_6_scrap_G_04", 8, Natural: ForestNaturalPartKind.EchoCorn),
        new("rogue_6_scrap_G_05", 6, Natural: ForestNaturalPartKind.Wave),
        new("rogue_6_scrap_G_06", 12, Natural: ForestNaturalPartKind.FrostCrystalTree),
        new("rogue_6_scrap_G_07", 12, Natural: ForestNaturalPartKind.GrowingMoss),
        new("rogue_6_scrap_G_13", 6, Natural: ForestNaturalPartKind.ThinBurialGrass),
        new("rogue_6_scrap_G_14", 6, Natural: ForestNaturalPartKind.LifeAndDeath),
        new("rogue_6_scrap_G_15", 4, Natural: ForestNaturalPartKind.BloodOrange),
        new("rogue_6_scrap_G_17", 8, Natural: ForestNaturalPartKind.LoveGrass),
        new("rogue_6_scrap_G_19", 6, Natural: ForestNaturalPartKind.SeedPod),
        new("rogue_6_scrap_P_01", 4, Concept: ForestConceptPartKind.WhiteBird),
        new("rogue_6_scrap_P_02", 4, Concept: ForestConceptPartKind.WhiteDog),
        new("rogue_6_scrap_P_03", 4, Concept: ForestConceptPartKind.WhiteFish),
        new("rogue_6_scrap_P_04", 8, Concept: ForestConceptPartKind.PaintedLiberi),
        new("rogue_6_scrap_P_05", 8, Concept: ForestConceptPartKind.PaintedPerro),
        new("rogue_6_scrap_P_06", 8, Concept: ForestConceptPartKind.PaintedAegir),
        new("rogue_6_scrap_P_07", 4, Concept: ForestConceptPartKind.LorentzEye),
        new("rogue_6_scrap_P_08", 4, Concept: ForestConceptPartKind.WhiteCannot),
        new("rogue_6_scrap_P_09", 8, Concept: ForestConceptPartKind.PaintedCannot)
    ]);

    private static readonly IReadOnlyDictionary<string, ForestShopPartDefinition> ById = All.ToDictionary(part => part.Id);

    public static ForestShopPartDefinition Get(string id) => ById.TryGetValue(id, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(id), id, "Part is not sold at traders.");

    // Artwork also covers parts absent from the purchase pool. Every held part
    // may be sold; adding its artwork never makes it eligible for shop stock.
    public static string IconId(ForestProcessedPartKind kind) => kind switch
    {
        ForestProcessedPartKind.StructuralPrinciple => "rogue_6_scrap_M_11",
        ForestProcessedPartKind.SimpleRemote => "rogue_6_scrap_M_12",
        _ => All.First(part => part.Processed == kind).Id
    };
    public static string IconId(ForestNaturalPartKind kind) => kind switch
    {
        ForestNaturalPartKind.DriedMossBall => "rogue_6_scrap_G_08",
        ForestNaturalPartKind.BoardVine => "rogue_6_scrap_G_09",
        ForestNaturalPartKind.HomesickFruit => "rogue_6_scrap_G_10",
        ForestNaturalPartKind.BrilliantTruffle => "rogue_6_scrap_G_11",
        ForestNaturalPartKind.CageController => "rogue_6_scrap_G_12",
        ForestNaturalPartKind.TravelFluff => "rogue_6_scrap_G_16",
        ForestNaturalPartKind.UnfatedFruit => "rogue_6_scrap_G_18",
        _ => All.First(part => part.Natural == kind).Id
    };
    public static string IconId(ForestConceptPartKind kind) => All.First(part => part.Concept == kind).Id;
}

public sealed class ForestShopPartStock
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
