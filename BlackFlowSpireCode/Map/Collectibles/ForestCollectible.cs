using System.Text.Json;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Collectibles;

public sealed record ForestCollectible(string Id, int Number, string Kind, string Rarity, string IconId,
    string? BaseId, int? Grade)
{
    public string TextKey => "UI.COLLECTIBLES." + Id;
    public string Title => ForestText.Get(TextKey + ".name");
    public string Description => ForestText.Get(TextKey + ".desc");
    public string Flavor => ForestText.Get(TextKey + ".flavor");
    public string IconPath => $"res://BlackFlowSpire/Images/Collectibles/{IconId}.png";
}

public static class ForestCollectibleCatalog
{
    public static IReadOnlyList<ForestCollectible> All { get; } = Load();
    private static readonly Dictionary<string, ForestCollectible> ById = All.ToDictionary(item => item.Id, StringComparer.Ordinal);
    private static readonly Dictionary<int, ForestCollectible> ByNumber = All.Where(item => item.BaseId == null)
        .ToDictionary(item => item.Number);
    private static readonly Dictionary<int, ForestCollectible[]> Families = All.GroupBy(item => item.Number)
        .ToDictionary(family => family.Key, family => family.ToArray());

    public static ForestCollectible? Find(string id) => ById.GetValueOrDefault(id);
    public static ForestCollectible? Find(int number) => ByNumber.GetValueOrDefault(number);
    internal static IReadOnlyList<ForestCollectible> Family(int number) => Families.GetValueOrDefault(number) ?? [];

    private static IReadOnlyList<ForestCollectible> Load()
    {
        using var stream = typeof(ForestCollectibleCatalog).Assembly.GetManifestResourceStream("BlackFlowSpire.Data.collectibles.json")
            ?? throw new InvalidDataException("Missing forest collectible catalog.");
        return Array.AsReadOnly(JsonSerializer.Deserialize<ForestCollectible[]>(stream)
            ?? throw new InvalidDataException("Invalid forest collectible catalog."));
    }
}

public sealed class ForestCollectibleInventory
{
    public HashSet<string> OwnedIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<int> RewardedFloors { get; set; } = [];
    public int PendingGold { get; set; }
    public bool ThirdFloorEntered { get; set; }
    public int SpecialVictoryAttackSpeed { get; set; }

    public bool Has(int number) => FindOwned(number) != null;

    public ForestCollectible? FindOwned(int number) => OwnedIds.Count == 0 ? null
        : ForestCollectibleCatalog.Family(number).FirstOrDefault(item => OwnedIds.Contains(item.Id));
}
