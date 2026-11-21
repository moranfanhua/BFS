using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class BlackFlowSpireCollectibleService
{
    public static BlackFlowSpireCollectibleInventory GetInventory(RunState state) =>
        BlackFlowSpireGenerationService.GetEngine(state).Collectibles;

    internal static Task OnFloorEntered(RunState state, BlackFlowSpireMap map)
    {
        var collectibles = GetInventory(state);
        if (map.ActIndex != 2 || collectibles.ThirdFloorEntered) return Task.CompletedTask;

        var earned = collectibles.Has(37);
        if (earned) BlackFlowSpireConceptService.QueueRewards(state, new BlackFlowSpireConceptEffects { Relics = 3 });
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Collectibles.ThirdFloorEntered = true);
        return earned ? BlackFlowSpireConceptService.SettleRewards(state, replicated: BlackFlowSpireTeam.IsOnline) : Task.CompletedTask;
    }

    public static async Task Grant(RunState state, BlackFlowSpireCollectible item)
    {
        if (!ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun
            || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress || !BlackFlowSpireTeam.CanManage(state))
            throw new InvalidOperationException("No idle forest run is active.");
        if (!BlackFlowSpireCollectibleRules.CanAcquire(item))
            throw new InvalidOperationException("This collectible is available as a reference entry.");

        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        BlackFlowSpireGenerationService.GrantCollectible(state, map, item);
        await SettleGold(state, map);
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    internal static void QueueSurvey(RunState state, BlackFlowSpireMap map)
    {
        var originium = BlackFlowSpireCollectibleRules.SurveyReward(map, GetInventory(state));
        if (originium <= 0) return;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine =>
        {
            if (engine.Collectibles.RewardedFloors.Add(map.ActIndex))
                engine.Collectibles.PendingGold += originium * BlackFlowSpirePartService.GoldPerOriginium;
        });
    }

    internal static async Task<bool> SettleGold(RunState state, BlackFlowSpireMap map, bool replicated = false)
    {
        QueueSurvey(state, map);
        var amount = GetInventory(state).PendingGold;
        if (amount <= 0) return false;
        var player = BlackFlowSpireTeam.Owner(state) ?? throw new InvalidOperationException("No player owns the collectible reward.");
        var before = player.Gold;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold -= amount);
        try
        {
            await PlayerCmd.GainGold(amount, player);
            BlackFlowSpireTeam.RecordGold(state, amount);
        }
        catch
        {
            if (player.Gold == before)
                BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold += amount);
            throw;
        }
        if (!replicated && !BlackFlowSpireTeam.InCommand(state) && player.Gold > before)
            RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(player.Gold - before);
        return true;
    }

    internal static Task PayDiscard(RunState state, int saleGold)
    {
        if (!GetInventory(state).Has(212) || saleGold <= 0) return Task.CompletedTask;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.Collectibles.PendingGold += saleGold / 2);
        return SettleGold(state, BlackFlowSpireGenerationService.GetOrGenerate(state));
    }
}
