using System.Text.Json.Serialization;

namespace BlackFlowSpire.Map.Parts;

[JsonConverter(typeof(JsonStringEnumConverter<ForestNaturalPartKind>))]
public enum ForestNaturalPartKind
{
    Seed, BloodMushroom, FogTumbleweed, EchoCorn, Wave, FrostCrystalTree, GrowingMoss, DriedMossBall,
    BoardVine, HomesickFruit, BrilliantTruffle, CageController, ThinBurialGrass, LifeAndDeath,
    BloodOrange, TravelFluff, LoveGrass, UnfatedFruit, SeedPod
}

public sealed record ForestNaturalPartDefinition(ForestNaturalPartKind Kind, int BaseAppraisal);

public static class ForestNaturalParts
{
    public static IReadOnlyList<ForestNaturalPartDefinition> All { get; } = Array.AsReadOnly<ForestNaturalPartDefinition>(
    [
        new(ForestNaturalPartKind.Seed, 2),
        new(ForestNaturalPartKind.BloodMushroom, 2),
        new(ForestNaturalPartKind.FogTumbleweed, 2),
        new(ForestNaturalPartKind.EchoCorn, 4),
        new(ForestNaturalPartKind.Wave, 3),
        new(ForestNaturalPartKind.FrostCrystalTree, 6),
        new(ForestNaturalPartKind.GrowingMoss, 1),
        new(ForestNaturalPartKind.DriedMossBall, 2),
        new(ForestNaturalPartKind.BoardVine, 8),
        new(ForestNaturalPartKind.HomesickFruit, 32),
        new(ForestNaturalPartKind.BrilliantTruffle, 32),
        new(ForestNaturalPartKind.CageController, 16),
        new(ForestNaturalPartKind.ThinBurialGrass, 3),
        new(ForestNaturalPartKind.LifeAndDeath, 1),
        new(ForestNaturalPartKind.BloodOrange, 2),
        new(ForestNaturalPartKind.TravelFluff, 1),
        new(ForestNaturalPartKind.LoveGrass, 4),
        new(ForestNaturalPartKind.UnfatedFruit, 4),
        new(ForestNaturalPartKind.SeedPod, 3)
    ]);

    private static readonly IReadOnlyDictionary<ForestNaturalPartKind, ForestNaturalPartDefinition> ByKind = All.ToDictionary(part => part.Kind);
    public static ForestNaturalPartDefinition Get(ForestNaturalPartKind kind) => ByKind.TryGetValue(kind, out var part)
        ? part : throw new ArgumentOutOfRangeException(nameof(kind));
}

public sealed class ForestNaturalPart
{
    public int Id { get; set; }
    public ForestNaturalPartKind Kind { get; set; }
    public int Appraisal { get; set; }
    public void Adjust(int amount) => Appraisal = (int)Math.Clamp((long)Appraisal + amount, 0, int.MaxValue);
}

