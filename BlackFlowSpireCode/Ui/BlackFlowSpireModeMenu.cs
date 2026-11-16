using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

public static class BlackFlowSpireModeMenu
{
    public static string Title => BlackFlowSpireText.Get("UI.MODE.title");
    public static string Description => BlackFlowSpireText.Get("UI.MODE.description");
    public const string ImagePath = "res://BlackFlowSpire/Images/MainMenu/BlackFlowSpire_mode.png";
    private const string ButtonName = "BlackFlowSpireMode";
    private static readonly string[] CardNames = ["StandardButton", "DailyButton", "CustomRunButton", ButtonName];

    public static void Add(NSingleplayerSubmenu submenu)
    {
        BlackFlowSpireRuntime.PendingNewRun = false;
        var card = submenu.GetNodeOrNull<NSubmenuButton>(ButtonName);
        if (card == null)
        {
            // A fresh card avoids copying the standard card's run-start signal.
            card = PreloadManager.Cache.GetScene("res://scenes/ui/submenu_button.tscn").Instantiate<NSubmenuButton>();
            card.Name = ButtonName;
            submenu.AddChild(card);
            var background = card.GetNode<Control>("BgPanel");
            var material = (ShaderMaterial)background.Material.Duplicate();
            background.Material = material;
            material.SetShaderParameter("h", 0.46f);
            material.SetShaderParameter("s", 0.35f);
            var icon = card.GetNode<TextureRect>("Icon");
            icon.Texture = ResourceLoader.Load<Texture2D>(ImagePath);
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            icon.ClipContents = true;
            var frame = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Transparent, BorderColor = new Color("ddbd85"),
                BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
                ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 2
            });
            icon.AddChild(frame);
            frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            card.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Start(submenu)));
            submenu.Resized += () => Layout(submenu);
            Entry.Logger.Info("[BlackFlowSpire] Added native-style mode card to the Play/singleplayer selection page.");
        }
        card.SetEnabled(!SaveManager.Instance.HasRunSave);
        BlackFlowSpireLocalization.RefreshWithLocale(submenu, () => RefreshText(submenu));
        RefreshText(submenu);
        Layout(submenu);
    }

    private static void RefreshText(NSingleplayerSubmenu submenu)
    {
        var card = submenu.GetNode<NSubmenuButton>(ButtonName);
        var title = card.GetNode<MegaLabel>("Title");
        title.AddThemeFontOverride("font", BlackFlowSpireFonts.Title);
        title.SetTextAutoSize(Title);
        var description = card.GetNode<MegaRichTextLabel>("Description");
        description.AddThemeFontOverride("normal_font", BlackFlowSpireFonts.Body);
        description.AddThemeFontOverride("bold_font", BlackFlowSpireFonts.Title);
        description.Text = Description;
    }

    private static void Layout(NSingleplayerSubmenu submenu)
    {
        var cards = CardNames.Select(name => submenu.GetNodeOrNull<NSubmenuButton>(name)).OfType<NSubmenuButton>().ToArray();
        var width = Math.Clamp((submenu.Size.X - 160) / 4 - 50, 250, 330);
        var pitch = width + 50;
        for (var index = 0; index < cards.Length; index++)
        {
            var card = cards[index];
            var center = (index - (cards.Length - 1) / 2f) * pitch;
            card.OffsetLeft = center - width / 2;
            card.OffsetRight = center + width / 2;
            var icon = card.GetNode<TextureRect>("Icon");
            icon.OffsetLeft = -(width - 46) / 2;
            icon.OffsetRight = (width - 46) / 2;
            var lockIcon = card.GetNode<TextureRect>("Lock");
            lockIcon.OffsetLeft = icon.OffsetLeft;
            lockIcon.OffsetRight = icon.OffsetRight;
            var title = card.GetNode<Control>("Title");
            title.OffsetLeft = -(width - 24) / 2;
            title.OffsetRight = (width - 24) / 2;
            card.GetNode<Control>("Description").OffsetRight = width - 25;
            card.FocusNeighborLeft = cards[Math.Max(0, index - 1)].GetPath();
            card.FocusNeighborRight = cards[Math.Min(cards.Length - 1, index + 1)].GetPath();
        }
    }

    private static void Start(NSingleplayerSubmenu submenu)
    {
        if (SaveManager.Instance.HasRunSave) return;
        Node? ancestor = submenu;
        while (ancestor != null && ancestor is not NMainMenu) ancestor = ancestor.GetParent();
        if (ancestor is not NMainMenu menu) throw new InvalidOperationException("Main menu stack is unavailable.");
        var select = menu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
        BlackFlowSpireRuntime.PendingNewRun = true;
        select.InitializeSingleplayer();
        menu.SubmenuStack.Push(select);
    }
}
