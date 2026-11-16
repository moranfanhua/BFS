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
internal static class BlackFlowSpireConceptCombatStartPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (runState is RunState state && combatState is CombatState combat)
            __result = HarmonyAsyncTaskBridge.After(__result, () =>
            {
                if (BlackFlowSpireGenerationService.IsEnabled(state))
                    BlackFlowSpireSpeedRuntime.BeginCombat(combat, BlackFlowSpireConceptService.CombatAttackSpeed(state));
            });
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class BlackFlowSpireConceptCombatEndPatch
{
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result)
    {
        if (runState is RunState state)
            __result = HarmonyAsyncTaskBridge.After(__result, () => SettleVictory(state, room));
    }

    private static async Task SettleVictory(RunState state, CombatRoom room)
    {
        if (!BlackFlowSpireGenerationService.IsEnabled(state)) return;
        BlackFlowSpireCombatHealthRuntime.RecordSpecialVictory(state, room);
        await BlackFlowSpireConceptService.EndCombat(state);
        BlackFlowSpireTeam.Publish(state);
    }
}

internal static class BlackFlowSpireNaturalCardGain
{
    internal static void Register() =>
        RitsuLibFramework.SubscribeLifecycle<CardMovedBetweenPilesEvent>(OnCardMoved, replayCurrentState: false);

    private static void OnCardMoved(CardMovedBetweenPilesEvent evt)
    {
        if (BlackFlowSpireTeam.IsGuest || evt.PreviousPile == PileType.Deck || evt.Card.Pile?.Type != PileType.Deck
            || evt.RunState is not RunState state || !BlackFlowSpireGenerationService.IsEnabled(state)
            || evt.Card.Owner != LocalContext.GetMe(state)) return;
        BlackFlowSpireGenerationService.ModifyEngine(state, BlackFlowSpireNaturalRules.CardObtained);
    }
}

