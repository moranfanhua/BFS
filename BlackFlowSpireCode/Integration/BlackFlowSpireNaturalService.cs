using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class BlackFlowSpireNaturalService
{
    // Seed cultivation and exchanges have their own event flows.
    public static Func<RunState, BlackFlowSpireNaturalPart, Task<bool>>? CageControllerEventHandler { get; set; }
    public static Func<RunState, Task<bool>>? TraderRefreshHandler { get; set; }

    public static async Task<BlackFlowSpireNaturalPart> Grant(BlackFlowSpireNaturalPartKind kind)
    {
        var state = RequireState();
        BlackFlowSpireNaturalPart? part = null;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => part = engine.AddNatural(kind));
        await BlackFlowSpirePartService.SaveAndRefresh(state);
        return part!;
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        var part = BlackFlowSpireGenerationService.GetEngine(state).NaturalParts.FirstOrDefault(part => part.Id == id)
            ?? throw new ArgumentException("Unknown natural part.", nameof(id));
        var saleGold = BlackFlowSpireCollectibleService.GetInventory(state).Has(212) ? SaleGold(part) : 0;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.DiscardNatural(id));
        await BlackFlowSpireCollectibleService.PayDiscard(state, saleGold);
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    public static int SaleGold(BlackFlowSpireNaturalPart part) => checked(part.Appraisal * BlackFlowSpirePartService.GoldPerOriginium);

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Natural parts can only be sold at a trader.");
        var part = BlackFlowSpireGenerationService.GetEngine(state).NaturalParts.FirstOrDefault(part => part.Id == id)
            ?? throw new ArgumentException("Unknown natural part.", nameof(id));
        var gold = SaleGold(part);
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.DiscardNatural(id));
        BlackFlowSpireConceptService.NaturalSold(state, part.Appraisal);
        if (gold > 0)
        {
            await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        }
        await FinishSale(state, part);
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    internal static async Task FinishSale(RunState state, BlackFlowSpireNaturalPart part)
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        if (part.Kind == BlackFlowSpireNaturalPartKind.UnfatedFruit
            && map.GetNode(map.PositionNodeId).Details?.VisualId != Map.Presentation.BlackFlowSpireNodeVisuals.SCRAP_SHOP
            && TraderRefreshHandler is { } refresh)
            await refresh(state);
        await BlackFlowSpireConceptService.SettleRewards(state);
    }

    internal static async Task PayMovementCosts(RunState state, IReadOnlyList<int>? ids, bool replicated)
    {
        if (ids == null || ids.Count == 0) return;
        var player = BlackFlowSpireTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the part cost.");
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        foreach (var id in ids.Order())
        {
            var part = engine.NaturalParts.FirstOrDefault(part => part.Id == id && part.Kind == BlackFlowSpireNaturalPartKind.LifeAndDeath);
            var price = BlackFlowSpirePartService.GoldPerOriginium;
            if (part == null || player.Gold < price) continue;
            await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
            if (!replicated) RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
            BlackFlowSpireGenerationService.ModifyEngine(state, _ => part.Adjust(3));
        }
    }

    private static RunState RequireState()
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanManage(BlackFlowSpireRuntime.State!)
            || (BlackFlowSpireTeam.IsOnline && MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return BlackFlowSpireRuntime.State!;
    }
}
