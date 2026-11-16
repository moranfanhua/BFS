using BlackFlowSpire.Map.Stages;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.TriggerEliteSecondPhase))]
internal static class BlackFlowSpireStagePhaseMusicPatch
{
    private static readonly AccessTools.FieldRef<NRunMusicController, IRunState> MusicRunState =
        AccessTools.FieldRefAccess<NRunMusicController, IRunState>("_runState");

    private static bool Prefix(NRunMusicController __instance)
    {
        // Forest battles use their own whole-track events, including bosses.
        if (BlackFlowSpireRuntime.IsActive) return false;
        // Native InfestedPower reveals the spawned worms only after this call;
        // BygoneEffigy also calls it before applying its wake-up buff. The native
        // method throws outside elite rooms, interrupting both ordinary stages.
        if (MusicRunState(__instance).CurrentRoom is CombatRoom { RoomType: RoomType.Monster } room
            && BlackFlowSpireEncounterFactory.TryGet(room.Encounter, out var setup)
            && setup.Stage?.Variant == BlackFlowSpireBattleVariant.Normal)
        {
            Entry.Logger.Info("[BlackFlowSpire] Keep normal battle music during phase transition: " + setup.Stage.StageId);
            return false;
        }
        return true;
    }
}
