using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Commands;

public sealed class BlackFlowSpireHopeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => Entry.ModId.ToLowerInvariant() + "_hope";
    public override string Args => "[signed-delta]";
    public override string Description => "Read or change the shared Hope balance in an active forest run.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } run)
            return new(false, "No active forest run.");
        if (args.Length == 0) return new(true, FormatHope(run));
        if (args is not [var value] || !int.TryParse(value, out var delta))
            return new(false, $"Usage: {CmdName} {Args}");
        if (!BlackFlowSpireTeam.CanManage(run) || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress)
            return new(false, "Only the exploration leader can change Hope while exploration is idle.");
        if (!BlackFlowSpireGenerationService.TryChangeHope(run, delta))
            return new(false, $"{FormatHope(run)}. Change rejected.");
        if (delta != 0) TaskHelper.RunSafely(SaveManager.Instance.SaveRun(null));
        return new(true, FormatHope(run));
    }

    private static string FormatHope(RunState run)
    {
        var hope = BlackFlowSpireGenerationService.GetHopeStatus(run);
        return $"Hope: {hope.Balance}/{hope.Maximum}";
    }
}
