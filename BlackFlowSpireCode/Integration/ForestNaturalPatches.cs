using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class ForestConceptCombatStartPatch
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result) => __result = AfterStart(__result, runState, combatState);

    private static async Task AfterStart(Task original, IRunState runState, ICombatState? combatState)
    {
        await original;
        if (runState is RunState state && ForestGenerationService.IsEnabled(state))
        {
            if (combatState is CombatState combat)
                ForestSpeedRuntime.BeginCombat(combat, ForestConceptService.CombatAttackSpeed(state));
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class ForestConceptCombatEndPatch
{
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result) =>
        __result = AfterVictory(__result, runState, room);

    private static async Task AfterVictory(Task original, IRunState runState, CombatRoom room)
    {
        await original;
        if (runState is RunState state && ForestGenerationService.IsEnabled(state))
        {
            ForestCombatHealthRuntime.RecordSpecialVictory(state, room);
            await ForestConceptService.EndCombat(state);
            ForestTeam.Publish(state);
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class ForestNaturalCardGainPatch
{
    private static void Postfix(IRunState runState, CardModel card, PileType oldPile, ref Task __result) =>
        __result = AfterChange(__result, runState, card, oldPile);

    private static async Task AfterChange(Task original, IRunState runState, CardModel card, PileType oldPile)
    {
        await original;
        if (ForestTeam.IsGuest || oldPile == PileType.Deck || card.Pile?.Type != PileType.Deck || runState is not RunState state
            || !ForestGenerationService.IsEnabled(state) || card.Owner != LocalContext.GetMe(state)) return;
        ForestGenerationService.ModifyEngine(state, ForestNaturalRules.CardObtained);
    }
}

