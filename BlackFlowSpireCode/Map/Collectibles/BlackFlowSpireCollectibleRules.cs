using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;

namespace BlackFlowSpire.Map.Collectibles;

public readonly record struct BlackFlowSpireCollectibleGrant(int MaxLife = 0, int Life = 0, int Shield = 0,
    int Originium = 0, int Hope = 0);

public static class BlackFlowSpireCollectibleRules
{
    private static readonly HashSet<int> Implemented =
    [
        11, 12, 13, 14, 23, 24, 25, 28, 29, 30, 31, 32, 33, 34, 37, 47, 48, 49, 56, 57, 58, 62, 66, 70, 71,
        73, 74, 75, 76, 77, 78, 79, 80, 118, 120, 121, 124, 192, 194, 198, 199, 200, 202, 203, 204, 205, 206, 208, 210,
        212, 213, 215, 216, 217, 244
    ];

    // Original sources include cultivated starting support, two specific events, and a squad's starting inventory.
    private static readonly HashSet<int> OutsideBattlePool = [32, 33, 34, 37, 208, 210, 217];

    private static readonly BlackFlowSpireProcessedPartKind[] OrdinaryProcessed =
    [
        BlackFlowSpireProcessedPartKind.ScrapWheel, BlackFlowSpireProcessedPartKind.ScrapProsthetic, BlackFlowSpireProcessedPartKind.StandardEngine,
        BlackFlowSpireProcessedPartKind.AirCushionBase, BlackFlowSpireProcessedPartKind.PrototypeExoskeleton,
        BlackFlowSpireProcessedPartKind.XiaoBajie, BlackFlowSpireProcessedPartKind.Horn
    ];

    private static readonly BlackFlowSpireNaturalPartKind[] OrdinaryNatural =
    [
        BlackFlowSpireNaturalPartKind.Seed, BlackFlowSpireNaturalPartKind.BloodMushroom, BlackFlowSpireNaturalPartKind.FogTumbleweed,
        BlackFlowSpireNaturalPartKind.Wave, BlackFlowSpireNaturalPartKind.DriedMossBall, BlackFlowSpireNaturalPartKind.ThinBurialGrass,
        BlackFlowSpireNaturalPartKind.BloodOrange, BlackFlowSpireNaturalPartKind.SeedPod
    ];

    private static readonly int[] SurveyRewards = [15, 20, 30, 40, 50];

    public static bool CanAcquire(BlackFlowSpireCollectible item) => item.Kind switch
    {
        "RELIC" => Implemented.Contains(item.Number),
        "UPGRADE" => (item.Number is 11 or 12 or 13) && (item.Grade is 3 or 6 or 9),
        _ => false
    };

    internal static BlackFlowSpireCollectible? Draw(StableRandom random, HashSet<string> excluded, bool rare,
        bool battleReward = false)
    {
        var candidates = BlackFlowSpireCollectibleCatalog.All.Where(item => item.Kind == "RELIC" && CanAcquire(item)
            && (!battleReward || !OutsideBattlePool.Contains(item.Number))
            && (!rare || item.Rarity == "SUPER_RARE")
            && !BlackFlowSpireCollectibleCatalog.Family(item.Number).Any(member => excluded.Contains(member.Id))).ToArray();
        return candidates.Length == 0 ? null : candidates[random.Next(candidates.Length)];
    }

    public static BlackFlowSpireCollectibleGrant OnAcquired(
        BlackFlowSpireCollectible item, BlackFlowSpireEngineInventory inventory, BlackFlowSpireMap map)
    {
        var random = new StableRandom(map.Seed, map.ActIndex, $"collectible/{item.Id}");
        switch (item.Number)
        {
            case 11 or 12: return new(Hope: 4);
            case 13: return new(Hope: item.Grade switch { 3 => 9, 6 => 10, 9 => 12, _ => 8 });
            case 14: return new(MaxLife: 2, Hope: 2);
            case 23: return new(Shield: 4);
            case 24: return new(Shield: 6);
            case 28: return new(MaxLife: 2, Life: 2);
            case 29: return new(Life: 4, Shield: 2);
            case 30: return new(MaxLife: 4);
            case 31: return new(MaxLife: 6);
            case 33: return new(Hope: 1);
            case 66: return new(Shield: 1);
            case 198:
                inventory.Capacity += 4;
                break;
            case 204:
                for (var i = 0; i < 3; i++) inventory.Add(OrdinaryProcessed[random.Next(OrdinaryProcessed.Length)]);
                break;
            case 205:
                for (var i = 0; i < 3; i++) inventory.AddNatural(OrdinaryNatural[random.Next(OrdinaryNatural.Length)]);
                break;
            case 206:
                while (inventory.TotalPartCount < inventory.Capacity) inventory.AddNatural(BlackFlowSpireNaturalPartKind.DriedMossBall);
                break;
            case 210: return new(Originium: 99);
            case 215: return new(Originium: 6);
        }
        return default;
    }

    internal static bool RevealMap(BlackFlowSpireMap map, BlackFlowSpireEngineInventory inventory, bool countInformation = true)
    {
        var discoveryInventory = countInformation ? inventory : null;
        if (map.ActIndex < 2 && inventory.Collectibles.Has(33))
            return BlackFlowSpireFog.RevealAll(map, discoveryInventory).Count > 0;
        return inventory.Collectibles.Has(215) && BlackFlowSpireFog.RevealTraders(map, discoveryInventory).Count > 0;
    }

    public static int SurveyReward(BlackFlowSpireMap map, BlackFlowSpireCollectibleInventory collectibles) =>
        collectibles.Has(217) && map.ActIndex >= 0 && map.ActIndex < SurveyRewards.Length
        && !collectibles.RewardedFloors.Contains(map.ActIndex)
        && map.Nodes.All(node => node.Revealed) ? SurveyRewards[map.ActIndex] : 0;

    internal static int FirstTurnEnergy(BlackFlowSpireCollectibleInventory collectibles) =>
        (collectibles.Has(56) ? 1 : 0) + (collectibles.Has(57) ? 2 : 0) + (collectibles.Has(58) ? 3 : 0);

    internal static int SpecialVictoryAttackSpeed(BlackFlowSpireCollectibleInventory collectibles, BlackFlowSpireSpecialEncounter encounter) => encounter switch
    {
        BlackFlowSpireSpecialEncounter.Resident when collectibles.Has(208) => 10,
        BlackFlowSpireSpecialEncounter.Hunt when collectibles.Has(213) => 20,
        _ => 0
    };

    public static int AllySpeedBonus(BlackFlowSpireCollectibleInventory collectibles) => collectibles.FindOwned(11)?.Grade switch
    {
        3 => 4,
        6 => 5,
        9 => 6,
        _ => 0
    };

    public static decimal AllyAttack(BlackFlowSpireCollectibleInventory collectibles) => collectibles.FindOwned(12)?.Grade switch
    {
        3 => 1.03m,
        6 => 1.05m,
        9 => 1.07m,
        _ => 1m
    };

    public static decimal AllyDefense(BlackFlowSpireCollectibleInventory collectibles) =>
        1m + (collectibles.Has(47) ? 0.15m : 0m) + (collectibles.Has(48) ? 0.25m : 0m)
        + (collectibles.Has(49) ? 0.5m : 0m);

    public static decimal EnemyDefense(BlackFlowSpireCollectibleInventory collectibles) =>
        1m - (collectibles.Has(73) ? 0.1m : 0m) - (collectibles.Has(74) ? 0.2m : 0m)
        - (collectibles.Has(75) ? 0.3m : 0m);

    public static decimal EnemyLife(BlackFlowSpireCollectibleInventory collectibles, int floor)
    {
        var modifier = 1m - (collectibles.Has(73) ? 0.05m : 0m) - (collectibles.Has(74) ? 0.1m : 0m)
            - (collectibles.Has(75) ? 0.15m : 0m) - (collectibles.Has(76) ? 0.15m : 0m)
            - (collectibles.Has(77) ? 0.3m : 0m);
        return floor < 2 && collectibles.Has(34) ? modifier * 0.5m : modifier;
    }

    public static decimal Healing(BlackFlowSpireCollectibleInventory collectibles) =>
        1m + (collectibles.Has(70) ? 0.2m : 0m) + (collectibles.Has(71) ? 0.3m : 0m);

    internal static decimal AllyLifeBonus(BlackFlowSpireCollectibleInventory collectibles, bool hasShield) =>
        (hasShield && collectibles.Has(66) ? 0.6m : 0m)
        + (collectibles.Has(78) ? 0.35m : 0m) + (collectibles.Has(79) ? 0.5m : 0m);

    public static (decimal Life, decimal Attack) PlayerModifiers(BlackFlowSpireCollectibleInventory collectibles,
        int partCount, long appraisal)
    {
        var partBonus = collectibles.Has(199) ? partCount * 0.08m : 0m;
        var appraisalBonus = collectibles.Has(216) ? Math.Min(appraisal / 5, 50) * 0.03m : 0m;
        return (1m + partBonus + appraisalBonus, 1m + partBonus);
    }
}
