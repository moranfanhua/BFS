using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

internal static class BlackFlowSpireNativeTurnAccess
{
    private static readonly Type TurnStateType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState", true)!;
    private static readonly MethodInfo StateGetter = AccessTools.PropertyGetter(TurnStateType, "State");
    private static readonly MethodInfo TokenGetter = AccessTools.PropertyGetter(TurnStateType, "Ct");
    public static CombatState State(object turn) => (CombatState)StateGetter.Invoke(turn, null)!;
    public static bool IsCanceled(object turn) => ((CancellationToken)TokenGetter.Invoke(turn, null)!).IsCancellationRequested;
}

[HarmonyPatch(typeof(CombatManager), "SwitchSides")]
internal static class BlackFlowSpireSpeedSwitchPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(CombatManager __instance, object __0)
    {
        var state = BlackFlowSpireNativeTurnAccess.State(__0);
        if (!BlackFlowSpireSpeedRuntime.IsEnabled(state) || !__instance.IsInProgress || BlackFlowSpireNativeTurnAccess.IsCanceled(__0)) return;
        var extra = state.CurrentSide == CombatSide.Player && __instance.PlayersTakingExtraTurn.Count > 0;
        var next = BlackFlowSpireSpeedRuntime.NextSide(state, extra);
        // Feed the chosen branch into the original method. Its turn counters,
        // extra-turn bookkeeping, creature notifications and TurnEnded event
        // remain native; only the assumption of alternating sides is replaced.
        if (!extra) state.CurrentSide = next == CombatSide.Enemy ? CombatSide.Player : CombatSide.Enemy;
    }
}

[HarmonyPatch(typeof(CombatManager), "StartTurn")]
internal static class BlackFlowSpireSpeedStartTurnPatch
{
    private static void Prefix(object __0)
    {
        if (!BlackFlowSpireNativeTurnAccess.IsCanceled(__0)) BlackFlowSpireSpeedRuntime.BeforeTurnStart(BlackFlowSpireNativeTurnAccess.State(__0));
    }
}

[HarmonyPatch(typeof(CombatManager), "StartTurn", MethodType.Async)]
internal static class BlackFlowSpireSpeedIntentPatch
{
    internal static bool Available { get; private set; }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(Creature), nameof(Creature.PrepareForNextTurn));
        var replacement = AccessTools.Method(typeof(BlackFlowSpireSpeedRuntime), nameof(BlackFlowSpireSpeedRuntime.PrepareEnemyIntentForPlayerTurn));
        var rewriter = HarmonyIlRewriter.From(source);
        var matches = rewriter.FindAll(HarmonyIl.IsCall(original));
        if (matches.Count != 1)
        {
            Available = false;
            Entry.Logger.Warn($"[BlackFlowSpire] Expected one enemy-intent preparation call, found {matches.Count}. Using native turn order.");
            return rewriter.Instructions();
        }
        var rewrite = rewriter.ReplaceCall("Prepare enemy intent for player turn", original, replacement);
        var instructions = rewriter.InstructionsChecked(rewrite);
        Available = true;
        return instructions;
    }
}

[HarmonyPatch(typeof(CombatManager), "AwaitTurnEndAndSwitchSides")]
internal static class BlackFlowSpireSpeedEnemyActionPatch
{
    private static void Postfix(object __0, ref Task<Func<Task>?> __result)
    {
        var state = BlackFlowSpireNativeTurnAccess.State(__0);
        if (BlackFlowSpireSpeedRuntime.IsEnabled(state)) __result = AfterSwitch(__result, state);
    }

    private static async Task<Func<Task>?> AfterSwitch(Task<Func<Task>?> original, CombatState state) =>
        BlackFlowSpireSpeedRuntime.DeferEnemyAction(state, await original);
}

[HarmonyPatch(typeof(CombatManager), "AfterCreatureAdded", new[] { typeof(Creature), typeof(CombatState) })]
internal static class BlackFlowSpireSpeedSpawnIntentPatch
{
    private static void Postfix(Creature creature, CombatState state, ref Task __result)
    {
        if (BlackFlowSpireSpeedRuntime.IsEnabled(state))
            __result = HarmonyAsyncTaskBridge.After(__result, () => BlackFlowSpireSpeedRuntime.RememberSpawnedEnemyIntent(state, creature));
    }
}
