using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class ForestPartsBarPatch
{
    private static void Postfix(NGlobalUi __instance, RunState runState)
    {
        ForestNativeHud.Attach(__instance, runState);
        if (ForestGenerationService.IsEnabled(runState)) ForestPartsBar.Attach(__instance, runState);
    }
}
