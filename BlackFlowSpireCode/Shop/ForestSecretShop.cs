using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Shop;

internal static class ForestSecretShop
{
    public const int GoodsCount = 6;
    public static bool IsCurrent(RunState state)
    {
        var map = ForestGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        return node.Content == ForestNodeContent.Shop && node.Details?.VisualId == ForestNodeVisuals.SCRAP_SHOP;
    }

    public static ForestSecretShopStock GetStock(RunState state)
    {
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        if (!IsCurrent(state)) throw new InvalidOperationException("The current node is not a secret trader.");
        if (!progress.SecretShops.TryGetValue(map.PositionNodeId, out var stock))
        {
            stock = new ForestSecretShopStock
            {
                Goods = DrawStock(map, map.PositionNodeId, 0).Select(id => new ForestShopPartStock { PartId = id }).ToList()
            };
            progress.SecretShops.Add(map.PositionNodeId, stock);
            ForestGenerationService.MarkChanged(state, map, progress);
        }
        return stock;
    }

    public static string[] DrawStock(ForestMap map, int nodeId, int refresh)
    {
        var pool = ForestShopParts.All.ToList();
        new StableRandom(map.Seed, map.ActIndex, $"secret-trader/{nodeId}/{refresh}").Shuffle(pool);
        return pool.Take(GoodsCount).Select(part => part.Id).ToArray();
    }

    public static int SeedCount(RunState state) => ForestGenerationService.GetEngine(state).NaturalParts
        .Count(part => part.Kind == ForestNaturalPartKind.Seed);

    // Local adaptation approved by the user: equal weight per existing kind,
    // excluding Seed. Legacy creatures have no outcome in this version.
    private static readonly ForestShopPartDefinition[] HarvestPool =
        Enum.GetValues<ForestProcessedPartKind>().Select(kind => new ForestShopPartDefinition(ForestShopParts.IconId(kind), 0, Processed: kind))
        .Concat(Enum.GetValues<ForestNaturalPartKind>().Where(kind => kind != ForestNaturalPartKind.Seed)
            .Select(kind => new ForestShopPartDefinition(ForestShopParts.IconId(kind), 0, Natural: kind)))
        .Concat(Enum.GetValues<ForestConceptPartKind>().Select(kind => new ForestShopPartDefinition(ForestShopParts.IconId(kind), 0, Concept: kind)))
        .ToArray();

    public static async Task Cultivate(RunState state, int count)
    {
        if (!ReferenceEquals(ForestRuntime.State, state) || !ForestRuntime.CanContinueRun || ForestRuntime.IsTraveling || !ForestTeam.CanManage(state)
            || state.CurrentRoom is not MerchantRoom || !IsCurrent(state))
            throw new InvalidOperationException("No idle secret trader is active.");
        var stock = GetStock(state);
        if (stock.PendingHarvest.Count > 0) throw new InvalidOperationException("A harvest is still being displayed.");
        var seeds = ForestGenerationService.GetEngine(state).NaturalParts.Where(part => part.Kind == ForestNaturalPartKind.Seed)
            .OrderBy(part => part.Id).Take(count).Select(part => part.Id).ToArray();
        if (count < 1 || seeds.Length != count) throw new ArgumentOutOfRangeException(nameof(count));
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        ForestGenerationService.ModifyEngine(state, inventory =>
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
        ForestGenerationService.MarkChanged(state, map, progress);
        // Commit the seed spend and the rewards together, before playing any animation.
        await ForestPartService.SaveAndRefresh(state);
    }

    public static async Task AcknowledgeHarvest(RunState state)
    {
        if (!ForestTeam.CanManage(state)) return;
        var stock = GetStock(state);
        stock.PendingHarvest.Clear();
        ForestGenerationService.MarkChanged(state, ForestGenerationService.GetOrGenerate(state), ForestGenerationService.GetProgress(state));
        await ForestPartService.SaveAndRefresh(state);
    }
}
