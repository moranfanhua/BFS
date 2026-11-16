using Godot;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireTheme
{
    public static Font Font => BlackFlowSpireFonts.Body;

    public static StyleBoxFlat Box(Color background, Color border, int width = 2) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8
    };

    public static void Format(Button button, Color border, int fontSize = 18)
    {
        button.AddThemeFontOverride("font", Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", new Color("f1ead8"));
        button.AddThemeColorOverride("font_disabled_color", new Color("b7c1b9"));
        button.AddThemeStyleboxOverride("normal", Box(new Color("18352b"), border));
        button.AddThemeStyleboxOverride("hover", Box(new Color("2b5141"), new Color("dfcb8c"), 3));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("3b6450"), new Color("dfcb8c"), 3));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("172820"), border.Darkened(0.35f), 1));
        button.AddThemeStyleboxOverride("focus", Box(Colors.Transparent, new Color("f0cf7f"), 3));
    }

    public static Label Label(string text, int size = 22)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("eee8d8"));
        return label;
    }
}
