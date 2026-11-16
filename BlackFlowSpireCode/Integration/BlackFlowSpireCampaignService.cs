using BlackFlowSpire.Map.Models;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Integration;

internal static class BlackFlowSpireCampaignService
{
    public static void ConfigureNewRun(RunState state)
    {
        var sources = state.Acts.ToArray();
        if (sources.Length < 3) throw new InvalidOperationException("The forest campaign requires three source acts.");
        // Each floor owns a fresh mutable act: encounter queues, event sets and
        // boss selection must not be shared between the two floors of a region.
        var acts = BlackFlowSpireFloorPlan.Floors
            .Select(floor => ModelDb.GetById<ActModel>(sources[floor.SourceActIndex].Id).ToMutable()).ToArray();
        AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Acts)).Invoke(state, [acts]);
    }
}
