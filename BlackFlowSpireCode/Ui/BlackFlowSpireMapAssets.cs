using System.Text.Json;
using Godot;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireMapAssets
{
    private static readonly Dictionary<string, Texture2D> Textures = [];

    public static Font Font => BlackFlowSpireFonts.Body;

    public static Texture2D Texture(string path)
    {
        if (!Textures.TryGetValue(path, out var texture) || !GodotObject.IsInstanceValid(texture))
            Textures[path] = texture = ResourceLoader.Load<Texture2D>(path)
                ?? throw new InvalidOperationException("Map texture unavailable: " + path);
        return texture;
    }

    public static Texture2D Icon(string key, bool details = false)
    {
        var visual = BlackFlowSpireNodeVisuals.Get(key);
        if (visual.Id == BlackFlowSpireNodeVisuals.BATTLE_BOSS) return Ui("map/icons/map_elite");
        var name = details ? visual.FocusIcon ?? visual.Icon : visual.Icon;
        return Texture("res://BlackFlowSpire/Images/Map/Nodes/" + name + ".png");
    }

    public static Texture2D Ui(string name) => Texture("res://images/atlases/ui_atlas.sprites/" + name + ".tres");

    public static int RegionNumber(int actIndex) => BlackFlowSpireFloorPlan.Get(actIndex).SourceActIndex + 1;

    public static Texture2D Background(int actIndex) => Texture(
        $"res://BlackFlowSpire/Images/Map/Backgrounds/rogue_6_map_{RegionNumber(actIndex)}.png");

    public static Control Focus(float size, Color tint, float strength = 1)
    {
        var material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Map/BlackFlowSpire_map_focus.gdshader") };
        material.SetShaderParameter("tint", tint);
        material.SetShaderParameter("strength", strength);
        return new ColorRect
        {
            Size = Vector2.One * size, Position = Vector2.One * (80 - size) / 2,
            Material = material, MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    public static TextureRect NodeImage(string key, float size, bool details = false)
    {
        var texture = Icon(key, details);
        var visual = BlackFlowSpireNodeVisuals.Get(key);
        // The original 256px icons include generous transparent effect margins.
        // Focus-only artwork is already tightly framed; keep its natural bounds.
        var factor = details && visual.FocusIcon != null ? 1f : 2f * Math.Max(texture.GetWidth(), texture.GetHeight()) / 256f;
        var image = Image(texture, Vector2.One * size * factor);
        image.Position = (Vector2.One * size - image.Size) / 2;
        return image;
    }

    public static Texture2D Compressed(string name)
    {
        var cacheKey = "compressed:" + name;
        if (Textures.TryGetValue(cacheKey, out var cached) && GodotObject.IsInstanceValid(cached)) return cached;
        // The shipped legacy .tres files refer to an obsolete page and obsolete rectangles.
        // Read the current sheet, including trim margins, rather than hardcoding those coordinates.
        using var sheet = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://images/atlases/compressed_atlas.tpsheet"));
        foreach (var page in sheet.RootElement.GetProperty("textures").EnumerateArray())
        foreach (var sprite in page.GetProperty("sprites").EnumerateArray())
        {
            if (sprite.GetProperty("filename").GetString() != name + ".png") continue;
            Rect2 Rect(JsonElement item) => new(item.GetProperty("x").GetSingle(), item.GetProperty("y").GetSingle(),
                item.GetProperty("w").GetSingle(), item.GetProperty("h").GetSingle());
            var texture = new AtlasTexture
            {
                Atlas = Texture("res://images/atlases/" + page.GetProperty("image").GetString()),
                Region = Rect(sprite.GetProperty("region")), Margin = Rect(sprite.GetProperty("margin"))
            };
            Textures[cacheKey] = texture;
            return texture;
        }
        throw new InvalidOperationException("Map sprite unavailable: " + name);
    }

    public static TextureRect Image(Texture2D texture, Vector2 size) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = texture,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        Size = size
    };

    public static Label Label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static void ClearButtonStyle(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }
}
