using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class BlackFlowSpireBattleRewardsPatch
{
    private static void Prefix(RewardsSet __instance, out int __state) => __state = __instance.Rewards.Count;

    private static void Postfix(RewardsSet __instance, AbstractRoom room, int __state)
    {
        var player = __instance.Player;
        if (room is not CombatRoom combat || player.RunState is not RunState run || !BlackFlowSpireGenerationService.IsEnabled(run)) return;
        combat.ExtraRewards.TryGetValue(player, out var extras);
        var rewards = __instance.Rewards;
        var extraCount = extras?.Count ?? 0;
        if (!BlackFlowSpireEncounterFactory.TryGetFixedSetup(combat.Encounter, out var setup))
        {
            ReplaceEventCards(rewards, __state, rewards.Count - extraCount, player, combat.RoomType);
            return;
        }
        var map = BlackFlowSpireGenerationService.GetOrGenerate(run);
        var loot = BlackFlowSpireBattleLoot.For(map, setup, run.CurrentActIndex == run.Acts.Count - 1);
        var cardRoomType = setup.Special == BlackFlowSpireSpecialEncounter.Hunt && loot.Experience > 0
            ? RoomType.Boss : combat.RoomType;
        var pack = loot.CardPack ? BlackFlowSpireBattleCardPackReward.Draw(player, cardRoomType) : null;
        RemoveNativeLoot(rewards, __state, rewards.Count - extraCount, keepCards: loot.CardPack && pack == null,
            keepPotions: loot.CardPack);
        var added = new List<Reward>();
        AddGold(added, combat, player, setup.Special, loot.Originium);
        if (pack != null) added.Add(pack);
        if (!BlackFlowSpireTeam.IsOnline || player == BlackFlowSpireTeam.Owner(run))
            AddSharedLoot(added, rewards, run, player, map, setup.Special, loot);
        rewards.InsertRange(rewards.Count - extraCount, added);
    }

    private static void ReplaceEventCards(List<Reward> rewards, int start, int end, Player player, RoomType roomType)
    {
        if (BlackFlowSpireBattleCardPackReward.Draw(player, roomType) is not { } pack) return;
        for (var index = start; index < end; index++)
        {
            if (rewards[index] is not CardReward cards) continue;
            if (!cards.IsPopulated) cards.OnSkipped();
            rewards[index] = pack;
            return;
        }
        rewards.Insert(end, pack);
    }

    private static void RemoveNativeLoot(List<Reward> rewards, int start, int end, bool keepCards, bool keepPotions)
    {
        for (var index = end - 1; index >= start; index--)
        {
            var reward = rewards[index];
            if (!keepCards && reward is CardReward cards)
            {
                // Unpopulated native card rewards still subscribe to relic changes.
                if (!cards.IsPopulated) cards.OnSkipped();
                rewards.RemoveAt(index);
            }
            else if (reward is RelicReward || reward is GoldReward gold && !gold.ToSerializable().WasGoldStolenBack
                || !keepPotions && reward is PotionReward)
                rewards.RemoveAt(index);
        }
    }

    private static void AddGold(List<Reward> rewards, CombatRoom combat, Player player, BlackFlowSpireSpecialEncounter special, int ingots)
    {
        var gold = ingots * BlackFlowSpirePartService.GoldPerOriginium;
        if (AscensionHelper.HasAscension(AscensionLevel.Poverty)) gold = gold * 3 / 4;
        if (combat.RoomType == RoomType.Monster && special == BlackFlowSpireSpecialEncounter.None)
            gold = (int)Math.Round(gold * combat.GoldProportion);
        if (gold > 0) rewards.Add(new GoldReward(gold, gold, player));
    }

    private static void AddSharedLoot(List<Reward> added, List<Reward> existing, RunState run, Player player,
        BlackFlowSpireMap map, BlackFlowSpireSpecialEncounter special, BlackFlowSpireBattleLoot loot)
    {
        var step = BlackFlowSpireGenerationService.GetProgress(run).StepsTaken;
        var seed = $"battle-loot/v1/{map.PositionNodeId}/{step}/{special}";
        var collectibleRng = new StableRandom(map.Seed, map.ActIndex, seed + "/collectibles");
        if (BlackFlowSpireBattleLoot.Roll(collectibleRng, loot.CollectibleChance))
        {
            var excluded = BlackFlowSpireCollectibleReward.Excluded(BlackFlowSpireGenerationService.GetEngine(run), existing);
            List<Reward> candidates = [];
            for (var index = 0; index < loot.CollectibleChoices; index++)
            {
                var item = BlackFlowSpireCollectibleRules.Draw(collectibleRng, excluded,
                        rare: loot.CollectibleChoices > 1, battleReward: true)
                    ?? BlackFlowSpireCollectibleRules.Draw(collectibleRng, excluded, rare: false, battleReward: true);
                if (item == null) break;
                excluded.Add(item.Id);
                candidates.Add(new BlackFlowSpireCollectibleReward(player, item));
            }
            if (candidates.Count > 0) added.Add(candidates.Count == 1 ? candidates[0] : LinkedRewardSets.Create(candidates, player));
        }

        var partRng = new StableRandom(map.Seed, map.ActIndex, seed + "/parts");
        var choices = loot.RollPartChoices(partRng);
        if (choices == 0) return;
        var parts = BlackFlowSpireShopParts.All.ToList();
        partRng.Shuffle(parts);
        for (var group = 0; group < loot.PartGroups; group++)
        {
            var candidates = parts.Skip(group * choices).Take(choices).Select(part => new BlackFlowSpirePartReward(player, part)).ToArray();
            added.Add(choices == 1 ? candidates[0] : LinkedRewardSets.Create(candidates, player));
        }
    }
}

[HarmonyPatch(typeof(NLinkedRewardSet), "Reload")]
internal static class BlackFlowSpireBattleRewardChoicePatch
{
    private static void Postfix(NLinkedRewardSet __instance, NRewardsScreen ____rewardsScreen, Control ____rewardContainer)
    {
        if (!IsForestChoice(__instance)) return;
        var signal = NRewardButton.SignalName.RewardClaimed;
        foreach (var button in ____rewardContainer.GetChildren().OfType<NRewardButton>())
        {
            foreach (var connection in button.GetSignalConnectionList(signal))
            {
                var callable = connection["callable"].AsCallable();
                if (callable.Delegate is Action callback && ReferenceEquals(callback.Target, __instance)
                    && callback.Method.Name == "GetReward")
                    button.Disconnect(signal, callable);
            }
            // The native handler takes no argument, but the child signal emits its button.
            button.Connect(signal, Callable.From<NRewardButton>(_ => ____rewardsScreen.RewardCollectedFrom(__instance)));
        }
    }

    internal static bool IsForestChoice(NLinkedRewardSet group) => group.LinkedRewardSet.Rewards.Count > 0
        && group.LinkedRewardSet.Rewards.All(reward => reward is BlackFlowSpireCollectibleReward or BlackFlowSpirePartReward);
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))]
internal static class BlackFlowSpireBattleSettlementPatch
{
    private static void Postfix(NRewardsScreen __result, RewardsSet set, bool isTerminal, IRunState runState)
    {
        if (isTerminal && set.Room is CombatRoom room && runState is RunState run && BlackFlowSpireGenerationService.IsEnabled(run))
            BlackFlowSpireBattleSettlementScreen.Attach(__result, run, room);
    }
}

[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
internal static class BlackFlowSpireBattleSettlementProceedPatch
{
    private static bool Prefix(NRewardsScreen __instance) =>
        BlackFlowSpireBattleSettlementScreen.Find(__instance)?.InterceptProceed() != true;
}

[HarmonyPatch]
internal static class BlackFlowSpireBattleSettlementFocusPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(NRewardsScreen), nameof(NRewardsScreen.DefaultFocusedControl));
        yield return AccessTools.PropertyGetter(typeof(NRewardsScreen), nameof(NRewardsScreen.FocusedControlFromTopBar));
    }

    private static bool Prefix(NRewardsScreen __instance, ref Control __result)
    {
        if (BlackFlowSpireBattleSettlementScreen.Find(__instance) is not { } settlement) return true;
        __result = settlement.DefaultFocus;
        return false;
    }
}

[HarmonyPatch(typeof(NCombatRewardFtue), nameof(NCombatRewardFtue.Create))]
internal static class BlackFlowSpireBattleSkipTutorialPatch
{
    private static void Prefix(ref Control rewardsContainer)
    {
        if (NOverlayStack.Instance?.Peek() is NRewardsScreen screen
            && BlackFlowSpireBattleSettlementScreen.Find(screen) is { } settlement)
            rewardsContainer = settlement.RewardList;
    }
}

[HarmonyPatch(typeof(NRelicRewardFtue), nameof(NRelicRewardFtue.Create))]
internal static class BlackFlowSpireBattleRelicTutorialPatch
{
    private static void Prefix(ref Control relicReward)
    {
        if (relicReward is NRewardButton button && NOverlayStack.Instance?.Peek() is NRewardsScreen screen
            && BlackFlowSpireBattleSettlementScreen.Find(screen)?.CardFor(button) is { } card)
            relicReward = card;
    }
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.GenerateWithoutOffering))]
internal static class BlackFlowSpireCollectibleRewardsPatch
{
    private static void Prefix(RewardsSet __instance, bool ____isGenerated)
    {
        if (!____isGenerated && __instance.Player.RunState is RunState run && BlackFlowSpireGenerationService.IsEnabled(run))
            BlackFlowSpireCollectibleReward.ReplaceRelics(run, __instance.Player, __instance.Rewards, __instance.Room);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards))]
internal static class BlackFlowSpireAddedCollectibleRewardsPatch
{
    private static void Postfix(IRunState runState, Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (runState is RunState run && BlackFlowSpireGenerationService.IsEnabled(run))
            BlackFlowSpireCollectibleReward.ReplaceRelics(run, player, rewards, room);
    }
}
