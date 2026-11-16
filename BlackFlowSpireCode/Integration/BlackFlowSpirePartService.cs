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
public static class BlackFlowSpirePartService
{
    public const int GoldPerOriginium = 10;
    public const int FixedSaleOriginium = 1;
    public static int FixedSaleGold => FixedSaleOriginium * GoldPerOriginium;

    public static long TotalAppraisal(BlackFlowSpireEngineInventory inventory) =>
        (long)(inventory.Parts.Count + inventory.ConceptParts.Count) * FixedSaleOriginium
        + inventory.NaturalParts.Aggregate(0L, (total, part) => total + part.Appraisal);

    public static long TotalSaleGold(BlackFlowSpireEngineInventory inventory) => checked(TotalAppraisal(inventory) * GoldPerOriginium);

    internal static void PrepareMovementRewards(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress, BlackFlowSpireMoveResult result)
    {
        BlackFlowSpireCollectibleService.QueueSurvey(state, map);
        BlackFlowSpireConceptService.PrepareMovement(state, map, result);
        var teamEffects = BlackFlowSpireTeam.IsOnline ? result.ConceptEffects : null;
        if (result.HopeGain == 0 && result.OriginiumGain == 0
            && result.NaturalPaymentIds is not { Count: > 0 } && teamEffects == null) return;
        if (progress.PendingPartReward != null) throw new InvalidOperationException("A previous part reward has not settled.");
        progress.PendingPartReward = new(map.ActIndex, result.NodeId, result.HopeGain, result.OriginiumGain)
        {
            NaturalPaymentIds = result.NaturalPaymentIds, ConceptEffects = teamEffects
        };
    }

    internal static async Task SettleMovementRewards(RunState state, BlackFlowSpireMap map, BlackFlowSpireExplorationProgress progress)
    {
        var replicated = BlackFlowSpireTeam.IsOnline;
        // The native room-entry checkpoint contains the pending reward. Apply
        // its Hope and clear the record together in the new room's history.
        if (BlackFlowSpireGenerationService.TakeMovementReward(state, map, progress) is not { } reward)
        {
            if (BlackFlowSpireGenerationService.GetEngine(state).PendingConceptRewards.Count > 0)
            {
                await BlackFlowSpireConceptService.SettleRewards(state, replicated);
                await SaveManager.Instance.SaveRun(null);
            }
            else if (await BlackFlowSpireCollectibleService.SettleGold(state, map, replicated))
                await SaveManager.Instance.SaveRun(null);
            return;
        }
        // The destination's native room sync has aligned the host's player RNG
        // and relic bag before the peers roll any part rewards.
        if (reward.ConceptEffects is { } effects) BlackFlowSpireConceptService.QueueRewards(state, effects);
        if (reward.Originium > 0)
        {
            var gold = checked(reward.Originium * GoldPerOriginium);
            var player = BlackFlowSpireTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the part reward.");
            await PlayerCmd.GainGold(gold, player);
            if (!replicated) RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        }
        await BlackFlowSpireNaturalService.PayMovementCosts(state, reward.NaturalPaymentIds, replicated);
        await BlackFlowSpireConceptService.SettleRewards(state, replicated);
        await SaveManager.Instance.SaveRun(null);
    }

    public static async Task<BlackFlowSpireProcessedPart> Grant(BlackFlowSpireProcessedPartKind kind)
    {
        var state = RequireState();
        BlackFlowSpireProcessedPart? part = null;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => part = engine.Add(kind));
        await SaveAndRefresh(state);
        return part!;
    }

    public static async Task Load(int? id)
    {
        var state = RequireState();
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Load(id));
        await SaveAndRefresh(state);
    }

    internal static async Task ExhaustActionPoints(RunState state)
    {
        if (!ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.IsTraveling
            || CombatManager.Instance.IsInProgress || !BlackFlowSpireTeam.CanTravel(state)
            || NMapScreen.Instance is not { IsOpen: true } screen || BlackFlowSpireMapUi.IsInspection(screen)) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpireHuntRules.TryExhaust(map, progress, BlackFlowSpireGenerationService.GetEngine(state))) return;
        BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        await SaveAndRefresh(state);
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Discard(id));
        await BlackFlowSpireCollectibleService.PayDiscard(state, FixedSaleGold);
        await SaveAndRefresh(state);
    }

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Parts can only be sold at a trader.");
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        if (engine.NaturalParts.Any(part => part.Id == id))
        {
            await BlackFlowSpireNaturalService.Sell(id);
            return;
        }
        if (engine.ConceptParts.Any(part => part.Id == id))
        {
            await BlackFlowSpireConceptService.Sell(id);
            return;
        }
        // Discard also clears the loadout when selling the currently loaded part.
        // Every processed part sells for one ingot, regardless of remaining uses
        // or whether that kind can appear in the shop's purchase pool.
        BlackFlowSpireGenerationService.ModifyEngine(state, inventory => inventory.Discard(id));
        var gold = FixedSaleGold;
        await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        await SaveAndRefresh(state);
    }

    public static Task UseRandomPart()
    {
        var state = RequireState();
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var target = BlackFlowSpireProcessedPartRules.RandomTarget(map, progress, BlackFlowSpireGenerationService.GetEngine(state));
        return BlackFlowSpireRuntime.Move(target);
    }

    public static async Task SellBatch(IReadOnlyList<int> ids)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Parts can only be sold at a trader.");
        var inventory = BlackFlowSpireGenerationService.GetEngine(state);
        var selected = ids.Distinct().ToHashSet();
        if (selected.Count == 0) return;
        var naturals = inventory.NaturalParts.Where(part => selected.Contains(part.Id))
            .Select(part => new BlackFlowSpireNaturalPart { Id = part.Id, Kind = part.Kind, Appraisal = part.Appraisal }).ToArray();
        var fixedPriceIds = inventory.Parts.Select(part => part.Id).Concat(inventory.ConceptParts.Select(part => part.Id))
            .Where(selected.Contains).ToArray();
        if (naturals.Length + fixedPriceIds.Length != selected.Count) throw new ArgumentException("Unknown part in batch sale.", nameof(ids));
        // Freeze quotes and remove the whole selection before acquisition hooks
        // run. Sold concepts cannot activate mid-sale, and new rewards cannot
        // change the appraisal or accidentally join this batch.
        var prices = naturals.Select(BlackFlowSpireNaturalService.SaleGold).Concat(fixedPriceIds.Select(_ => FixedSaleGold)).ToArray();
        BlackFlowSpireGenerationService.ModifyEngine(state, engine =>
        {
            engine.Parts.RemoveAll(part => selected.Contains(part.Id));
            engine.NaturalParts.RemoveAll(part => selected.Contains(part.Id));
            engine.ConceptParts.RemoveAll(part => selected.Contains(part.Id));
            if (engine.LoadedPartId is { } loaded && selected.Contains(loaded)) engine.LoadedPartId = null;
        });
        foreach (var natural in naturals) BlackFlowSpireConceptService.NaturalSold(state, natural.Appraisal);
        var player = LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player.");
        foreach (var price in prices.Where(price => price > 0))
        {
            await PlayerCmd.GainGold(price, player);
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(price);
        }
        foreach (var natural in naturals) await BlackFlowSpireNaturalService.FinishSale(state, natural);
        await SaveAndRefresh(state);
    }

    private static RunState RequireState()
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.IsTraveling
            || !BlackFlowSpireTeam.CanManage(BlackFlowSpireRuntime.State!) || (BlackFlowSpireTeam.IsOnline && CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return BlackFlowSpireRuntime.State!;
    }

    internal static async Task SaveAndRefresh(RunState state)
    {
        var explorationActive = NMapScreen.Instance is { IsOpen: true } openMap
            ? !BlackFlowSpireMapUi.IsInspection(openMap) : state.CurrentRoom is MapRoom;
        if (explorationActive && !CombatManager.Instance.IsInProgress)
        {
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            var progress = BlackFlowSpireGenerationService.GetProgress(state);
            if (BlackFlowSpireHuntRules.TryBegin(map, progress, BlackFlowSpireGenerationService.GetEngine(state)))
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
        }
        await SaveManager.Instance.SaveRun(null);
        BlackFlowSpireTeam.Publish(state);
        if (ReferenceEquals(BlackFlowSpireRuntime.State, state) && NMapScreen.Instance is { IsOpen: true } screen)
            BlackFlowSpireMapUi.Show(screen, state, BlackFlowSpireMapUi.IsInspection(screen));
    }
}
