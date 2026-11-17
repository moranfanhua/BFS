using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
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
    private const string ButtonScenePath = "res://scenes/ui/submenu_button.tscn";

    public static void Add(NSingleplayerSubmenu submenu)
    {
        BlackFlowSpireRuntime.PendingNewRun = false;
        NSubmenuButton? card = null;
        try
        {
            var buttons = submenu.GetNodeOrNull<HBoxContainer>("%ButtonContainer")
                ?? throw new InvalidOperationException("The singleplayer submenu is missing its ButtonContainer.");
            var dread = buttons.GetNodeOrNull<NSubmenuButton>("DreadButton");
            card = buttons.GetNodeOrNull<NSubmenuButton>(ButtonName);
            if (card != null)
            {
                card.SetEnabled(!SaveManager.Instance.HasRunSave);
                FitButtonRow(submenu, buttons, dread);
                return;
            }

            // A fresh card avoids copying the standard card's run-start signal.
            card = PreloadManager.Cache.GetScene(ButtonScenePath).Instantiate<NSubmenuButton>();
            card.Name = ButtonName;
            var portrait = card.GetNodeOrNull<NSubmenuPortrait>("%SubmenuPortrait")
                ?? throw new InvalidOperationException("The native mode card is missing SubmenuPortrait.");
            if (portrait.GetNodeOrNull<TextureRect>("Lock") == null || portrait.GetNodeOrNull<TextureRect>("Frame") == null)
                throw new InvalidOperationException("The native mode portrait is missing its Lock or Frame.");
            var title = card.GetNodeOrNull<MegaLabel>("%Title")
                ?? throw new InvalidOperationException("The native mode card is missing its Title.");
            var description = card.GetNodeOrNull<MegaRichTextLabel>("%Description")
                ?? throw new InvalidOperationException("The native mode card is missing its Description.");
            if (card.GetNodeOrNull<Control>("BgPanel")?.Material is not ShaderMaterial material)
                throw new InvalidOperationException("The native mode card is missing its background shader material.");
            var image = ResourceLoader.Load<Texture2D>(ImagePath)
                ?? throw new InvalidOperationException($"Could not load the mode portrait at {ImagePath}.");

            // The original scene gives each card its own background material.
            material.SetShaderParameter("h", 0.46f);
            portrait.Texture = image;
            if (card.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Start(submenu))) != Error.Ok)
                throw new InvalidOperationException("Could not connect the mode card's release signal.");

            buttons.AddChild(card);
            card.SetEnabled(!SaveManager.Instance.HasRunSave);
            RefreshText(title, description);
            BlackFlowSpireLocalization.RefreshWithLocale(card, () => RefreshText(title, description));

            void Fit() => FitButtonRow(submenu, buttons, dread);
            submenu.Resized += Fit;
            buttons.Resized += Fit;
            card.TreeExiting += () =>
            {
                submenu.Resized -= Fit;
                buttons.Resized -= Fit;
            };
            Fit();
            Entry.Logger.Info("[BlackFlowSpire] Added native-style mode card to the Play/singleplayer selection page.");
        }
        catch (Exception error)
        {
            if (card != null)
            {
                card.GetParent()?.RemoveChild(card);
                card.QueueFree();
            }
            Entry.Logger.Error($"[BlackFlowSpire] Could not add the mode card to the singleplayer submenu: {error}");
        }
    }

    private static void RefreshText(MegaLabel title, MegaRichTextLabel description)
    {
        title.AddThemeFontOverride("font", BlackFlowSpireFonts.Title);
        title.SetTextAutoSize(Title);
        description.AddThemeFontOverride("normal_font", BlackFlowSpireFonts.Body);
        description.AddThemeFontOverride("bold_font", BlackFlowSpireFonts.Title);
        description.Text = Description;
    }

    private static void FitButtonRow(NSingleplayerSubmenu submenu, HBoxContainer buttons, NSubmenuButton? dread)
    {
        var minimumWidth = buttons.GetCombinedMinimumSize().X;
        if (submenu.Size.X <= 0 || minimumWidth <= 0) return;

        var nativeScale = dread?.Visible == true ? 0.85f : 1f;
        // The container can grow past its anchors to fit the cards. Scale about its actual center.
        buttons.PivotOffset = buttons.Size / 2;
        buttons.Scale = Vector2.One * Math.Min(nativeScale, submenu.Size.X / minimumWidth);
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
