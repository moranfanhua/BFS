using BlackFlowSpire.Audio;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.SetRunState))]
internal static class BlackFlowSpireMusicRunPatch
{
    private static void Postfix(NRunMusicController __instance, IRunState runState) =>
        BlackFlowSpireMusicDirector.Attach(__instance, runState);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusic))]
internal static class BlackFlowSpireMusicUpdatePatch
{
    private static bool Prefix(NRunMusicController __instance)
    {
        if (!BlackFlowSpireMusicDirector.TryUpdate(__instance)) return true;
        __instance.UpdateAmbience();
        return false;
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), [])]
internal static class BlackFlowSpireMusicTrackPatch
{
    private static void Postfix(NRunMusicController __instance) => BlackFlowSpireMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.PlayCustomMusic))]
internal static class BlackFlowSpireMusicCustomPatch
{
    private static bool Prefix(NRunMusicController __instance) => !BlackFlowSpireMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopCustomMusic))]
internal static class BlackFlowSpireMusicCustomStopPatch
{
    private static bool Prefix(NRunMusicController __instance) => !BlackFlowSpireMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateCustomTrack))]
internal static class BlackFlowSpireMusicCustomTrackPatch
{
    private static bool Prefix(NRunMusicController __instance) => !BlackFlowSpireMusicDirector.TryUpdate(__instance);
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusicParameter))]
internal static class BlackFlowSpireMusicParameterPatch
{
    private static bool Prefix(NRunMusicController __instance) => !BlackFlowSpireMusicDirector.TryUpdate(__instance);
}
