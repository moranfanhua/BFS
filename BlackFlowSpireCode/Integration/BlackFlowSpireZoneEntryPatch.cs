using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class BlackFlowSpireZoneEntryPatch
{
    private static void Prefix(RunManager __instance, int currentActIndex, out Action? __state)
    {
        __state = null;
        var state = __instance.DebugOnlyGetState();
        if (state == null || !BlackFlowSpireGenerationService.IsEnabled(state)) return;
        var advances = currentActIndex > state.CurrentActIndex;

        // Wait for the destination room before covering it, including on floor one.
        void Entered()
        {
            if (!ReferenceEquals(__instance.DebugOnlyGetState(), state) || state.CurrentActIndex != currentActIndex) return;
            __instance.ActEntered -= Entered;
            BlackFlowSpireZoneEntryUi.TryShow(currentActIndex, playTransitionSound: advances);
        }

        __state = Entered;
        __instance.ActEntered += Entered;
    }

    private static void Postfix(RunManager __instance, Action? __state, ref Task __result)
    {
        if (__state != null) __result = DetachAfterEntry(__instance, __state, __result);
    }

    private static async Task DetachAfterEntry(RunManager manager, Action handler, Task entry)
    {
        try { await entry; }
        finally { manager.ActEntered -= handler; }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.LoadRun))]
internal static class BlackFlowSpireZoneEntryLoadPatch
{
    private static void Prefix(RunState runState, out int __state) => __state = runState.CurrentActIndex;

    private static void Postfix(RunState runState, int __state, ref Task __result)
    {
        if (BlackFlowSpireGenerationService.IsEnabled(runState))
            __result = HarmonyAsyncTaskBridge.After(__result, () => ShowAfterLoad(runState, __state));
    }

    private static void ShowAfterLoad(RunState state, int savedActIndex)
    {
        // A checkpointed exit can advance during restore. Its EnterAct hook
        // owns the destination intro, so avoid replaying the departed floor.
        if (!BlackFlowSpireRuntime.IsTraveling && ReferenceEquals(BlackFlowSpireRuntime.State, state)
            && state.CurrentActIndex == savedActIndex)
            BlackFlowSpireZoneEntryUi.TryShow(savedActIndex);
    }
}
