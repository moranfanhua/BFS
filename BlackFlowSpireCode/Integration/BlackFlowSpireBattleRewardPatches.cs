using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class BlackFlowSpireBattleRewardsPatch
{
    private static readonly int[] NormalIngots = [1, 2, 2, 2, 2];
    private static readonly int[] EmergencyIngots = [2, 2, 3, 3, 5];

    private static void Prefix(RewardsSet __instance, out int __state) => __state = __instance.Rewards.Count;

    private static void Postfix(RewardsSet __instance, AbstractRoom room, int __state)
    {
        var player = __instance.Player;
        if (room is not CombatRoom combat || player.RunState is not RunState run || !BlackFlowSpireGenerationService.IsEnabled(run)) return;
        combat.ExtraRewards.TryGetValue(player, out var extras);
        var rewards = __instance.Rewards;
        var endOfRoomRewards = rewards.Count - (extras?.Count ?? 0);
        AdjustGold(rewards, __state, endOfRoomRewards, combat, run, player);
        ReplaceCardReward(rewards, __state, rewards.Count - (extras?.Count ?? 0), player, combat.RoomType);
    }

    private static void AdjustGold(List<Reward> rewards, int start, int end, CombatRoom combat, RunState run, Player player)
    {
        var floor = run.CurrentActIndex;
        var special = BlackFlowSpireEncounterFactory.SpecialKind(combat.Encounter);
        int ingots;
        if (special == BlackFlowSpireSpecialEncounter.Resident)
        {
            var map = BlackFlowSpireGenerationService.GetOrGenerate(run);
            ingots = map.GetNode(map.PositionNodeId).Content == BlackFlowSpireNodeContent.BattleSavage ? 5 : 3;
        }
        else if (special == BlackFlowSpireSpecialEncounter.Hunt)
        {
            // The double-Hopper chase can happen on floors without an original chase payout.
            ingots = floor is 2 or 4 ? BossIngots(floor) : NormalIngots[floor];
        }
        else if (BlackFlowSpireEncounterFactory.TryGet(combat.Encounter, out var setup))
        {
            ingots = setup.Stage!.Variant switch
            {
                BlackFlowSpireBattleVariant.Normal => NormalIngots[floor],
                BlackFlowSpireBattleVariant.Emergency => EmergencyIngots[floor],
                _ => BossIngots(floor)
            };
        }
        else return;

        var gold = ingots * BlackFlowSpirePartService.GoldPerOriginium;
        if (AscensionHelper.HasAscension(AscensionLevel.Poverty)) gold = gold * 3 / 4;
        if (combat.RoomType == RoomType.Monster && special == BlackFlowSpireSpecialEncounter.None)
            gold = (int)Math.Round(gold * combat.GoldProportion);

        for (var index = start; index < end; index++)
        {
            if (rewards[index] is not GoldReward existing || existing.ToSerializable().WasGoldStolenBack) continue;
            if (gold == 0) rewards.RemoveAt(index);
            else rewards[index] = new GoldReward(gold, gold, player);
            return;
        }

        // The native final boss normally has no base loot. The forest boss still drops ingots.
        if (gold > 0 && combat.RoomType == RoomType.Boss && floor == run.Acts.Count - 1 && end == start)
            rewards.Insert(start, new GoldReward(gold, gold, player));
    }

    private static void ReplaceCardReward(List<Reward> rewards, int start, int end, Player player, RoomType roomType)
    {
        if (BlackFlowSpireBattleCardPackReward.Draw(player, roomType) is not { } pack) return;
        for (var index = start; index < end; index++)
        {
            if (rewards[index] is not CardReward cardReward) continue;
            // The unpopulated native reward subscribed to relic changes in its constructor.
            if (!cardReward.IsPopulated) cardReward.OnSkipped();
            rewards[index] = pack;
            return;
        }
        rewards.Insert(end, pack);
    }

    private static int BossIngots(int floor) => BlackFlowSpireCommandLevel.BattleExperience(floor, BlackFlowSpireBattleVariant.Boss) / 6;
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
