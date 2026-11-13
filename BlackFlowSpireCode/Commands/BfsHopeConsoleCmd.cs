using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Commands;

public sealed class BfsHopeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "bfs_hope";
    public override string Args => "[signed-delta]";
    public override string Description => "Read or change the shared Hope balance in an active forest run.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.State is not { } run)
            return new(false, "No active forest run.");
        if (args.Length == 0) return new(true, $"Hope: {ForestGenerationService.GetHope(run)}");
        if (args is not [var value] || !int.TryParse(value, out var delta))
            return new(false, "Usage: bfs_hope " + Args);
        if (!ForestTeam.CanManage(run) || ForestRuntime.IsTraveling || CombatManager.Instance.IsInProgress)
            return new(false, "Only the exploration leader can change Hope while exploration is idle.");
        if (!ForestGenerationService.TryChangeHope(run, delta))
            return new(false, $"Hope: {ForestGenerationService.GetHope(run)}. Change rejected.");
        if (delta != 0) TaskHelper.RunSafely(SaveManager.Instance.SaveRun(null));
        return new(true, $"Hope: {ForestGenerationService.GetHope(run)}");
    }
}
