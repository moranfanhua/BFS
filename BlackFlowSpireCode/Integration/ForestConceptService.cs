using System.Runtime.CompilerServices;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

public static class ForestConceptService
{
    private sealed class Settlement { public bool Running; }
    private static readonly ConditionalWeakTable<RunState, Settlement> Settlements = new();

    public static Func<RunState, int, Task>? ShieldRewardHandler { get; set; } = (state, amount) =>
    {
        ForestGenerationService.GrantTargetShield(state, amount, replicated: ForestTeam.IsOnline);
        return Task.CompletedTask;
    };

    public static async Task<ForestConceptPart> Grant(ForestConceptPartKind kind)
    {
        var state = RequireState();
        ForestConceptPart? part = null;
        ForestGenerationService.ModifyEngine(state, engine => part = engine.AddConcept(kind));
        await ForestPartService.SaveAndRefresh(state);
        return part!;
    }

    public static async Task Discard(int id)
    {
        var state = RequireState();
        ForestGenerationService.ModifyEngine(state, engine => engine.DiscardConcept(id));
        await ForestPartService.SaveAndRefresh(state);
    }

    public static async Task Sell(int id)
    {
        var state = RequireState();
        if (state.CurrentRoom is not MerchantRoom) throw new InvalidOperationException("Concept parts can only be sold at a trader.");
        ForestGenerationService.ModifyEngine(state, engine => engine.DiscardConcept(id));
        var gold = ForestPartService.FixedSaleGold;
        await PlayerCmd.GainGold(gold, LocalContext.GetMe(state) ?? throw new InvalidOperationException("No local player."));
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(gold);
        await ForestPartService.SaveAndRefresh(state);
    }

    internal static void PrepareMovement(RunState state, ForestMap map, ForestMoveResult result)
    {
        if (result.ConceptEffects is not { } effects) return;
        if (effects.AttackSpeed != 0 || effects.VictoryRelics > 0)
            ForestGenerationService.ModifyEngine(state, engine => engine.ConceptBattle = new()
            {
                ActIndex = map.ActIndex, NodeId = result.NodeId,
                AttackSpeed = effects.AttackSpeed, VictoryRelics = effects.VictoryRelics
            });
        if (!ForestTeam.IsOnline) QueueRewards(state, effects);
    }

    internal static void NaturalSold(RunState state, int appraisal)
    {
        ForestConceptEffects? effects = null;
        ForestGenerationService.ModifyEngine(state, engine => effects = ForestConceptRules.OnNaturalSold(engine, appraisal));
        QueueRewards(state, effects!);
    }

    internal static void QueueRewards(RunState state, ForestConceptEffects effects, bool replicated = false)
    {
        if (effects.Relics == 0 && effects.RareRelics == 0 && effects.ProcessedParts == 0 && effects.Shield == 0) return;
        var player = (replicated ? ForestTeam.Owner(state) : LocalContext.GetMe(state))
            ?? throw new InvalidOperationException("No player owns the concept reward.");
        var engine = ForestGenerationService.GetEngine(state);
        var reward = new ForestConceptReward { Shield = effects.Shield };
        // Native bags exclude already owned and previously pulled unique relics.
        for (var index = 0; index < effects.Relics; index++)
            reward.RelicIds.Add(RelicFactory.PullNextRelicFromFront(player).Id.ToString());
        for (var index = 0; index < effects.RareRelics; index++)
            reward.RelicIds.Add(RelicFactory.PullNextRelicFromFront(player, RelicRarity.Rare).Id.ToString());
        var map = ForestGenerationService.GetOrGenerate(state);
        var random = new StableRandom(map.Seed, map.ActIndex, $"concept-parts/{engine.ConceptRewardCount}");
        for (var index = 0; index < effects.ProcessedParts; index++)
            reward.ProcessedParts.Add(ForestProcessedParts.All[random.Next(ForestProcessedParts.All.Count)].Kind);
        ForestGenerationService.ModifyEngine(state, inventory =>
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
            var engine = ForestGenerationService.GetEngine(state);
            var player = (replicated ? ForestTeam.Owner(state) : LocalContext.GetMe(state))
                ?? throw new InvalidOperationException("No player owns the concept reward.");
            while (engine.PendingConceptRewards.Count > 0)
            {
                var reward = engine.PendingConceptRewards[0];
                while (reward.RelicIds.Count > 0)
                {
                    var id = reward.RelicIds[0];
                    ForestGenerationService.ModifyEngine(state, _ => reward.RelicIds.RemoveAt(0));
                    var relic = ModelDb.GetById<RelicModel>(ModelId.Deserialize(id)).ToMutable();
                    await RelicCmd.Obtain(relic, player);
                    if (!replicated) RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(relic);
                }
                while (reward.ProcessedParts.Count > 0)
                {
                    var kind = reward.ProcessedParts[0];
                    ForestGenerationService.ModifyEngine(state, inventory =>
                    {
                        reward.ProcessedParts.RemoveAt(0);
                        inventory.Add(kind);
                    });
                }
                if (reward.Shield > 0 && ShieldRewardHandler is { } grantShield)
                    await grantShield(state, reward.Shield);
                ForestGenerationService.ModifyEngine(state, inventory => inventory.PendingConceptRewards.Remove(reward));
            }
        }
        finally { settlement.Running = false; }
    }

    internal static int CombatAttackSpeed(RunState state) => MatchesBattle(state, out var battle) ? battle.AttackSpeed : 0;

    internal static async Task EndCombat(RunState state)
    {
        if (!MatchesBattle(state, out var battle)) return;
        ForestGenerationService.ModifyEngine(state, engine => engine.ConceptBattle = null);
        var replicated = ForestTeam.IsOnline;
        QueueRewards(state, new ForestConceptEffects { Relics = battle.VictoryRelics }, replicated);
        await SettleRewards(state, replicated);
    }

    private static bool MatchesBattle(RunState state, out ForestConceptBattle battle)
    {
        battle = ForestGenerationService.GetEngine(state).ConceptBattle!;
        return battle != null && state.CurrentRoom is CombatRoom && battle.ActIndex == state.CurrentActIndex
            && ForestGenerationService.GetProgress(state).HuntPhase == ForestHuntPhase.None
            && battle.NodeId == ForestGenerationService.GetOrGenerate(state).PositionNodeId;
    }

    private static RunState RequireState()
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.IsTraveling || !ForestTeam.CanManage(ForestRuntime.State!)
            || (ForestTeam.IsOnline && MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress))
            throw new InvalidOperationException("No idle forest run is active.");
        return ForestRuntime.State!;
    }
}
