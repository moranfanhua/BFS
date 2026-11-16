using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class BlackFlowSpirePartsBarPatch
{
    private static void Postfix(NGlobalUi __instance, RunState runState)
    {
        BlackFlowSpireNativeHud.Attach(__instance, runState);
        if (BlackFlowSpireGenerationService.IsEnabled(runState)) BlackFlowSpirePartsBar.Attach(__instance, runState);
    }
}
