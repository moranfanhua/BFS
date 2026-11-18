using System.Text.Json;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Generation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace BlackFlowSpire.Combat;

internal sealed class BlackFlowSpireCollectibleReward(Player player, BlackFlowSpireCollectible item) : ModCustomReward(player)
{
    private static ModRewardDefinition _definition = null!;

    private string CollectibleId => item.Id;
    public override RewardType ModRewardType => _definition.RewardType;
    public override int RewardsSetIndex => 3;
    public override LocString Description => BlackFlowSpireLocalization.CreateLocString(item.TextKey + ".name");
    protected override string RewardIconPath => item.IconPath;
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [new HoverTip(Description, BlackFlowSpireLocalization.CreateLocString(item.TextKey + ".desc"))];

    internal static void Register() => _definition = ModRewardRegistry.Register(Entry.ModId + ":Collectible",
        (_, owner, json) => new BlackFlowSpireCollectibleReward(owner,
            BlackFlowSpireCollectibleCatalog.Find(JsonSerializer.Deserialize<string>(json ?? "null") ?? "")
            ?? throw new InvalidDataException("Missing forest collectible reward.")));

    public override string ToModRewardJson() => JsonSerializer.Serialize(item.Id);
    public override void MarkContentAsSeen() { }

    protected override async Task<bool> OnSelect()
    {
        var run = (RunState)Player.RunState;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(run);
        BlackFlowSpireGenerationService.GrantCollectible(run, map, item);
        await BlackFlowSpireCollectibleService.SettleGold(run, map, replicated: BlackFlowSpireTeam.IsOnline);
        BlackFlowSpireTeam.Publish(run);
        return true;
    }

    internal static void ReplaceRelics(RunState run, Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (!rewards.Any(reward => reward is RelicReward)) return;
        // Combat rewards pay into the team inventory once, through the exploration leader's reward set.
        if (room is CombatRoom && BlackFlowSpireTeam.IsOnline && player != BlackFlowSpireTeam.Owner(run))
        {
            rewards.RemoveAll(reward => reward is RelicReward);
            return;
        }
        var map = BlackFlowSpireGenerationService.GetOrGenerate(run);
        var progress = BlackFlowSpireGenerationService.GetProgress(run);
        var inventory = BlackFlowSpireGenerationService.GetEngine(run);
        var excluded = inventory.Collectibles.OwnedIds
            .Concat(inventory.PendingConceptRewards.SelectMany(reward => reward.CollectibleIds))
            .Concat(rewards.OfType<BlackFlowSpireCollectibleReward>().Select(reward => reward.CollectibleId))
            .ToHashSet(StringComparer.Ordinal);
        var random = new StableRandom(map.Seed, map.ActIndex, $"reward-collectibles/{progress.StepsTaken}/{player.NetId}");
        for (var index = 0; index < rewards.Count; index++)
        {
            if (rewards[index] is not RelicReward) continue;
            if (BlackFlowSpireCollectibleRules.Draw(random, excluded, rare: false) is not { } collectible)
            {
                rewards.RemoveAt(index--);
                continue;
            }
            excluded.Add(collectible.Id);
            rewards[index] = new BlackFlowSpireCollectibleReward(player, collectible);
        }
    }
}
