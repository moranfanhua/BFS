using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using static BlackFlowSpire.Ui.BlackFlowSpireBattleSettlementArt;

namespace BlackFlowSpire.Ui;

internal sealed partial class BlackFlowSpireBattleSettlementScreen
{
    private Button BuildChoiceCard(Control parent, Vector2 position, NRewardButton candidate, NLinkedRewardSet? group)
    {
        var reward = candidate.Reward!;
        var part = reward as BlackFlowSpirePartReward;
        var entry = group != null;
        var card = new Control { Position = position, Size = new(CardStride, 528), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(card);
        AddImage(card, "card-frame", -9, 5, 272, 588);
        AddImage(card, part != null ? "scrap-underlay" : "relic-underlay", -11, -7, 298, 621);
        var tint = entry ? new Color(part != null ? "b8b8b8" : "b4b4b4") : new Color(part != null ? "32abff" : "21ceb2");
        var highTitle = entry ? part == null : part != null;
        var titleGlow = AddImage(card, "card-title", -3.5f, highTitle ? 230.5f : 234.5f, 272, 47);
        titleGlow.Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Ui/BlackFlowSpire_reward_title.gdshader") };
        titleGlow.Modulate = entry ? new Color("b4b4b459") : new(tint, 0.35f);
        Rect(card, new(1, 1, 1, 0.204f), 27.5f, 283.3f, 210, 2);
        string title;
        string description;
        if (entry)
        {
            if (part != null) AddImage(card, "choice-scrap", 15.6f, 68.4f, 225, 176);
            else AddImage(card, "choice-relic", 41, 66.9f, 182, 178);
            title = Text(part != null ? "part_choice" : "collectible_choice");
            description = Text(part != null ? "part_choice_desc" : "collectible_choice_desc");
        }
        else
        {
            title = reward.Description.GetFormattedText();
            if (part != null) AddChoicePart(card, part);
            else if (RewardTexture(candidate) is { } icon) AddImage(card, icon, 42.5f, 33.4f, 180, 180, true);
            description = part?.EffectText ?? ((BlackFlowSpireCollectibleReward)reward).EffectText;
        }
        var titleSize = entry && part == null ? 23 : 24;
        var name = Label(card, title, 23, 237.5f, 219, 37, titleSize, new("dddddd"), HorizontalAlignment.Center);
        FitNumber(name, titleSize, 219);
        var height = entry && part == null ? 116 : !entry && part != null ? 80 : 90.2f;
        var top = entry && part == null ? 295 : !entry && part != null ? 294.7f : 287.7f;
        var detail = new RichTextLabel
        {
            Position = new(18.5f, top), Size = new(228, height), BbcodeEnabled = true, Text = description,
            HorizontalAlignment = HorizontalAlignment.Center, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore
        };
        detail.AddThemeFontOverride("normal_font", BlackFlowSpireMapAssets.Font);
        detail.AddThemeColorOverride("default_color", new("919191"));
        card.AddChild(detail);
        for (var size = 18; size >= 1; size--)
        {
            detail.AddThemeFontSizeOverride("normal_font_size", size);
            if (detail.GetContentHeight() <= height) break;
        }
        var buttonY = entry && part == null ? 407 : !entry && part != null ? 405.8f : 404.9f;
        return ActionButton(card, Text(!entry && part != null ? "claim" : "choose_reward"), 32, buttonY, 201, 76,
            () => { if (group != null) OpenChoice(group); else Claim(candidate); }, tint);
    }

    private static void AddChoicePart(Control card, BlackFlowSpirePartReward part)
    {
        Texture2D ShopImage(string name) => BlackFlowSpireMapAssets.Texture($"res://BlackFlowSpire/Images/Shop/{name}.png");
        var icon = new Control { Position = new(132.5f, 134), Scale = Vector2.One * (1.4f * 0.8f), MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(icon);
        AddImage(icon, ShopImage($"part-{part.Category}-ring"), -124.4f, -124.4f, 248.8f, 248.8f, true);
        AddImage(icon, BlackFlowSpireMapAssets.Texture(part.ArtPath), -60.7f, -60.7f, 121.4f, 121.4f, true);
        Vector2[] stars = [new(41.16f, 70.56f), new(55.58f, 59.64f), new(61.6f, 37.66f), new(59.36f, 13.72f), new(56.7f, -11.48f)];
        for (var index = 0; index < Math.Min(stars.Length, part.InitialUses); index++)
            AddImage(icon, ShopImage("part-use"), stars[index].X + 0.1f, -stars[index].Y - 19.7f, 28, 30.8f);
        AddImage(card, ShopImage($"part-{part.Category}-type"), 2, 12.8f, 50, 50, true);
        Label(card, BlackFlowSpireText.Get("UI.SECRET_SHOP.category_" + part.Category), 48, 26.4f, 210, 25, 18, new("cccccc"));
        AddImage(card, ShopImage("Secret/scrap_price_bg"), 62, 200.1f, 141, 32);
        Label(card, part.Appraisal.ToString(), 109.9f, 199, 78.1f, 30, 21, alignment: HorizontalAlignment.Right,
            font: ResourceLoader.Load<Font>("res://BlackFlowSpire/Fonts/NumbersNormal.tres"));
    }
}
