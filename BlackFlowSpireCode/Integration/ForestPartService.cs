using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

// Acquisition systems can call Grant; this service does not invent drop tables.
public static class ForestPartService
{
    public const int GoldPerOriginium = 10;
    public const int FixedSaleOriginium = 1;
    public static int FixedSaleGold => FixedSaleOriginium * GoldPerOriginium;

    public static long TotalAppraisal(ForestEngineInventory inventory) =>
        (long)(inventory.Parts.Count + inventory.ConceptParts.Count) * FixedSaleOriginium
        + inventory.NaturalParts.Aggregate(0L, (total, part) => total + part.Appraisal);

    public static long TotalSaleGold(ForestEngineInventory inventory) => checked(TotalAppraisal(inventory) * GoldPerOriginium);

    // The hope system is deferred. A future implementation can install this
    // handler without changing movement rules, inventory or saved part data.
    public static Func<RunState, int, Task>? HopeRewardHandler { get; set; }

    internal static void PrepareMovementRewards(RunState state, ForestMap map, ForestExplorationProgress progress, ForestMoveResult result)
    {
        ForestConceptService.PrepareMovement(state, map, result);
        var teamEffects = ForestTeam.IsOnline ? result.ConceptEffects : null;
        if (result.HopeGain == 0 && result.OriginiumGain == 0
            && result.NaturalPaymentIds is not { Count: > 0 } && teamEffects == null) return;
        if (progress.PendingPartReward != null) throw new InvalidOperationException("A previous part reward has not settled.");
        progress.PendingPartReward = new(map.ActIndex, result.NodeId, result.HopeGain, result.OriginiumGain)
        {
            NaturalPaymentIds = result.NaturalPaymentIds, ConceptEffects = teamEffects
        };
    }

    internal static async Task SettleMovementRewards(RunState state, ForestMap map, ForestExplorationProgress progress)
    {
        var replicated = ForestTeam.IsOnline;
        if (progress.PendingPartReward is not { } reward)
        {
            if (ForestGenerationService.GetEngine(state).PendingConceptRewards.Count > 0)
            {
                await ForestConceptService.SettleRewards(state, replicated);
                await SaveManager.Instance.SaveRun(null);
            }
            return;
        }
        if (reward.ActIndex != state.CurrentActIndex || reward.NodeId != map.PositionNodeId)
            throw new InvalidOperationException("The part reward does not match the movement destination.");
        // A native room-entry checkpoint is taken before entering the destination.
        // Save the pending reward there, then settle it in the new room's history.
        progress.PendingPartReward = null;
        ForestGenerationService.MarkChanged(state, map, progress);
        // The destination's native room sync has aligned the host's player RNG
        // and relic bag before the peers roll any part rewards.
        if (reward.ConceptEffects is { } effects) ForestConceptService.QueueRewards(state, effects, replicated);
        if (reward.Originium > 0)
        {
            var gold = checked(reward.Originium * GoldPerOriginium);
            var player = ForestTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the part reward.");
            await PlayerCmd.GainGold(gold, player);
            if (!replicated) RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        }
        if (reward.Hope > 0 && HopeRewardHandler is { } grantHope) await grantHope(state, reward.Hope);
        await ForestNaturalService.PayMovementCosts(state, reward.NaturalPaymentIds, replicated);
        await ForestConceptService.SettleRewards(state, replicated);
        await SaveManager.Instance.SaveRun(null);
    }

    public static async Task<ForestProcessedPart> Grant(ForestProcessedPartKind kind)
    {
        var state = RequireState();
        ForestProcessedPart? part = null;
        ForestGenerationService.ModifyEngine(state, engine => part = engine.Add(kind));
        await SaveAndRefresh(state);
        return part!;
    }

    public static async Task Load(int? id)
    {
        var state = RequireState();
        ForestGenerationService.ModifyEngine(state, engine => engine.Load(id));
        await SaveAndRefresh(state);
    }

    internal static async Task ExhaustActionPoints(RunState state)
    {
        if (!ReferenceEquals(ForestRuntime.State, state) || !ForestRuntime.CanContinueRun || ForestRuntime.IsTraveling
            || CombatManager.Instance.IsInProgress || !ForestTeam.CanTravel(state)
            || NMapScreen.Instance is not { IsOpen: true } screen || ForestMapUi.IsInspection(screen)) return;
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        if (!ForestHuntRules.TryExhaust(map, progress, ForestGenerationService.GetEngine(state))) return;
        ForestGenerationService.MarkChanged(state, map, progress);
        await SaveAndRefresh(state);
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        ForestGenerationService.ModifyEngine(state, engine => engine.Discard(id));
        await SaveAndRefresh(state);
    }

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Parts can only be sold at a trader.");
        var engine = ForestGenerationService.GetEngine(state);
        if (engine.NaturalParts.Any(part => part.Id == id))
        {
            await ForestNaturalService.Sell(id);
            return;
        }
        if (engine.ConceptParts.Any(part => part.Id == id))
        {
            await ForestConceptService.Sell(id);
            return;
        }
        // Discard also clears the loadout when selling the currently loaded part.
        // Every processed part sells for one ingot, regardless of remaining uses
        // or whether that kind can appear in the shop's purchase pool.
        ForestGenerationService.ModifyEngine(state, inventory => inventory.Discard(id));
        var gold = FixedSaleGold;
        await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        await SaveAndRefresh(state);
    }

    public static Task UseRandomPart()
    {
        var state = RequireState();
        var map = ForestGenerationService.GetOrGenerate(state);
        var progress = ForestGenerationService.GetProgress(state);
        var target = ForestProcessedPartRules.RandomTarget(map, progress, ForestGenerationService.GetEngine(state));
        return ForestRuntime.Move(target);
    }

    public static async Task SellBatch(IReadOnlyList<int> ids)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Parts can only be sold at a trader.");
        var inventory = ForestGenerationService.GetEngine(state);
        var selected = ids.Distinct().ToHashSet();
        if (selected.Count == 0) return;
        var naturals = inventory.NaturalParts.Where(part => selected.Contains(part.Id))
            .Select(part => new ForestNaturalPart { Id = part.Id, Kind = part.Kind, Appraisal = part.Appraisal }).ToArray();
        var fixedPriceIds = inventory.Parts.Select(part => part.Id).Concat(inventory.ConceptParts.Select(part => part.Id))
            .Where(selected.Contains).ToArray();
        if (naturals.Length + fixedPriceIds.Length != selected.Count) throw new ArgumentException("Unknown part in batch sale.", nameof(ids));
        // Freeze quotes and remove the whole selection before acquisition hooks
        // run. Sold concepts cannot activate mid-sale, and new rewards cannot
        // change the appraisal or accidentally join this batch.
        var prices = naturals.Select(ForestNaturalService.SaleGold).Concat(fixedPriceIds.Select(_ => FixedSaleGold)).ToArray();
        ForestGenerationService.ModifyEngine(state, engine =>
        {
            engine.Parts.RemoveAll(part => selected.Contains(part.Id));
            engine.NaturalParts.RemoveAll(part => selected.Contains(part.Id));
            engine.ConceptParts.RemoveAll(part => selected.Contains(part.Id));
            if (engine.LoadedPartId is { } loaded && selected.Contains(loaded)) engine.LoadedPartId = null;
        });
        foreach (var natural in naturals) ForestConceptService.NaturalSold(state, natural.Appraisal);
        var player = LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player.");
        foreach (var price in prices.Where(price => price > 0))
        {
            await PlayerCmd.GainGold(price, player);
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(price);
        }
        foreach (var natural in naturals) await ForestNaturalService.FinishSale(state, natural);
        await SaveAndRefresh(state);
    }

    private static RunState RequireState()
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.IsTraveling
            || !ForestTeam.CanManage(ForestRuntime.State!) || (ForestTeam.IsOnline && CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return ForestRuntime.State!;
    }

    internal static async Task SaveAndRefresh(RunState state)
    {
        var explorationActive = NMapScreen.Instance is { IsOpen: true } openMap
            ? !ForestMapUi.IsInspection(openMap) : state.CurrentRoom is MapRoom;
        if (explorationActive && !CombatManager.Instance.IsInProgress)
        {
            var map = ForestGenerationService.GetOrGenerate(state);
            var progress = ForestGenerationService.GetProgress(state);
            if (ForestHuntRules.TryBegin(map, progress, ForestGenerationService.GetEngine(state)))
                ForestGenerationService.MarkChanged(state, map, progress);
        }
        await SaveManager.Instance.SaveRun(null);
        ForestTeam.Publish(state);
        if (ReferenceEquals(ForestRuntime.State, state) && NMapScreen.Instance is { IsOpen: true } screen)
            ForestMapUi.Show(screen, state, ForestMapUi.IsInspection(screen));
    }
}
