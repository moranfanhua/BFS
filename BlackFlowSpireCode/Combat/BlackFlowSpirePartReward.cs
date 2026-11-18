using System.Text.Json;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Ui;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace BlackFlowSpire.Combat;

internal sealed class BlackFlowSpirePartReward(Player player, BlackFlowSpireShopPartDefinition part) : ModCustomReward(player)
{
    private static ModRewardDefinition _definition = null!;

    public override RewardType ModRewardType => _definition.RewardType;
    public override int RewardsSetIndex => 4;
    public override LocString Description => BlackFlowSpireLocalization.CreateLocString(BlackFlowSpireText.PartKey(part.Id) + ".name");
    protected override string RewardIconPath => $"res://BlackFlowSpire/Images/Parts/{part.Id}.png";
    internal string EffectText => part.Processed is { } processed ? BlackFlowSpirePartsUi.Description(processed)
        : part.Natural is { } natural ? BlackFlowSpirePartsUi.NaturalDescription(natural)
        : BlackFlowSpirePartsUi.ConceptDescription(part.Concept!.Value);
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(Description, EffectText)];

    internal static void Register() => _definition = ModRewardRegistry.Register(Entry.ModId + ":EnginePart",
        (_, owner, json) => new BlackFlowSpirePartReward(owner,
            BlackFlowSpireShopParts.Get(JsonSerializer.Deserialize<string>(json ?? "null")
                ?? throw new InvalidDataException("Missing forest part reward."))));

    public override string ToModRewardJson() => JsonSerializer.Serialize(part.Id);
    public override void MarkContentAsSeen() { }

    protected override Task<bool> OnSelect()
    {
        var run = (RunState)Player.RunState;
        BlackFlowSpireGenerationService.ModifyEngine(run, part.Grant);
        return Task.FromResult(true);
    }
}
