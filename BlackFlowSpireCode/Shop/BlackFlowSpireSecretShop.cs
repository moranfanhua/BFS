using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Shop;

internal static class BlackFlowSpireSecretShop
{
    public const int GoodsCount = 6;
    public static bool IsCurrent(RunState state)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        return node.Content == BlackFlowSpireNodeContent.Shop && node.Details?.VisualId == BlackFlowSpireNodeVisuals.SCRAP_SHOP;
    }

    public static BlackFlowSpireSecretShopStock GetStock(RunState state)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!IsCurrent(state)) throw new InvalidOperationException("The current node is not a secret trader.");
        if (!progress.SecretShops.TryGetValue(map.PositionNodeId, out var stock))
        {
            stock = new BlackFlowSpireSecretShopStock
            {
                Goods = DrawStock(map, map.PositionNodeId, 0).Select(id => new BlackFlowSpireShopPartStock { PartId = id }).ToList()
            };
            progress.SecretShops.Add(map.PositionNodeId, stock);
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        }
        return stock;
    }

    public static string[] DrawStock(BlackFlowSpireMap map, int nodeId, int refresh)
    {
        var pool = BlackFlowSpireShopParts.All.ToList();
        new StableRandom(map.Seed, map.ActIndex, $"secret-trader/{nodeId}/{refresh}").Shuffle(pool);
        return pool.Take(GoodsCount).Select(part => part.Id).ToArray();
    }

    public static int SeedCount(RunState state) => BlackFlowSpireGenerationService.GetEngine(state).NaturalParts
        .Count(part => part.Kind == BlackFlowSpireNaturalPartKind.Seed);

    // Local adaptation approved by the user: equal weight per existing kind,
    // excluding Seed. Legacy creatures have no outcome in this version.
    private static readonly BlackFlowSpireShopPartDefinition[] HarvestPool =
        Enum.GetValues<BlackFlowSpireProcessedPartKind>().Select(kind => new BlackFlowSpireShopPartDefinition(BlackFlowSpireShopParts.IconId(kind), 0, Processed: kind))
        .Concat(Enum.GetValues<BlackFlowSpireNaturalPartKind>().Where(kind => kind != BlackFlowSpireNaturalPartKind.Seed)
            .Select(kind => new BlackFlowSpireShopPartDefinition(BlackFlowSpireShopParts.IconId(kind), 0, Natural: kind)))
        .Concat(Enum.GetValues<BlackFlowSpireConceptPartKind>().Select(kind => new BlackFlowSpireShopPartDefinition(BlackFlowSpireShopParts.IconId(kind), 0, Concept: kind)))
        .ToArray();

    public static async Task Cultivate(RunState state, int count)
    {
        if (!ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanManage(state)
            || state.CurrentRoom is not MerchantRoom || !IsCurrent(state))
            throw new InvalidOperationException("No idle secret trader is active.");
        var stock = GetStock(state);
        if (stock.PendingHarvest.Count > 0) throw new InvalidOperationException("A harvest is still being displayed.");
        var seeds = BlackFlowSpireGenerationService.GetEngine(state).NaturalParts.Where(part => part.Kind == BlackFlowSpireNaturalPartKind.Seed)
            .OrderBy(part => part.Id).Take(count).Select(part => part.Id).ToArray();
        if (count < 1 || seeds.Length != count) throw new ArgumentOutOfRangeException(nameof(count));
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        BlackFlowSpireGenerationService.ModifyEngine(state, inventory =>
        {
            foreach (var id in seeds) inventory.DiscardNatural(id);
            for (var index = 0; index < count; index++)
            {
                var random = new StableRandom(map.Seed, map.ActIndex, $"cultivation/{map.PositionNodeId}/{stock.SeedsCultivated++}");
                var resultId = inventory.NextId;
                HarvestPool[random.Next(HarvestPool.Length)].Grant(inventory);
                stock.PendingHarvest.Add(resultId);
            }
        });
        BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        // Commit the seed spend and the rewards together, before playing any animation.
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    public static async Task AcknowledgeHarvest(RunState state)
    {
        if (!BlackFlowSpireTeam.CanManage(state)) return;
        var stock = GetStock(state);
        stock.PendingHarvest.Clear();
        BlackFlowSpireGenerationService.MarkChanged(state, BlackFlowSpireGenerationService.GetOrGenerate(state), BlackFlowSpireGenerationService.GetProgress(state));
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }
}
