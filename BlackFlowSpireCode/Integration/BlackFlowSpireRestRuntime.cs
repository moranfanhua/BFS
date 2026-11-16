using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Integration;

public static partial class BlackFlowSpireRuntime
{
    internal static Task ResolveRest(BlackFlowSpireRestChoice? choice)
    {
        var choiceId = choice.HasValue ? (int)choice.Value : 0;
        return SubmitTeamAction(BlackFlowSpireTeamAction.Rest, choiceId, () => ResolveRestCore(choiceId));
    }

    internal static async Task ResolveRestCore(int choiceId)
    {
        if (IsTraveling || !CanContinueRun || CombatManager.Instance.IsInProgress || NMapScreen.Instance is not { } screen) return;
        var state = State!;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (!BlackFlowSpireRestRules.IsPending(map, progress)) return;

        var leave = choiceId == 0;
        try
        {
            IsTraveling = true;
            LastError = null;
            if (leave)
            {
                if (!BlackFlowSpireRestRules.TryLeave(map, progress)) return;
                BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            }
            else if (!BlackFlowSpireGenerationService.TryChooseRest(state, map, progress, (BlackFlowSpireRestChoice)choiceId))
                return;
            await SaveManager.Instance.SaveRun(null);
        }
        catch (Exception error)
        {
            LastError = error.Message;
            Entry.Logger.Error($"[BlackFlowSpire] Rest choice failed: {error}");
        }
        finally { IsTraveling = false; }

        if (leave && !BlackFlowSpireRestRules.IsPending(map, progress))
        {
            BlackFlowSpireRestUi.Dismiss(screen);
            await RefreshMapAfterTravel(state, map, progress, screen, focusCurrent: true);
            ActiveScreenContext.Instance.Update();
        }
    }
}
