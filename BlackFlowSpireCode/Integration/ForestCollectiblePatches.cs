using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.Healing;

namespace BlackFlowSpire.Integration;

internal sealed class ForestCollectibleHealing : IHealHookListener
{
    public decimal ModifyHealMultiplicative(HealContext context, decimal amount) =>
        context.CombatState != null && context.Creature.Side == CombatSide.Player
        && context.RunState is RunState run && ForestGenerationService.IsEnabled(run)
            ? ForestCollectibleRules.Healing(ForestCollectibleService.GetInventory(run)) : 1m;
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class ForestCollectibleKnockdownStunPatch
{
    private static void Prefix(ICombatState? combatState, Creature creature, bool wasRemovalPrevented, out bool __state) =>
        __state = creature.IsPlayer && combatState is CombatState combat
            && ForestCombatHealthRuntime.TakeKnockdown(combat, creature) && !wasRemovalPrevented;

    private static void Postfix(IRunState runState, ICombatState? combatState, bool __state, ref Task __result)
    {
        if (__state && runState is RunState run && combatState != null) __result = StunEnemy(__result, run, combatState);
    }

    private static async Task StunEnemy(Task original, RunState run, ICombatState combat)
    {
        await original;
        var target = run.Rng.CombatTargets.NextItem(combat.Enemies.Where(enemy => enemy.IsAlive
            && enemy.Monster is { MoveStateMachine.StateLog.Count: > 0, NextMove.CanTransitionAway: true }));
        if (target != null) await CreatureCmd.Stun(target);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class ForestCollectibleTurnStartPatch
{
    private static void Postfix(ICombatState combatState, CombatSide side, ref Task __result)
    {
        if (side != CombatSide.Player || combatState.RunState is not RunState run
            || !ForestGenerationService.IsEnabled(run)) return;
        var collectibles = ForestCollectibleService.GetInventory(run);
        var healAllies = collectibles.Has(80);
        var grantUnhurtBlock = collectibles.Has(120);
        var grantLowLifeBlock = collectibles.Has(124);
        if (healAllies || grantUnhurtBlock || grantLowLifeBlock)
            __result = Apply(__result, combatState, healAllies, grantUnhurtBlock, grantLowLifeBlock);
    }

    private static async Task Apply(Task original, ICombatState combat, bool healAllies, bool grantUnhurtBlock,
        bool grantLowLifeBlock)
    {
        await original;
        if (grantLowLifeBlock)
        {
            var injured = combat.Allies.Where(ally => ally.IsAlive && ally.CurrentHp * 10L < ally.MaxHp).ToArray();
            foreach (var ally in injured)
                await CreatureCmd.GainBlock(ally, ally.MaxHp, ValueProp.Unpowered, null);
        }
        if (grantUnhurtBlock && combat is CombatState state) await ForestCombatHealthRuntime.GrantUnhurtBlock(state);
        if (!healAllies) return;
        foreach (var ally in combat.Allies.ToArray())
            if (ally.IsAlive && ally.CurrentHp < ally.MaxHp)
                await CreatureCmd.Heal(ally, ally.MaxHp * 0.02m);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class ForestCollectibleCardCostPatch
{
    internal static bool HasDiscount(ICombatState? combat)
    {
        if (combat?.RunState is not RunState run || !ForestGenerationService.IsEnabled(run)) return false;
        var inventory = ForestGenerationService.GetEngine(run);
        return inventory.Collectibles.Has(200) && inventory.Capacity - inventory.TotalPartCount >= 4;
    }

    private static void Postfix(ICombatState combatState, decimal originalCost, ref decimal __result)
    {
        if (originalCost >= 0m && HasDiscount(combatState)) __result = Math.Max(0m, __result - 1m);
    }
}

[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetAmountToSpend))]
internal static class ForestCollectibleXCostPatch
{
    private static void Postfix(CardEnergyCost __instance, CardModel ____card, ref int __result)
    {
        if (__instance.CostsX && ForestCollectibleCardCostPatch.HasDiscount(____card.CombatState))
            __result = Math.Max(0, __result - 1);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergySpent))]
internal static class ForestCollectibleXValuePatch
{
    private static void Prefix(ICombatState combatState, CardModel card, int amount)
    {
        // Paying zero at one Energy still captures X = 1. An empty energy pool captures X = 0.
        if (card.EnergyCost.CostsX && (amount > 0 || card.Owner.PlayerCombatState?.Energy > 0)
            && ForestCollectibleCardCostPatch.HasDiscount(combatState))
            card.EnergyCost.CapturedXValue = amount + 1;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset))]
internal static class ForestCollectibleFirstTurnEnergyPatch
{
    private static void Postfix(ICombatState combatState, Player player, ref Task __result)
    {
        if (player.PlayerCombatState?.TurnNumber != 1 || combatState.CurrentSide != CombatSide.Player
            || combatState.RunState is not RunState run || !ForestGenerationService.IsEnabled(run)) return;
        var bonus = ForestCollectibleRules.FirstTurnEnergy(ForestCollectibleService.GetInventory(run));
        if (bonus > 0) __result = GrantEnergy(__result, player, bonus);
    }

    private static async Task GrantEnergy(Task original, Player player, int bonus)
    {
        await original;
        await PlayerCmd.GainEnergy(bonus, player);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class ForestCollectibleKillHealingPatch
{
    private static void Postfix(ICombatState combatState, Creature? dealer, DamageResult results, ref Task __result)
    {
        if (dealer is { IsPlayer: true } && results.WasTargetKilled && results.Receiver.Side == CombatSide.Enemy
            && combatState.RunState is RunState run && ForestGenerationService.IsEnabled(run)
            && ForestCollectibleService.GetInventory(run).Has(118))
            __result = HealKiller(__result, dealer, results.Receiver);
    }

    private static async Task HealKiller(Task original, Creature dealer, Creature target)
    {
        await original;
        if (target.IsDead && dealer.IsAlive && dealer.CurrentHp < dealer.MaxHp)
            await CreatureCmd.Heal(dealer, dealer.MaxHp * 0.1m);
    }
}

[HarmonyPatch(typeof(CombatManager), "AfterCreatureAdded", new[] { typeof(Creature), typeof(CombatState) })]
internal static class ForestCollectibleBufferPatch
{
    private static void Postfix(Creature creature, CombatState state, ref Task __result)
    {
        if (creature.Side == CombatSide.Player && state.RunState is RunState run && ForestGenerationService.IsEnabled(run)
            && ForestCollectibleService.GetInventory(run).Has(62)) __result = Apply(__result, creature);
    }

    private static async Task Apply(Task original, Creature creature)
    {
        await original;
        await PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), creature, 2, null, null);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards))]
internal static class ForestCollectibleCombatGoldPatch
{
    private static void Prefix(IRunState runState, Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (room is not CombatRoom || runState is not RunState run || !ForestGenerationService.IsEnabled(run)
            || !ForestCollectibleService.GetInventory(run).Has(25)) return;
        for (var index = 0; index < rewards.Count; index++)
            if (rewards[index] is GoldReward { Amount: > 0 } gold && !gold.ToSerializable().WasGoldStolenBack)
                rewards[index] = new GoldReward((int)Math.Ceiling(gold.Amount * 1.5m), player);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantPrice))]
internal static class ForestCollectiblePricePatch
{
    private static void Postfix(IRunState runState, MerchantEntry entry, ref decimal __result)
    {
        if (entry is ForestShopPartEntry or MerchantRelicEntry && runState is RunState run
            && ForestGenerationService.IsEnabled(run) && ForestCollectibleService.GetInventory(run).Has(194))
            __result /= 2m;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.Gold), MethodType.Setter)]
internal static class ForestCollectibleGoldSpeedPatch
{
    private static void Postfix(Player __instance)
    {
        if (__instance.Creature.CombatState is CombatState combat) ForestSpeedRuntime.RefreshGoldSpeed(combat);
    }
}
