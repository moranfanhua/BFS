using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class ForestNaturalService
{
    // Seed cultivation is handled by ForestSecretShop. Dedicated events and
    // exchanges can attach their own outcomes when those systems are implemented.
    public static Func<RunState, ForestNaturalPart, Task<bool>>? CageControllerEventHandler { get; set; }
    public static Func<RunState, ForestNaturalPart, Task<bool>>? ThinBurialGrassExchangeHandler { get; set; }
    public static Func<RunState, Task<bool>>? TraderRefreshHandler { get; set; }

    public static async Task<ForestNaturalPart> Grant(ForestNaturalPartKind kind)
    {
        var state = RequireState();
        ForestNaturalPart? part = null;
        ForestGenerationService.ModifyEngine(state, engine => part = engine.AddNatural(kind));
        await ForestPartService.SaveAndRefresh(state);
        return part!;
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        ForestGenerationService.ModifyEngine(state, engine => engine.DiscardNatural(id));
        await ForestPartService.SaveAndRefresh(state);
    }

    public static int SaleGold(ForestNaturalPart part) => checked(part.Appraisal * ForestPartService.GoldPerOriginium);

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Natural parts can only be sold at a trader.");
        var part = ForestGenerationService.GetEngine(state).NaturalParts.FirstOrDefault(part => part.Id == id)
            ?? throw new ArgumentException("Unknown natural part.", nameof(id));
        var gold = SaleGold(part);
        ForestGenerationService.ModifyEngine(state, engine => engine.DiscardNatural(id));
        ForestConceptService.NaturalSold(state, part.Appraisal);
        if (gold > 0)
        {
            await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        }
        await FinishSale(state, part);
        await ForestPartService.SaveAndRefresh(state);
    }

    internal static async Task FinishSale(RunState state, ForestNaturalPart part)
    {
        var map = ForestGenerationService.GetOrGenerate(state);
        if (part.Kind == ForestNaturalPartKind.UnfatedFruit
            && map.GetNode(map.PositionNodeId).Details?.VisualId != Map.Presentation.ForestNodeVisuals.SCRAP_SHOP
            && TraderRefreshHandler is { } refresh)
            await refresh(state);
        await ForestConceptService.SettleRewards(state);
    }

    internal static async Task PayMovementCosts(RunState state, IReadOnlyList<int>? ids, bool replicated)
    {
        if (ids == null || ids.Count == 0) return;
        var player = ForestTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the part cost.");
        var engine = ForestGenerationService.GetEngine(state);
        foreach (var id in ids.Order())
        {
            var part = engine.NaturalParts.FirstOrDefault(part => part.Id == id && part.Kind == ForestNaturalPartKind.LifeAndDeath);
            var price = ForestPartService.GoldPerOriginium;
            if (part == null || player.Gold < price) continue;
            await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
            if (!replicated) RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
            ForestGenerationService.ModifyEngine(state, _ => part.Adjust(3));
        }
    }

    private static RunState RequireState()
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.IsTraveling || !ForestTeam.CanManage(ForestRuntime.State!)
            || (ForestTeam.IsOnline && MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return ForestRuntime.State!;
    }
}
