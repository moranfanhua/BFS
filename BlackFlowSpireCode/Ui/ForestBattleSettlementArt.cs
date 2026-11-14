using BlackFlowSpire.Combat;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace BlackFlowSpire.Ui;

internal static class ForestBattleSettlementArt
{
    internal static readonly Vector2 DesignSize = new(1422.2222f, 800);
    internal static readonly Color Hope = new("ffda12");
    private static readonly Color Muted = new("8c9498");

    internal static string Text(string key, params (string Name, object Value)[] variables) =>
        ForestText.Get("UI.BATTLE_SETTLEMENT." + key, variables);

    internal static Texture2D Texture(string name) => ForestMapAssets.Texture(
        $"res://BlackFlowSpire/Images/Ui/BattleSettlement/{name}.png");

    internal static TextureRect AddImage(Control parent, string name, float x, float y, float width, float height,
        bool keepAspect = false) => AddImage(parent, Texture(name), x, y, width, height, keepAspect);

    internal static TextureRect AddImage(Control parent, Texture2D texture, float x, float y, float width, float height,
        bool keepAspect = false)
    {
        var image = ForestMapAssets.Image(texture, new(width, height));
        if (!keepAspect) image.StretchMode = TextureRect.StretchModeEnum.Scale;
        image.Position = new(x, y);
        parent.AddChild(image);
        return image;
    }

    internal static Label Label(Control parent, string text, float x, float y, float width, float height, int fontSize,
        Color? color = null, HorizontalAlignment alignment = HorizontalAlignment.Left, Font? font = null)
    {
        var label = ForestMapAssets.Label(text, fontSize, color ?? Colors.White);
        if (font != null) label.AddThemeFontOverride("font", font);
        label.Position = new(x, y);
        label.Size = new(width, height);
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        parent.AddChild(label);
        return label;
    }

    internal static NinePatchRect AddSlice(Control parent, string name, float x, float y, float width, float height, int cap, Color tint)
    {
        var panel = new NinePatchRect
        {
            Texture = Texture(name), Position = new(x, y), Size = new(width, height),
            PatchMarginLeft = cap, PatchMarginRight = cap, Modulate = tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(panel);
        return panel;
    }

    internal static ColorRect Rect(Control parent, Color color, float x, float y, float width, float height)
    {
        var rect = new ColorRect { Color = color, Position = new(x, y), Size = new(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(rect);
        return rect;
    }

    internal static void BuildResult(Control canvas, Texture2D background, CharacterModel? character, string title, string kind, bool chinese,
        (int Current, int Maximum, int Shield) life, ForestCommandLevel command, ForestHope hope,
        ForestCombatHealthRuntime.Settlement? result, bool animate)
    {
        AddImage(canvas, background, 0, 0, DesignSize.X, DesignSize.Y);
        var portrait = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(portrait);
        ForestBattlePortrait.Add(portrait, character);
        Shade(canvas, new(425, 0), new(DesignSize.X - 425, 800), Vector2.Zero, Vector2.Right,
            [0, 0.23f, 1], [new(0, 0, 0, 0), new(0, 0, 0, 0.58f), new(0, 0, 0, 0.64f)]);
        Shade(canvas, new(0, 344), new(DesignSize.X, 456), Vector2.Zero, Vector2.Down,
            [0, 0.46f, 1], [new(0, 0, 0, 0), new(0, 0, 0, 0.54f), Colors.Black]);

        if (result is { Hope: > 0 }) AddLevelBurst(canvas, animate);

        var copy = new Control { Position = Vector2.Zero, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(copy);
        AddImage(copy, "stage", 17, 488, 259, 91);
        Label(copy, kind, 83, 489, 491, 47, 25);
        Label(copy, title, 83, 530, 493, 35, 27);
        if (chinese) AddImage(copy, "victory", 28, 595, 288, 72);
        else Label(copy, Text("victory"), 28, 586, 517, 90, 68, font: ForestFonts.Title);

        var numbers = ResourceLoader.Load<Font>("res://BlackFlowSpire/Fonts/Numbers.tres");
        var stats = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(stats);
        AddImage(stats, "life", 648, 174, 217, 97);
        Label(stats, Text("life"), 696, 174, 164, 30, 18, alignment: HorizontalAlignment.Center);
        var lifeValue = Label(stats, life.Current.ToString(), 704, 207, 73, 63, 50, new("53b9e4"), HorizontalAlignment.Right, numbers);
        Label(stats, $"/{life.Maximum}", 784, 218, 74, 53, 30, font: numbers);
        FitNumber(lifeValue, 50, 73);

        AddImage(stats, "shield", 873, 174, 113, 97);
        AddImage(stats, "shield-icon", 885, 181, 14, 17, true);
        Label(stats, Text("shield"), 909, 174, 74, 30, 18);
        Label(stats, life.Shield.ToString(), 885, 204, 90, 63, 48, Muted, HorizontalAlignment.Center, numbers);

        if (result is { } change)
        {
            AddImage(stats, change.Damage == 0 ? "life-intact" : "life-loss", 995, 174, 242, 97).Modulate = new(1, 1, 1, 0.65f);
            Label(stats, Text("this_battle"), 1014, 174, 150, 30, 18, Muted);
            Label(stats, change.Damage == 0 ? "0" : $"−{change.Damage}", 1004, 204, 102, 63, 40, Muted,
                HorizontalAlignment.Center, numbers);
            if (change.Damage == 0)
            {
                AddImage(stats, "perfect", 1132, 142, 181, 158, true);
                Label(stats, "1", 1202, 239, 42, 29, 21, alignment: HorizontalAlignment.Center, font: numbers);
            }
        }

        AddImage(stats, "command", 639, 290, 225, 111);
        Label(stats, Text("command"), 729, 299, 129, 30, 18);
        Label(stats, command.Level.ToString(), 616, 307, 75, 75, 57, alignment: HorizontalAlignment.Center, font: numbers);
        Label(stats, command.ExperienceToNext > 0 ? $"{command.Experience}/{command.ExperienceToNext}" : Text("level_max"),
            727, 334, 130, 56, command.ExperienceToNext > 0 ? 37 : 24, font: numbers);

        if (result is { } earned)
        {
            AddImage(stats, "experience", 873, 299, 242, 97).Modulate = new(1, 1, 1, 0.65f);
            Label(stats, Text("this_battle"), 896, 299, 154, 30, 18, Muted);
            Label(stats, $"+{earned.Experience}", 886, 327, 117, 68, 39, Muted, HorizontalAlignment.Center, numbers);
            if (earned.Hope > 0)
            {
                var pill = new Panel
                {
                    Position = new(1094, 338), Size = new(187, 25), MouseFilter = Control.MouseFilterEnum.Ignore
                };
                var skin = new StyleBoxFlat { BgColor = Hope };
                skin.SetCornerRadiusAll(12);
                pill.AddThemeStyleboxOverride("panel", skin);
                stats.AddChild(pill);
                AddImage(pill, "hope-bonus", 12, -1, 27, 27, true);
                Label(pill, Text("hope_bonus", ("Amount", earned.Hope)), 40, 0, 145, 25, 17, new("242017"), HorizontalAlignment.Center);
            }
        }

        const float barLeft = 704, barWidth = 477;
        var fill = hope.Maximum == 0 ? 0 : barWidth * hope.Balance / hope.Maximum;
        AddImage(stats, "hope", 648, 445, 480, 98);
        Rect(stats, new(0.19f, 0.11f, 0, 0.83f), barLeft, 475, barWidth, 68);
        Rect(stats, new("f0cb01"), barLeft, 475, fill, 68);
        Label(stats, Text("hope"), 727, 443, 340, 32, 18, Hope);
        var balance = Label(stats, $"◂{hope.Balance}", Math.Min(barLeft + fill + 8, 1100), 480, 91, 58, 34,
            fill > barWidth - 85 ? new Color("2b2000") : Hope, font: numbers);
        FitNumber(balance, 34, 82);
        Label(stats, $"◂{hope.Maximum}", 1190, 480, 111, 58, 34, Muted, font: numbers);

        if (!animate) return;
        copy.Modulate = Colors.Transparent;
        stats.Modulate = Colors.Transparent;
        var entry = canvas.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        entry.TweenProperty(portrait, "position:x", 0, 0.68).From(52f);
        entry.TweenProperty(copy, "position:x", 0, 0.5).From(45f);
        entry.TweenProperty(copy, "modulate", Colors.White, 0.38);
        entry.TweenProperty(stats, "modulate", Colors.White, 0.47).SetDelay(0.16);
    }

    private static void FitNumber(Label label, int fontSize, float availableWidth)
    {
        var textWidth = label.GetThemeFont("font").GetStringSize(label.Text, fontSize: fontSize).X;
        if (textWidth > availableWidth)
            label.AddThemeFontSizeOverride("font_size", Math.Max(1, (int)(fontSize * availableWidth / textWidth)));
    }

    private static void Shade(Control parent, Vector2 position, Vector2 size, Vector2 from, Vector2 to, float[] offsets, Color[] colors)
    {
        var texture = new GradientTexture2D
        {
            FillFrom = from, FillTo = to, Width = 512, Height = 512,
            Gradient = new Gradient { Offsets = offsets, Colors = colors }
        };
        AddImage(parent, texture, position.X, position.Y, size.X, size.Y);
    }

    private static void AddLevelBurst(Control canvas, bool animate)
    {
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var smoke = AddImage(canvas, "smoke", 429.5f, 120, 449, 453);
        smoke.PivotOffset = smoke.Size / 2;
        smoke.RotationDegrees = 146.5f;
        smoke.Material = additive;
        smoke.Modulate = new(1, 1, 1, 0.8f);
        foreach (var (x, y, size, opacity) in new (float, float, float, float)[]
                 { (734, 346, 23, 0.9f), (710, 367, 16, 0.72f), (654, 287, 18, 0.48f), (876, 349, 12, 0.6f), (892, 379, 11, 0.55f) })
        {
            var star = AddImage(canvas, "star", x - size / 2, y - size / 2, size, size);
            star.Material = additive;
            star.Modulate = new(1, 1, 1, opacity);
        }
        if (animate) smoke.CreateTween().TweenProperty(smoke, "modulate:a", 0.8f, 0.58).From(0.15f);
    }
}
