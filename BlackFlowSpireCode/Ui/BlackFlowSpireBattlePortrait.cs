using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireBattlePortrait
{
    private const float PortraitHeight = 655;
    private static readonly Vector2 PortraitAnchor = new(300, 788);

    internal static void Add(Control parent, CharacterModel? character)
    {
        if (character == null || !ResourceLoader.Exists(character.CharacterSelectBg)) return;
        var selection = ResourceLoader.Load<PackedScene>(character.CharacterSelectBg)?.Instantiate();
        if (selection == null) return;

        var body = selection.GetNodeOrNull<Node2D>("SpineSprite");
        if (body == null || body.GetClass() != MegaSprite.spineClassName)
        {
            selection.Free();
            return;
        }

        // Keep the native idle player and slot effects. Leave the selection backdrop and input nodes behind.
        selection.RemoveChild(body);
        selection.Free();
        body.Name = "CharacterPortrait";
        body.Position = Vector2.Zero;
        body.Scale = Vector2.One;
        body.Visible = false;
        parent.AddChild(body);
        var sprite = new MegaSprite(body);
        body.RunWhenSpineReady(sprite, _ => FramePortrait(body, sprite, character));
    }

    private static void FramePortrait(Node2D body, MegaSprite sprite, CharacterModel character)
    {
        if (sprite.GetSkeleton() is not { } skeleton) return;

        // Some selection backgrounds are attachments on the character's skeleton.
        // Hide them after the animation applies its slot keys, before Spine renders the meshes.
        var (focalX, backdropSlots) = character switch
        {
            Ironclad => (0.58f, new[] { "bg", "fire" }),
            Silent => (0.76f, Array.Empty<string>()),
            Regent => (0.53f, new[] { "Layer 5", "swirls", "stars_a", "stars_b", "stars_c", "stars_d", "stars_e", "stars_f",
                "stars_g", "stars_h", "spheric guardian", "snecko", "amogus", "sentry", "shapes", "deca", "cultist" }),
            Necrobinder => (0.63f, Array.Empty<string>()),
            Defect => (0.66f, new[] { "backgroundbottom", "backgroundtop", "shine", "shine2", "shine3", "shine4", "shine5",
                "shine6", "shine7", "shine8", "shine9", "shine10", "shine11", "shine12" }),
            Fool => (0.68f, Array.Empty<string>()),
            _ => (0.5f, Array.Empty<string>())
        };

        var slots = backdropSlots.Select(skeleton.FindSlot).OfType<MegaSlot>().Select(slot => slot.BoundObject).ToArray();
        if (slots.Length > 0)
            sprite.ConnectBeforeWorldTransformsChange(Callable.From<GodotObject>(_ => HideBackdrop(slots)));
        HideBackdrop(slots);

        var bounds = skeleton.GetBounds();
        if (bounds.Size.Y <= 0 || !bounds.Size.IsFinite()) return;
        var scale = PortraitHeight / bounds.Size.Y;
        var anchor = new Vector2(bounds.Position.X + bounds.Size.X * focalX, bounds.End.Y);
        body.Scale = Vector2.One * scale;
        body.Position = PortraitAnchor - anchor * scale;
        body.Show();
    }

    private static void HideBackdrop(GodotObject[] slots)
    {
        foreach (var slot in slots)
            slot.Call("set_attachment", default(Variant));
    }
}
