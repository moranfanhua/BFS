using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Persistence;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace BlackFlowSpire.Commands;

public sealed class BlackFlowSpirePartConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => Entry.ModId.ToLowerInvariant() + "_part";
    public override string Args => "list | give <kind> | give-natural <kind> | give-concept <kind> | load <id|walk> | discard <id> | sell <id>";
    public override string Description => "Manage parts in an active forest run; give is a development acquisition entry.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun) return new(false, "No active forest run.");
        try
        {
            var engine = BlackFlowSpireGenerationService.GetEngine(BlackFlowSpireRuntime.State!);
            if (args.Length == 1 && args[0] == "list")
                return new(true, "Processed: " + string.Join(", ", Enum.GetNames<BlackFlowSpireProcessedPartKind>())
                    + "\nNatural: " + string.Join(", ", Enum.GetNames<BlackFlowSpireNaturalPartKind>()) + "\n"
                    + "Concepts: " + string.Join(", ", Enum.GetNames<BlackFlowSpireConceptPartKind>()) + "\n"
                    + string.Join("\n", engine.Parts.Select(part => $"{part.Id}: {part.Kind} ({part.RemainingUses}){(part.Id == engine.LoadedPartId ? " *" : "")}"))
                    + "\n" + string.Join("\n", engine.NaturalParts.Select(part => $"{part.Id}: {part.Kind} (appraisal {part.Appraisal})"))
                    + "\n" + string.Join("\n", engine.ConceptParts.Select(part => $"{part.Id}: {part.Kind} ({part.RemainingActivations?.ToString() ?? "unlimited"})")));
            if (args.Length == 2 && args[0] == "give" && Enum.TryParse<BlackFlowSpireProcessedPartKind>(args[1], true, out var kind)
                && Enum.IsDefined(kind))
                TaskHelper.RunSafely(BlackFlowSpirePartService.Grant(kind));
            else if (args.Length == 2 && args[0] == "give-natural" && Enum.TryParse<BlackFlowSpireNaturalPartKind>(args[1], true, out var natural)
                && Enum.IsDefined(natural))
                TaskHelper.RunSafely(BlackFlowSpireNaturalService.Grant(natural));
            else if (args.Length == 2 && args[0] == "give-concept" && Enum.TryParse<BlackFlowSpireConceptPartKind>(args[1], true, out var concept)
                && Enum.IsDefined(concept))
                TaskHelper.RunSafely(BlackFlowSpireConceptService.Grant(concept));
            else if (args.Length == 2 && args[0] == "load")
                TaskHelper.RunSafely(BlackFlowSpirePartService.Load(args[1] == "walk" ? null : int.Parse(args[1])));
            else if (args.Length == 2 && args[0] == "discard")
            {
                var id = int.Parse(args[1]);
                TaskHelper.RunSafely(engine.NaturalParts.Any(part => part.Id == id) ? BlackFlowSpireNaturalService.Discard(id)
                    : engine.ConceptParts.Any(part => part.Id == id) ? BlackFlowSpireConceptService.Discard(id) : BlackFlowSpirePartService.Discard(id));
            }
            else if (args.Length == 2 && args[0] == "sell")
            {
                var id = int.Parse(args[1]);
                TaskHelper.RunSafely(BlackFlowSpirePartService.Sell(id));
            }
            else return new(false, $"Usage: {CmdName} {Args}");
            return new(true, "Part operation queued.");
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException or InvalidOperationException)
        {
            return new(false, error.Message);
        }
    }
}
