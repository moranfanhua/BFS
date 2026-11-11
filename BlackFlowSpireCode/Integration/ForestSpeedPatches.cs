using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BlackFlowSpire.Integration;

internal static class ForestNativeTurnAccess
{
    private static readonly Type TurnStateType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState", true)!;
    private static readonly MethodInfo StateGetter = AccessTools.PropertyGetter(TurnStateType, "State");
    private static readonly MethodInfo TokenGetter = AccessTools.PropertyGetter(TurnStateType, "Ct");
    public static CombatState State(object turn) => (CombatState)StateGetter.Invoke(turn, null)!;
    public static bool IsCanceled(object turn) => ((CancellationToken)TokenGetter.Invoke(turn, null)!).IsCancellationRequested;
}

[HarmonyPatch(typeof(CombatManager), "SwitchSides")]
internal static class ForestSpeedSwitchPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(CombatManager __instance, object __0)
    {
        var state = ForestNativeTurnAccess.State(__0);
        if (!ForestSpeedRuntime.IsEnabled(state) || !__instance.IsInProgress || ForestNativeTurnAccess.IsCanceled(__0)) return;
        var extra = state.CurrentSide == CombatSide.Player && __instance.PlayersTakingExtraTurn.Count > 0;
        var next = ForestSpeedRuntime.NextSide(state, extra);
        // Feed the chosen branch into the original method. Its turn counters,
        // extra-turn bookkeeping, creature notifications and TurnEnded event
        // remain native; only the assumption of alternating sides is replaced.
        if (!extra) state.CurrentSide = next == CombatSide.Enemy ? CombatSide.Player : CombatSide.Enemy;
    }
}

[HarmonyPatch(typeof(CombatManager), "StartTurn")]
internal static class ForestSpeedStartTurnPatch
{
    private static void Prefix(object __0)
    {
        if (!ForestNativeTurnAccess.IsCanceled(__0)) ForestSpeedRuntime.BeforeTurnStart(ForestNativeTurnAccess.State(__0));
    }
}

[HarmonyPatch]
internal static class ForestSpeedIntentPatch
{
    internal static bool Available { get; private set; }

    private static MethodBase TargetMethod() => AccessTools.Method(typeof(CombatManager), "StartTurn")
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
    {
        var original = AccessTools.Method(typeof(Creature), nameof(Creature.PrepareForNextTurn));
        var replacement = AccessTools.Method(typeof(ForestSpeedRuntime), nameof(ForestSpeedRuntime.PrepareEnemyIntentForPlayerTurn));
        var instructions = source.ToList();
        var matches = instructions.Where(instruction => instruction.Calls(original)).ToArray();
        Available = matches.Length == 1;
        if (!Available)
        {
            Entry.Logger.Warn($"[Forest] Expected one enemy-intent preparation call, found {matches.Length}. Using native turn order.");
            return instructions;
        }
        matches[0].opcode = OpCodes.Call;
        matches[0].operand = replacement;
        return instructions;
    }
}

[HarmonyPatch(typeof(CombatManager), "AwaitTurnEndAndSwitchSides")]
internal static class ForestSpeedEnemyActionPatch
{
    private static void Postfix(object __0, ref Task<Func<Task>?> __result)
    {
        var state = ForestNativeTurnAccess.State(__0);
        if (ForestSpeedRuntime.IsEnabled(state)) __result = AfterSwitch(__result, state);
    }

    private static async Task<Func<Task>?> AfterSwitch(Task<Func<Task>?> original, CombatState state) =>
        ForestSpeedRuntime.DeferEnemyAction(state, await original);
}

[HarmonyPatch(typeof(CombatManager), "AfterCreatureAdded", new[] { typeof(Creature), typeof(CombatState) })]
internal static class ForestSpeedSpawnIntentPatch
{
    private static void Postfix(Creature creature, CombatState state, ref Task __result)
    {
        if (ForestSpeedRuntime.IsEnabled(state)) __result = AfterSpawn(__result, creature, state);
    }

    private static async Task AfterSpawn(Task original, Creature creature, CombatState state)
    {
        await original;
        ForestSpeedRuntime.RememberSpawnedEnemyIntent(state, creature);
    }
}
