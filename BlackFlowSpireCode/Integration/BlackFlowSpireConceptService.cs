using System.Runtime.CompilerServices;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class BlackFlowSpireConceptService
{
    private sealed class Settlement { public bool Running; }
    private static readonly ConditionalWeakTable<RunState, Settlement> Settlements = new();

    public static Func<RunState, int, Task>? ShieldRewardHandler { get; set; } = (state, amount) =>
    {
        BlackFlowSpireGenerationService.GrantTargetShield(state, amount, replicated: BlackFlowSpireTeam.IsOnline);
        return Task.CompletedTask;
    };

    public static async Task<BlackFlowSpireConceptPart> Grant(BlackFlowSpireConceptPartKind kind)
    {
        var state = RequireState();
        BlackFlowSpireConceptPart? part = null;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => part = engine.AddConcept(kind));
        await BlackFlowSpirePartService.SaveAndRefresh(state);
        return part!;
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.DiscardConcept(id));
        await BlackFlowSpireCollectibleService.PayDiscard(state, BlackFlowSpirePartService.FixedSaleGold);
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Concept parts can only be sold at a trader.");
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.DiscardConcept(id));
        var gold = BlackFlowSpirePartService.FixedSaleGold;
        await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
        if (BlackFlowSpireTeam.InCommand(state)) BlackFlowSpireTeam.RecordGold(state, gold);
        else RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        await BlackFlowSpirePartService.SaveAndRefresh(state);
    }

    internal static void PrepareMovement(RunState state, BlackFlowSpireMap map, BlackFlowSpireMoveResult result)
    {
        if (result.ConceptEffects is not { } effects) return;
        if (effects.AttackSpeed != 0 || effects.VictoryRelics > 0)
            BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.ConceptBattle = new()
            {
                ActIndex = map.ActIndex, NodeId = result.NodeId,
                AttackSpeed = effects.AttackSpeed, VictoryRelics = effects.VictoryRelics
            });
        if (!BlackFlowSpireTeam.IsOnline) QueueRewards(state, effects);
    }

    internal static void NaturalSold(RunState state, int appraisal)
    {
        BlackFlowSpireConceptEffects? effects = null;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => effects = BlackFlowSpireConceptRules.OnNaturalSold(engine, appraisal));
        QueueRewards(state, effects!);
    }

    internal static void QueueRewards(RunState state, BlackFlowSpireConceptEffects effects)
    {
        if (effects.Relics == 0 && effects.RareRelics == 0 && effects.ProcessedParts == 0 && effects.Shield == 0) return;
        var engine = BlackFlowSpireGenerationService.GetEngine(state);
        var reward = new BlackFlowSpireConceptReward { Shield = effects.Shield };
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var excluded = engine.Collectibles.OwnedIds.Concat(engine.PendingConceptRewards.SelectMany(item => item.CollectibleIds))
            .ToHashSet(StringComparer.Ordinal);
        var collectibleRandom = new StableRandom(map.Seed, map.ActIndex, $"concept-collectibles/{engine.ConceptRewardCount}");
        for (var index = 0; index < effects.Relics + effects.RareRelics; index++)
            if (BlackFlowSpireCollectibleRules.Draw(collectibleRandom, excluded, rare: index >= effects.Relics) is { } item)
            {
                excluded.Add(item.Id);
                reward.CollectibleIds.Add(item.Id);
            }
        var random = new StableRandom(map.Seed, map.ActIndex, $"concept-parts/{engine.ConceptRewardCount}");
        for (var index = 0; index < effects.ProcessedParts; index++)
            reward.ProcessedParts.Add(BlackFlowSpireProcessedParts.All[random.Next(BlackFlowSpireProcessedParts.All.Count)].Kind);
        BlackFlowSpireGenerationService.ModifyEngine(state, inventory =>
        {
            inventory.ConceptRewardCount++;
            inventory.PendingConceptRewards.Add(reward);
        });
    }

    internal static async Task SettleRewards(RunState state, bool replicated = false)
    {
        var settlement = Settlements.GetValue(state, static _ => new Settlement());
        if (settlement.Running) return;
        settlement.Running = true;
        try
        {
            var engine = BlackFlowSpireGenerationService.GetEngine(state);
            var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
            while (engine.PendingConceptRewards.Count > 0)
            {
                var reward = engine.PendingConceptRewards[0];
                foreach (var id in reward.CollectibleIds)
                    if (BlackFlowSpireCollectibleCatalog.Find(id) is { } item && BlackFlowSpireCollectibleRules.CanAcquire(item))
                        BlackFlowSpireGenerationService.GrantCollectible(state, map, item);
                while (reward.ProcessedParts.Count > 0)
                {
                    var kind = reward.ProcessedParts[0];
                    BlackFlowSpireGenerationService.ModifyEngine(state, inventory =>
                    {
                        reward.ProcessedParts.RemoveAt(0);
                        inventory.Add(kind);
                    });
                }
                if (reward.Shield > 0 && ShieldRewardHandler is { } grantShield)
                    await grantShield(state, reward.Shield);
                BlackFlowSpireGenerationService.ModifyEngine(state, inventory => inventory.PendingConceptRewards.Remove(reward));
            }
            await BlackFlowSpireCollectibleService.SettleGold(state, map, replicated);
        }
        finally { settlement.Running = false; }
    }

    internal static int CombatAttackSpeed(RunState state) => MatchesBattle(state, out var battle) ? battle.AttackSpeed : 0;

    internal static async Task EndCombat(RunState state)
    {
        if (!MatchesBattle(state, out var battle)) return;
        BlackFlowSpireGenerationService.ModifyEngine(state, engine => engine.ConceptBattle = null);
        var replicated = BlackFlowSpireTeam.IsOnline;
        QueueRewards(state, new BlackFlowSpireConceptEffects { Relics = battle.VictoryRelics });
        await SettleRewards(state, replicated);
    }

    private static bool MatchesBattle(RunState state, out BlackFlowSpireConceptBattle battle)
    {
        battle = BlackFlowSpireGenerationService.GetEngine(state).ConceptBattle!;
        return battle != null && state.CurrentRoom is CombatRoom && battle.ActIndex == state.CurrentActIndex
            && BlackFlowSpireGenerationService.GetProgress(state).HuntPhase == BlackFlowSpireHuntPhase.None
            && battle.NodeId == BlackFlowSpireGenerationService.GetOrGenerate(state).PositionNodeId;
    }

    private static RunState RequireState()
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanManage(BlackFlowSpireRuntime.State!)
            || (BlackFlowSpireTeam.IsOnline && MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return BlackFlowSpireRuntime.State!;
    }
}
