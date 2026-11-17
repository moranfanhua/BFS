using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Commands;

public sealed class BlackFlowSpireRevealConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => Entry.ModId.ToLowerInvariant() + "_reveal";
    public override string Args => "";
    public override string Description => "Reveal all nodes on the current forest floor.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args) => args.Length == 0
        ? BlackFlowSpireExplorationConsole.Apply(map =>
        {
            var count = BlackFlowSpireFog.RevealAll(map).Count;
            return ($"Revealed {count} new nodes on this floor.", count > 0);
        })
        : new(false, $"Usage: {CmdName}");
}

public sealed class BlackFlowSpireFreeMoveConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => Entry.ModId.ToLowerInvariant() + "_free_move";
    public override string Args => "[on|off]";
    public override string Description => "Ignore or restore movement range on the current forest floor.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        var mode = args.Length == 0 ? "on" : args[0].ToLowerInvariant();
        if (args.Length > 1 || mode is not ("on" or "off")) return new(false, $"Usage: {CmdName} {Args}");
        var enabled = mode == "on";
        return BlackFlowSpireExplorationConsole.Apply(map =>
        {
            var changed = map.IgnoreMovementRange != enabled;
            map.IgnoreMovementRange = enabled;
            return ($"Unrestricted movement range is {mode} on this floor.", changed);
        });
    }
}

internal static class BlackFlowSpireExplorationConsole
{
    internal static CmdResult Apply(Func<BlackFlowSpireMap, (string Message, bool Changed)> change)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || BlackFlowSpireRuntime.State is not { } state)
            return new(false, "No active forest run.");
        if (!BlackFlowSpireTeam.CanManage(state) || BlackFlowSpireRuntime.IsTraveling || CombatManager.Instance.IsInProgress)
            return new(false, "Only the exploration leader can use this command while exploration is idle.");

        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        var (message, changed) = change(map);
        if (changed)
        {
            BlackFlowSpireGenerationService.MarkChanged(state, map, progress);
            BlackFlowSpireTeam.Publish(state);
            if (NMapScreen.Instance is { IsOpen: true } screen)
                BlackFlowSpireMapUi.Show(screen, state, BlackFlowSpireMapUi.IsInspection(screen));
            TaskHelper.RunSafely(SaveManager.Instance.SaveRun(null));
        }
        return new(true, message);
    }
}
