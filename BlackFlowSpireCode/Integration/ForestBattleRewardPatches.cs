using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Stages;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
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
internal static class ForestBattleGoldPatch
{
    private static readonly int[] NormalIngots = [1, 2, 2, 2, 2];
    private static readonly int[] EmergencyIngots = [2, 2, 3, 3, 5];

    private static void Prefix(RewardsSet __instance, out int __state) => __state = __instance.Rewards.Count;

    private static void Postfix(RewardsSet __instance, AbstractRoom room, int __state)
    {
        var player = __instance.Player;
        if (room is not CombatRoom combat || player.RunState is not RunState run || !ForestGenerationService.IsEnabled(run)) return;
        var floor = run.CurrentActIndex;
        var special = ForestEncounterFactory.SpecialKind(combat.Encounter);
        int ingots;
        if (special == ForestSpecialEncounter.Resident)
        {
            var map = ForestGenerationService.GetOrGenerate(run);
            ingots = map.GetNode(map.PositionNodeId).Content == ForestNodeContent.BattleSavage ? 5 : 3;
        }
        else if (special == ForestSpecialEncounter.Hunt)
        {
            // The double-Hopper chase can happen on floors without an original chase payout.
            ingots = floor is 2 or 4 ? BossIngots(floor) : NormalIngots[floor];
        }
        else if (ForestEncounterFactory.TryGet(combat.Encounter, out var setup))
        {
            ingots = setup.Stage!.Variant switch
            {
                ForestBattleVariant.Normal => NormalIngots[floor],
                ForestBattleVariant.Emergency => EmergencyIngots[floor],
                _ => BossIngots(floor)
            };
        }
        else return;

        var gold = ingots * ForestPartService.GoldPerOriginium;
        if (AscensionHelper.HasAscension(AscensionLevel.Poverty)) gold = gold * 3 / 4;
        if (combat.RoomType == RoomType.Monster && special == ForestSpecialEncounter.None)
            gold = (int)Math.Round(gold * combat.GoldProportion);

        combat.ExtraRewards.TryGetValue(player, out var extras);
        var rewards = __instance.Rewards;
        var endOfRoomRewards = rewards.Count - (extras?.Count ?? 0);
        for (var index = __state; index < endOfRoomRewards; index++)
        {
            if (rewards[index] is not GoldReward existing || existing.ToSerializable().WasGoldStolenBack) continue;
            if (gold == 0) rewards.RemoveAt(index);
            else rewards[index] = new GoldReward(gold, gold, player);
            return;
        }

        // The native final boss normally has no base loot. The forest boss still drops ingots.
        if (gold > 0 && combat.RoomType == RoomType.Boss && floor == run.Acts.Count - 1 && endOfRoomRewards == __state)
            rewards.Insert(__state, new GoldReward(gold, gold, player));
    }

    private static int BossIngots(int floor) => ForestCommandLevel.BattleExperience(floor, ForestBattleVariant.Boss) / 6;
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))]
internal static class ForestBattleSettlementPatch
{
    private static void Postfix(NRewardsScreen __result, RewardsSet set, bool isTerminal, IRunState runState)
    {
        if (isTerminal && set.Room is CombatRoom room && runState is RunState run && ForestGenerationService.IsEnabled(run))
            ForestBattleSettlementScreen.Attach(__result, run, room);
    }
}

[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
internal static class ForestBattleSettlementProceedPatch
{
    private static bool Prefix(NRewardsScreen __instance) =>
        ForestBattleSettlementScreen.Find(__instance)?.InterceptProceed() != true;
}

[HarmonyPatch]
internal static class ForestBattleSettlementFocusPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(NRewardsScreen), nameof(NRewardsScreen.DefaultFocusedControl));
        yield return AccessTools.PropertyGetter(typeof(NRewardsScreen), nameof(NRewardsScreen.FocusedControlFromTopBar));
    }

    private static bool Prefix(NRewardsScreen __instance, ref Control __result)
    {
        if (ForestBattleSettlementScreen.Find(__instance) is not { } settlement) return true;
        __result = settlement.DefaultFocus;
        return false;
    }
}

[HarmonyPatch(typeof(NCombatRewardFtue), nameof(NCombatRewardFtue.Create))]
internal static class ForestBattleSkipTutorialPatch
{
    private static void Prefix(ref Control rewardsContainer)
    {
        if (NOverlayStack.Instance?.Peek() is NRewardsScreen screen
            && ForestBattleSettlementScreen.Find(screen) is { } settlement)
            rewardsContainer = settlement.RewardList;
    }
}

[HarmonyPatch(typeof(NRelicRewardFtue), nameof(NRelicRewardFtue.Create))]
internal static class ForestBattleRelicTutorialPatch
{
    private static void Prefix(ref Control relicReward)
    {
        if (relicReward is NRewardButton button && NOverlayStack.Instance?.Peek() is NRewardsScreen screen
            && ForestBattleSettlementScreen.Find(screen)?.CardFor(button) is { } card)
            relicReward = card;
    }
}
