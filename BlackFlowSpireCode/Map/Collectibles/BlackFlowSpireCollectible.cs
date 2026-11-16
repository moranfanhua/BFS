using System.Text.Json;
using BlackFlowSpire.Map.Presentation;

namespace BlackFlowSpire.Map.Collectibles;

public sealed record BlackFlowSpireCollectible(string Id, int Number, string Kind, string Rarity, string IconId,
    string? BaseId, int? Grade)
{
    public string TextKey => "UI.COLLECTIBLES." + Id;
    public string Title => BlackFlowSpireText.Get(TextKey + ".name");
    public string Description => BlackFlowSpireText.Get(TextKey + ".desc");
    public string Flavor => BlackFlowSpireText.Get(TextKey + ".flavor");
    public string IconPath => $"res://BlackFlowSpire/Images/Collectibles/{IconId}.png";
}

public static class BlackFlowSpireCollectibleCatalog
{
    public static IReadOnlyList<BlackFlowSpireCollectible> All { get; } = Load();
    private static readonly Dictionary<string, BlackFlowSpireCollectible> ById = All.ToDictionary(item => item.Id, StringComparer.Ordinal);
    private static readonly Dictionary<int, BlackFlowSpireCollectible> ByNumber = All.Where(item => item.BaseId == null)
        .ToDictionary(item => item.Number);
    private static readonly Dictionary<int, BlackFlowSpireCollectible[]> Families = All.GroupBy(item => item.Number)
        .ToDictionary(family => family.Key, family => family.ToArray());

    public static BlackFlowSpireCollectible? Find(string id) => ById.GetValueOrDefault(id);
    public static BlackFlowSpireCollectible? Find(int number) => ByNumber.GetValueOrDefault(number);
    internal static IReadOnlyList<BlackFlowSpireCollectible> Family(int number) => Families.GetValueOrDefault(number) ?? [];

    private static IReadOnlyList<BlackFlowSpireCollectible> Load()
    {
        using var stream = typeof(BlackFlowSpireCollectibleCatalog).Assembly.GetManifestResourceStream("BlackFlowSpire.Data.collectibles.json")
            ?? throw new InvalidDataException("Missing forest collectible catalog.");
        return Array.AsReadOnly(JsonSerializer.Deserialize<BlackFlowSpireCollectible[]>(stream)
            ?? throw new InvalidDataException("Invalid forest collectible catalog."));
    }
}

public sealed class BlackFlowSpireCollectibleInventory
{
    public HashSet<string> OwnedIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<int> RewardedFloors { get; set; } = [];
    public int PendingGold { get; set; }
    public bool ThirdFloorEntered { get; set; }
    public int SpecialVictoryAttackSpeed { get; set; }

    public bool Has(int number) => FindOwned(number) != null;

    public BlackFlowSpireCollectible? FindOwned(int number) => OwnedIds.Count == 0 ? null
        : BlackFlowSpireCollectibleCatalog.Family(number).FirstOrDefault(item => OwnedIds.Contains(item.Id));
}
