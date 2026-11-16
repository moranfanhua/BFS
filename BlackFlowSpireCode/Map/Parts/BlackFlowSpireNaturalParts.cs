using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<BlackFlowSpireNaturalPartKind>))]
public enum BlackFlowSpireNaturalPartKind
{
    Seed, BloodMushroom, FogTumbleweed, EchoCorn, Wave, FrostCrystalTree, GrowingMoss, DriedMossBall,
    BoardVine, HomesickFruit, BrilliantTruffle, CageController, ThinBurialGrass, LifeAndDeath,
    BloodOrange, TravelFluff, LoveGrass, UnfatedFruit, SeedPod
}

public sealed record BlackFlowSpireNaturalPartDefinition(BlackFlowSpireNaturalPartKind Kind, int BaseAppraisal);

public static class BlackFlowSpireNaturalParts
{
    public static IReadOnlyList<BlackFlowSpireNaturalPartDefinition> All { get; } = Array.AsReadOnly<BlackFlowSpireNaturalPartDefinition>(
    [
        new(BlackFlowSpireNaturalPartKind.Seed, 2),
        new(BlackFlowSpireNaturalPartKind.BloodMushroom, 2),
        new(BlackFlowSpireNaturalPartKind.FogTumbleweed, 2),
        new(BlackFlowSpireNaturalPartKind.EchoCorn, 4),
        new(BlackFlowSpireNaturalPartKind.Wave, 3),
        new(BlackFlowSpireNaturalPartKind.FrostCrystalTree, 6),
        new(BlackFlowSpireNaturalPartKind.GrowingMoss, 1),
        new(BlackFlowSpireNaturalPartKind.DriedMossBall, 2),
        new(BlackFlowSpireNaturalPartKind.BoardVine, 8),
        new(BlackFlowSpireNaturalPartKind.HomesickFruit, 32),
        new(BlackFlowSpireNaturalPartKind.BrilliantTruffle, 32),
        new(BlackFlowSpireNaturalPartKind.CageController, 16),
        new(BlackFlowSpireNaturalPartKind.ThinBurialGrass, 3),
        new(BlackFlowSpireNaturalPartKind.LifeAndDeath, 1),
        new(BlackFlowSpireNaturalPartKind.BloodOrange, 2),
        new(BlackFlowSpireNaturalPartKind.TravelFluff, 1),
        new(BlackFlowSpireNaturalPartKind.LoveGrass, 4),
        new(BlackFlowSpireNaturalPartKind.UnfatedFruit, 4),
        new(BlackFlowSpireNaturalPartKind.SeedPod, 3)
    ]);

    private static readonly IReadOnlyDictionary<BlackFlowSpireNaturalPartKind, BlackFlowSpireNaturalPartDefinition> ByKind = All.ToDictionary(part => part.Kind);
    public static BlackFlowSpireNaturalPartDefinition Get(BlackFlowSpireNaturalPartKind kind) => ByKind.TryGetValue(kind, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class BlackFlowSpireNaturalPart
{
    public int Id { get; set; }
    public BlackFlowSpireNaturalPartKind Kind { get; set; }
    public int Appraisal { get; set; }
    public void Adjust(int amount) => Appraisal = (int)Math.Clamp((long)Appraisal + amount, 0, int.MaxValue);
}
