using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class ForestCollectibleService
{
    public static ForestCollectibleInventory GetInventory(RunState state) =>
        ForestGenerationService.GetEngine(state).Collectibles;

    internal static Task OnFloorEntered(RunState state, ForestMap map)
    {
        var collectibles = GetInventory(state);
        if (map.ActIndex != 2 || collectibles.ThirdFloorEntered) return Task.CompletedTask;

        var earned = collectibles.Has(37);
        if (earned) ForestConceptService.QueueRewards(state, new ForestConceptEffects { Relics = 3 });
        ForestGenerationService.ModifyEngine(state, engine => engine.Collectibles.ThirdFloorEntered = true);
        return earned ? ForestConceptService.SettleRewards(state, replicated: ForestTeam.IsOnline) : Task.CompletedTask;
    }

    public static async Task Grant(RunState state, ForestCollectible item)
    {
        if (!ReferenceEquals(ForestRuntime.State, state) || !ForestRuntime.CanContinueRun
            || ForestRuntime.IsTraveling || CombatManager.Instance.IsInProgress || !ForestTeam.CanManage(state))
            throw new InvalidOperationException("No idle forest run is active.");
        if (!ForestCollectibleRules.CanAcquire(item))
            throw new InvalidOperationException("This collectible is available as a reference entry.");

        var map = ForestGenerationService.GetOrGenerate(state);
        ForestGenerationService.GrantCollectible(state, map, item);
        await SettleGold(state, map);
        await ForestPartService.SaveAndRefresh(state);
    }

    internal static void QueueSurvey(RunState state, ForestMap map)
    {
        var originium = ForestCollectibleRules.SurveyReward(map, GetInventory(state));
        if (originium <= 0) return;
        ForestGenerationService.ModifyEngine(state, engine =>
        {
            if (engine.Collectibles.RewardedFloors.Add(map.ActIndex))
                engine.Collectibles.PendingGold += originium * ForestPartService.GoldPerOriginium;
        });
    }

    internal static async Task<bool> SettleGold(RunState state, ForestMap map, bool replicated = false)
    {
        QueueSurvey(state, map);
        var amount = GetInventory(state).PendingGold;
        if (amount <= 0) return false;
        var player = ForestTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the collectible reward.");
        var before = player.Gold;
        ForestGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold -= amount);
        try
        {
            await PlayerCmd.GainGold(amount, player);
        }
        catch
        {
            if (player.Gold == before)
                ForestGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold += amount);
            throw;
        }
        if (!replicated && player.Gold > before)
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(player.Gold - before);
        return true;
    }

    internal static Task PayDiscard(RunState state, int saleGold)
    {
        if (!GetInventory(state).Has(212) || saleGold <= 0) return Task.CompletedTask;
        ForestGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold += saleGold / 2);
        return SettleGold(state, ForestGenerationService.GetOrGenerate(state));
    }
}
