using Godot;

namespace BlackFlowSpire.Ui;

// Shared with the editable scenes; fonts are bundled in the Mod's resource pack.
internal static class ForestFonts
{
    private static Font? _body;
    private static Font? _title;

    public static Font Body => Load(ref _body, "Body");
    public static Font Title => Load(ref _title, "Title");

    private static Font Load(ref Font? cached, string role)
    {
        if (cached == null || !GodotObject.IsInstanceValid(cached))
            cached = ResourceLoader.Load<Font>($"res://BlackFlowSpire/Fonts/{role}.tres")
                ?? throw new InvalidOperationException("BlackFlow font unavailable: " + role);
        return cached;
    }
}
