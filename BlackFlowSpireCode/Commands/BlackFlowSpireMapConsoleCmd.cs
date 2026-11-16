using System.Globalization;
using System.Text;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Serialization;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Commands;

public sealed class BlackFlowSpireMapConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => Entry.ModId.ToLowerInvariant() + "_map";
    public override string Args => "[seed|run|check|stage-check|step|hunt-preview] [actIndex|nodeId]";
    public override string Description => "Export forest previews. 'run' stores a snapshot; 'step <nodeId>' updates preview position and BFS discovery; 'check' verifies native serialization.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].Equals("stage-check", StringComparison.OrdinalIgnoreCase))
                return new(true, BlackFlowSpireStageChecks.Verify());
            if (args.Length == 1 && args[0].Equals("hunt-preview", StringComparison.OrdinalIgnoreCase))
                return new(BlackFlowSpireHuntUi.Preview(), "Hunt UI preview requested. Run data is unchanged; use the close button after confirming the notice.");
            if (args.Length > 2) return new(false, $"Usage: {CmdName} {Args}");
            if (args.Length == 1 && args[0].Equals("check", StringComparison.OrdinalIgnoreCase))
            {
                BlackFlowSpireNativeMapChecks.VerifyRoundTrip(BlackFlowSpireMapGenerator.Generate("BlackFlowSpire-CHECK"));
                return new(true, "Native ActMap JSON round-trip passed; bidirectional edges and special-node identity preserved.");
            }
            BlackFlowSpireMap map;
            string? bossTitle = null;
            var step = args.Length > 0 && args[0].Equals("step", StringComparison.OrdinalIgnoreCase);
            if (step || (args.Length > 0 && args[0].Equals("run", StringComparison.OrdinalIgnoreCase)))
            {
                var manager = RunManager.Instance;
                var runState = manager.DebugOnlyGetState();
                if (args.Length != (step ? 2 : 1) || !manager.IsInProgress || runState == null)
                    return new(false, $"Use {CmdName} run or {CmdName} step <neighborNodeId> during an active single-player run.");
                if (!manager.IsSingleplayerOrFakeMultiplayer)
                    return new(false, "Run snapshot generation currently supports single-player only.");
                if (step && BlackFlowSpireGenerationService.IsEnabled(runState))
                    return new(false, "Use the forest map buttons to move during a BlackFlowSpire run; preview steps cannot change live exploration progress.");
                map = BlackFlowSpireGenerationService.GetOrGenerate(runState);
                if (step)
                {
                    BlackFlowSpireGenerationService.UpdatePosition(runState, int.Parse(args[1], CultureInfo.InvariantCulture));
                    map = BlackFlowSpireGenerationService.GetOrGenerate(runState);
                }
                bossTitle = runState.Act.BossEncounter.Title.GetFormattedText();
            }
            else
            {
                var act = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 0;
                map = BlackFlowSpireMapGenerator.Generate(args.Length > 0 ? args[0] : "BlackFlowSpire-DEMO", act);
            }
            var directory = ProjectSettings.GlobalizePath("user://BlackFlowSpire/previews");
            Directory.CreateDirectory(directory);
            var prefix = Path.Combine(directory, $"act-{map.ActIndex}");
            File.WriteAllText(prefix + ".json", BlackFlowSpireMapJson.Serialize(map), Encoding.UTF8);
            File.WriteAllText(prefix + "-fog.svg", BlackFlowSpireSvgRenderer.Render(map, bossTitle: bossTitle), Encoding.UTF8);
            File.WriteAllText(prefix + "-revealed.svg", BlackFlowSpireSvgRenderer.Render(map, true, bossTitle), Encoding.UTF8);
            var steps = map.FindShortestPath(map.StartNodeId, map.BossNodeId).Count - 1;
            return new(true, $"Generated {map.Nodes.Count} nodes. Forest position: {map.PositionNodeId}; reveal radius: {map.Options.RevealRadius} road steps. Exit: {steps} steps / {steps * map.Options.MoveCost} AP. Exported to {directory}. Preview state only; original map is unchanged.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or FormatException or OverflowException)
        {
            return new(false, "BlackFlowSpire generation failed: " + error.Message);
        }
    }
}
