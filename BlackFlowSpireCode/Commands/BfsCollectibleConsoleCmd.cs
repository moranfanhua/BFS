using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace BlackFlowSpire.Commands;

public sealed class BfsCollectibleConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "bfs_collectible";
    public override string Args => "list | give <original-id|archive-number>";
    public override string Description => "List or grant independent BlackFlow collectibles in an active forest run.";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!ForestRuntime.CanContinueRun || ForestRuntime.State is not { } run)
            return new(false, "No active forest run.");
        var inventory = ForestCollectibleService.GetInventory(run);
        if (args is ["list"])
        {
            var owned = ForestCollectibleCatalog.All.Where(item => inventory.OwnedIds.Contains(item.Id));
            return new(true, $"Owned: {inventory.OwnedIds.Count}\n" +
                string.Join("\n", owned.Select(item => $"{item.Number:000} {item.Title} ({item.Id})")));
        }
        if (args is not ["give", var key]) return new(false, "Usage: bfs_collectible " + Args);
        var item = int.TryParse(key, out var number) ? ForestCollectibleCatalog.Find(number) : ForestCollectibleCatalog.Find(key);
        if (item == null || !ForestCollectibleRules.CanAcquire(item)) return new(false, "Collectible effect is unavailable.");
        if (inventory.Has(item.Number)) return new(false, "Collectible already owned.");
        if (!ForestTeam.CanManage(run)) return new(false, "Only the exploration leader can grant collectibles.");
        TaskHelper.RunSafely(ForestCollectibleService.Grant(run, item));
        return new(true, "Collectible grant queued.");
    }
}
