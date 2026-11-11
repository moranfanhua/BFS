using BlackFlowSpire.Audio;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.SetRunState))]
internal static class ForestMusicRunPatch
{
    private static void Postfix(NRunMusicController __instance, IRunState runState) =>
        ForestMusicDirector.Attach(__instance, runState);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusic))]
internal static class ForestMusicUpdatePatch
{
    private static bool Prefix(NRunMusicController __instance)
    {
        if (!ForestMusicDirector.TryUpdate(__instance)) return true;
        __instance.UpdateAmbience();
        return false;
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), [])]
internal static class ForestMusicTrackPatch
{
    private static void Postfix(NRunMusicController __instance) => ForestMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.PlayCustomMusic))]
internal static class ForestMusicCustomPatch
{
    private static bool Prefix(NRunMusicController __instance) => !ForestMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopCustomMusic))]
internal static class ForestMusicCustomStopPatch
{
    private static bool Prefix(NRunMusicController __instance) => !ForestMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateCustomTrack))]
internal static class ForestMusicCustomTrackPatch
{
    private static bool Prefix(NRunMusicController __instance) => !ForestMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusicParameter))]
internal static class ForestMusicParameterPatch
{
    private static bool Prefix(NRunMusicController __instance) => !ForestMusicDirector.TryUpdate(__instance);
}
