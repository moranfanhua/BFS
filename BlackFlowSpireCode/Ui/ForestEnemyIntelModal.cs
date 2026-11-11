using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace BlackFlowSpire.Ui;

// Uses the game's modal, close button and original bestiary/monster visuals.
// This screen reads the saved formation and never generates an encounter.
internal partial class ForestEnemyIntelModal : Control, IScreenContext
{
    private readonly ForestCombatSetup _setup;
    private NVerticalPopup? _popup;
    public Control? DefaultFocusedControl => _popup?.NoButton;

    private ForestEnemyIntelModal(ForestCombatSetup setup) => _setup = setup;

    public static void Open(ForestCombatSetup setup)
    {
        if (NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new ForestEnemyIntelModal(setup) { Name = "ForestEnemyIntelligence" });
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _popup = ResourceLoader.Load<PackedScene>("res://scenes/ui/vertical_popup.tscn").Instantiate<NVerticalPopup>();
        AddChild(_popup);
        _popup.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _popup.OffsetLeft = -490;
        _popup.OffsetRight = 490;
        _popup.OffsetTop = -370;
        _popup.OffsetBottom = 370;
        _popup.Set("stretch_mode", (int)TextureRect.StretchModeEnum.Scale);
        _popup.SetText(ForestText.Get("UI.ENEMY_INTEL.title"), "");
        _popup.GetNode<Control>("Description").Hide();
        _popup.YesButton.Hide();
        _popup.InitNoButton(new LocString("main_menu_ui", "GENERIC_POPUP.cancel"), _ => { });
        _popup.NoButton.SetText(ForestText.Get("UI.CLOSE"));
        _popup.NoButton.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        _popup.NoButton.Position = new Vector2(400, 625);
        _popup.NoButton.Size = new Vector2(180, 72);

        var summary = AddLabel(ForestText.Get("UI.ENEMY_INTEL.summary", ("Encounter", ModelDb.GetById<EncounterModel>(ModelId.Deserialize(_setup.EncounterId)).Title.GetFormattedText()), ("Count", _setup.Enemies.Count)),
            new(65, 127), new(850, 40), 24, true);
        var list = new ScrollContainer { Position = new(65, 190), Size = new(290, 405),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _popup.AddChild(list);
        var entries = new VBoxContainer { CustomMinimumSize = new(270, 0) };
        entries.AddThemeConstantOverride("separation", 14);
        list.AddChild(entries);
        var preview = new Control { Position = new(385, 195), Size = new(510, 340), MouseFilter = MouseFilterEnum.Ignore };
        _popup.AddChild(preview);
        var title = AddLabel("", new(380, 550), new(520, 44), 28, true);
        var groups = _setup.Enemies.GroupBy(enemy => enemy.MonsterId).ToArray();
        string? selectedMonsterId = null;
        var rows = new List<(Button Button, MonsterModel Monster, int Count)>();
        foreach (var group in groups)
        {
            var monster = ModelDb.GetById<MonsterModel>(ModelId.Deserialize(group.Key));
            var button = new Button { Text = ForestText.Get("UI.ENEMY_INTEL.row", ("Name", monster.Title.GetFormattedText()), ("Count", group.Count())), CustomMinimumSize = new(270, 68) };
            rows.Add((button, monster, group.Count()));
            button.AddThemeFontOverride("font", ForestMapAssets.Font);
            button.AddThemeFontSizeOverride("font_size", 23);
            var style = new StyleBoxFlat { BgColor = new Color("172e38"), BorderColor = new Color("759caa"),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
            button.AddThemeStyleboxOverride("normal", style);
            var highlighted = (StyleBoxFlat)style.Duplicate();
            highlighted.BgColor = new Color("335463");
            button.AddThemeStyleboxOverride("hover", highlighted);
            button.AddThemeStyleboxOverride("focus", highlighted);
            button.AddThemeColorOverride("font_color", new Color("f5e5be"));
            entries.AddChild(button);
            void Select()
            {
                foreach (var child in preview.GetChildren())
                {
                    preview.RemoveChild(child);
                    child.QueueFree();
                }
                selectedMonsterId = group.Key;
                title.Text = ForestText.Get("UI.ENEMY_INTEL.row", ("Name", monster.Title.GetFormattedText()), ("Count", group.Count()));
                var visuals = monster.CreateVisuals();
                preview.AddChild(visuals);
                // Fit using the same authored bounds used by the native bestiary.
                var bounds = visuals.Bounds;
                var center = visuals.ToLocal(bounds.GlobalPosition) + bounds.Size / 2;
                var scale = Math.Min(1.6f, Math.Min(470 / Math.Max(1, bounds.Size.X), 310 / Math.Max(1, bounds.Size.Y)));
                visuals.Scale = Vector2.One * scale;
                visuals.Position = preview.Size / 2 - center * scale;
                foreach (var control in visuals.FindChildren("*", "Control", true, false).OfType<Control>())
                    control.MouseFilter = MouseFilterEnum.Ignore;
            }
            button.Pressed += Select;
            button.FocusEntered += Select;
            if (group == groups[0]) Select();
        }
        ForestLocalization.RefreshWithLocale(this, () =>
        {
            _popup.SetText(ForestText.Get("UI.ENEMY_INTEL.title"), "");
            _popup.NoButton.SetText(ForestText.Get("UI.CLOSE"));
            summary.Text = ForestText.Get("UI.ENEMY_INTEL.summary", ("Encounter", ModelDb.GetById<EncounterModel>(ModelId.Deserialize(_setup.EncounterId)).Title.GetFormattedText()), ("Count", _setup.Enemies.Count));
            foreach (var row in rows)
            {
                row.Button.Text = ForestText.Get("UI.ENEMY_INTEL.row", ("Name", row.Monster.Title.GetFormattedText()), ("Count", row.Count));
                if (row.Monster.Id.ToString() == selectedMonsterId) title.Text = row.Button.Text;
            }
        });
    }

    private Label AddLabel(string text, Vector2 position, Vector2 size, int fontSize, bool centered = false)
    {
        var label = ForestMapAssets.Label(text, fontSize, new Color("f5e5be"));
        label.Position = position;
        label.Size = size;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (centered) label.HorizontalAlignment = HorizontalAlignment.Center;
        _popup!.AddChild(label);
        return label;
    }
}
