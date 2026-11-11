using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace BlackFlowSpire.Commands;

public sealed class BfsPartConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "bfs_part";
    public override string Args => "list | give <kind> | give-natural <kind> | give-concept <kind> | load <id|walk> | discard <id> | sell <id>";
    public override string Description => "Manage parts in an active forest run; give is a development acquisition entry.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!ForestRuntime.CanContinueRun) return new(false, "No active forest run.");
        try
        {
            var engine = ForestGenerationService.GetEngine(ForestRuntime.State!);
            if (args.Length == 1 && args[0] == "list")
                return new(true, "Processed: " + string.Join(", ", Enum.GetNames<ForestProcessedPartKind>())
                    + "\nNatural: " + string.Join(", ", Enum.GetNames<ForestNaturalPartKind>()) + "\n"
                    + "Concepts: " + string.Join(", ", Enum.GetNames<ForestConceptPartKind>()) + "\n"
                    + string.Join("\n", engine.Parts.Select(part => $"{part.Id}: {part.Kind} ({part.RemainingUses}){(part.Id == engine.LoadedPartId ? " *" : "")}"))
                    + "\n" + string.Join("\n", engine.NaturalParts.Select(part => $"{part.Id}: {part.Kind} (appraisal {part.Appraisal})"))
                    + "\n" + string.Join("\n", engine.ConceptParts.Select(part => $"{part.Id}: {part.Kind} ({part.RemainingActivations?.ToString() ?? "unlimited"})")));
            if (args.Length == 2 && args[0] == "give" && Enum.TryParse<ForestProcessedPartKind>(args[1], true, out var kind)
                && Enum.IsDefined(kind))
                TaskHelper.RunSafely(ForestPartService.Grant(kind));
            else if (args.Length == 2 && args[0] == "give-natural" && Enum.TryParse<ForestNaturalPartKind>(args[1], true, out var natural)
                && Enum.IsDefined(natural))
                TaskHelper.RunSafely(ForestNaturalService.Grant(natural));
            else if (args.Length == 2 && args[0] == "give-concept" && Enum.TryParse<ForestConceptPartKind>(args[1], true, out var concept)
                && Enum.IsDefined(concept))
                TaskHelper.RunSafely(ForestConceptService.Grant(concept));
            else if (args.Length == 2 && args[0] == "load")
                TaskHelper.RunSafely(ForestPartService.Load(args[1] == "walk" ? null : int.Parse(args[1])));
            else if (args.Length == 2 && args[0] == "discard")
            {
                var id = int.Parse(args[1]);
                TaskHelper.RunSafely(engine.NaturalParts.Any(part => part.Id == id) ? ForestNaturalService.Discard(id)
                    : engine.ConceptParts.Any(part => part.Id == id) ? ForestConceptService.Discard(id) : ForestPartService.Discard(id));
            }
            else if (args.Length == 2 && args[0] == "sell")
            {
                var id = int.Parse(args[1]);
                TaskHelper.RunSafely(ForestPartService.Sell(id));
            }
            else return new(false, "Usage: bfs_part " + Args);
            return new(true, "Part operation queued.");
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException or InvalidOperationException)
        {
            return new(false, error.Message);
        }
    }
}
