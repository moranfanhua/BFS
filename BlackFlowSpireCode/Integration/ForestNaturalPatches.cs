using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Lifecycle;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class ForestConceptCombatStartPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (runState is RunState state && combatState is CombatState combat)
            __result = HarmonyAsyncTaskBridge.After(__result, () =>
            {
                if (ForestGenerationService.IsEnabled(state))
                    ForestSpeedRuntime.BeginCombat(combat, ForestConceptService.CombatAttackSpeed(state));
            });
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class ForestConceptCombatEndPatch
{
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result)
    {
        if (runState is RunState state)
            __result = HarmonyAsyncTaskBridge.After(__result, () => SettleVictory(state, room));
    }

    private static async Task SettleVictory(RunState state, CombatRoom room)
    {
        if (!ForestGenerationService.IsEnabled(state)) return;
        ForestCombatHealthRuntime.RecordSpecialVictory(state, room);
        await ForestConceptService.EndCombat(state);
        ForestTeam.Publish(state);
    }
}

internal static class ForestNaturalCardGain
{
    internal static void Register() =>
        RitsuLibFramework.SubscribeLifecycle<CardMovedBetweenPilesEvent>(OnCardMoved, replayCurrentState: false);

    private static void OnCardMoved(CardMovedBetweenPilesEvent evt)
    {
        if (ForestTeam.IsGuest || evt.PreviousPile == PileType.Deck || evt.Card.Pile?.Type != PileType.Deck
            || evt.RunState is not RunState state || !ForestGenerationService.IsEnabled(state)
            || evt.Card.Owner != LocalContext.GetMe(state)) return;
        ForestGenerationService.ModifyEngine(state, ForestNaturalRules.CardObtained);
    }
}

