using BlackFlowSpire.Combat;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using static BlackFlowSpire.Ui.ForestBattleSettlementArt;

namespace BlackFlowSpire.Ui;

internal sealed partial class ForestCardPackRarityScreen(RunState run, string packKey,
    IReadOnlyList<(CardRarity? Rarity, int Cost)> choices, IReadOnlySet<CardRarity> available,
    int? openedOption, CardModel cardStyle) : Control, IOverlayScreen
{
    private const float CardWidth = 226;
    private const float CardHeight = 304;
    private const float CardSpacing = 292;
    private const string HopeIconPath = "res://BlackFlowSpire/Images/Ui/TopBar/hope-icon.png";
    private readonly TaskCompletionSource<int?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<ChoiceView> _cards = [];
    private Control _canvas = null!;
    private Control _hopeBar = null!;
    private ShaderMaterial _cardTint = null!;
    private NChoiceSelectionSkipButton _back = null!;
    private ForestHope? _shownHope;

    private sealed record ChoiceView(int Index, Button Button, MegaLabel Title, Label Cost);

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl => _cards.FirstOrDefault(card => !card.Button.Disabled) is { } card ? card.Button : _back;

    public static async Task<int?> Choose(RunState run, string packKey,
        IReadOnlyList<(CardRarity? Rarity, int Cost)> choices, IReadOnlySet<CardRarity> available,
        int? openedOption, CardModel cardStyle)
    {
        if (NOverlayStack.Instance is not { } stack) return null;
        var screen = new ForestCardPackRarityScreen(run, packKey, choices, available, openedOption, cardStyle)
        {
            Name = "ForestCardPackRarity"
        };
        stack.Push(screen);
        var result = await screen._result.Task;
        if (GodotObject.IsInstanceValid(screen) && screen.IsInsideTree() && !screen.IsQueuedForDeletion())
            stack.Remove(screen);
        return result;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _canvas = new Control { Name = "Canvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        var banner = ResourceLoader.Load<PackedScene>("res://scenes/ui/common_banner.tscn").Instantiate<NCommonBanner>();
        AddChild(banner);
        banner.AnimateIn();

        _hopeBar = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/ForestHopeBar.tscn").Instantiate<Control>();
        _hopeBar.Size = new(368, 27);
        _hopeBar.Set("fill_start", 92.5);
        _hopeBar.Set("fill_length", 172.0);
        AddChild(_hopeBar);
        var hopeTitle = Label(_hopeBar, "", 47, 0, 43, 27, 17, Hope);
        Resized += Layout;
        Layout();

        _cardTint = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Ui/forest_pack_rarity.gdshader")
        };
        var nativeCard = PreloadManager.Cache.GetScene("res://scenes/cards/card.tscn").Instantiate();
        try
        {
            var nativeTitle = nativeCard.GetNode<MegaLabel>("%TitleLabel");
            var indices = Enumerable.Range(0, choices.Count)
                .Where(index => choices[index].Rarity is not { } rarity || available.Contains(rarity)).ToArray();
            for (var slot = 0; slot < indices.Length; slot++)
            {
                var center = DesignSize.X / 2 + (slot - (indices.Length - 1) * 0.5f) * CardSpacing;
                _cards.Add(BuildCard(indices[slot], center, nativeTitle));
            }
        }
        finally
        {
            nativeCard.Free();
        }
        _back = ResourceLoader.Load<PackedScene>("res://scenes/ui/choice_selection_skip_button.tscn")
            .Instantiate<NChoiceSelectionSkipButton>();
        _back.FocusMode = FocusModeEnum.All;
        AddChild(_back);
        _back.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => _result.TrySetResult(null)));
        _back.AnimateIn();
        void RefreshText()
        {
            banner.label.SetTextAutoSize(ForestText.Get(packKey + ".title"));
            hopeTitle.Text = RarityText("hope");
            _back.GetNode<MegaLabel>("Label").SetTextAutoSize(RarityText("back"));
            RefreshOptions(ForestGenerationService.GetHopeStatus(run));
        }
        RefreshText();
        ForestLocalization.RefreshWithLocale(this, RefreshText);
    }

    private void Layout()
    {
        var factor = Math.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * factor;
        _canvas.Position = (Size - DesignSize * factor) / 2;
        _hopeBar.Scale = Vector2.One * factor;
        _hopeBar.Position = new(Size.X - (_hopeBar.Size.X + 28) * factor, 114 * factor);
    }

    private ChoiceView BuildCard(int index, float center, MegaLabel nativeTitle)
    {
        var (accent, outline) = choices[index].Rarity switch
        {
            CardRarity.Uncommon => (new Color("76c8ff"), StsColors.cardTitleOutlineUncommon),
            CardRarity.Rare => (new Color("ffd35b"), StsColors.cardTitleOutlineRare),
            _ => (Colors.White, StsColors.cardTitleOutlineCommon)
        };
        var button = new Button
        {
            Name = $"Rarity{index}", Position = new(center - CardWidth / 2, 316), Size = new(CardWidth, CardHeight),
            PivotOffset = new(CardWidth / 2, CardHeight / 2)
        };
        ForestMapAssets.ClearButtonStyle(button);
        button.AddThemeStyleboxOverride("focus", ForestTheme.Box(Colors.Transparent, accent.Lightened(0.2f), 3));
        _canvas.AddChild(button);

        void AddFrame(Texture2D texture, float x, float y, float width, float height)
        {
            var part = AddImage(button, texture, x, y, width, height);
            part.Material = _cardTint;
            part.SelfModulate = accent;
        }
        AddFrame(cardStyle.Frame, 0, 0, CardWidth, CardHeight);
        Rect(button, accent.Darkened(0.88f), 17, 49, CardWidth - 34, 147);
        AddImage(button, "draft", 47, 52, CardWidth - 94, 141, true);
        AddFrame(cardStyle.PortraitBorder, 8, 39, CardWidth - 16, 169);
        AddFrame(cardStyle.BannerTexture, -10, 12, CardWidth + 20, 57);
        var title = new MegaLabel
        {
            Name = "TitleLabel", Position = new(14, 16), Size = new(CardWidth - 28, 36),
            MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MinFontSize = 12, MaxFontSize = 24
        };
        title.AddThemeFontOverride("font", nativeTitle.GetThemeFont("font"));
        title.AddThemeColorOverride("font_color", StsColors.cream);
        title.AddThemeColorOverride("font_outline_color", outline);
        var outlineScale = (float)title.MaxFontSize / nativeTitle.MaxFontSize;
        var outlineSize = Mathf.RoundToInt(nativeTitle.GetThemeConstant("outline_size") * outlineScale);
        title.AddThemeConstantOverride("outline_size", Math.Max(2, outlineSize));
        button.AddChild(title);
        const float badgeWidth = 86, iconWidth = 38, badgeY = 215;
        var badgeX = (CardWidth - badgeWidth) / 2;
        Rect(button, new Color("242424"), badgeX, badgeY, badgeWidth, 30);
        Rect(button, new Color("7c4b00"), badgeX, badgeY, iconWidth, 30);
        AddImage(button, ForestMapAssets.Texture(HopeIconPath), badgeX + 5, badgeY + 2, 28, 26, true);
        var cost = Label(button, "", badgeX + iconWidth, badgeY, badgeWidth - iconWidth, 30, 23, Colors.White, HorizontalAlignment.Center);

        void Highlight(bool enabled)
        {
            var active = enabled && !button.Disabled;
            button.Scale = Vector2.One * (active ? 1.04f : 1);
            button.ZIndex = active ? 1 : 0;
        }
        button.MouseEntered += () => Highlight(true);
        button.MouseExited += () => Highlight(button.HasFocus());
        button.FocusEntered += () => Highlight(true);
        button.FocusExited += () => Highlight(button.IsHovered());
        button.Pressed += () =>
        {
            if (!button.Disabled && (openedOption == index || choices[index].Cost <= ForestGenerationService.GetHope(run)))
                _result.TrySetResult(index);
        };
        return new(index, button, title, cost);
    }

    private void RefreshOptions(ForestHope hope)
    {
        if (hope != _shownHope)
            _hopeBar.Call("set_hope", hope.Balance, hope.Maximum, _shownHope.HasValue);
        _shownHope = hope;
        foreach (var card in _cards)
        {
            var (rarity, cost) = choices[card.Index];
            var price = openedOption == card.Index ? 0 : cost;
            var affordable = hope.Balance >= price;
            var locked = openedOption.HasValue && openedOption != card.Index;
            card.Title.SetTextAutoSize(rarity?.ToLocString().GetFormattedText() ?? RarityText("random"));
            card.Cost.Text = price.ToString();
            card.Button.Disabled = locked || !affordable;
            card.Button.Modulate = card.Button.Disabled ? new Color(0.72f, 0.72f, 0.72f, 0.75f) : Colors.White;
        }

        var enabled = _cards.Where(card => !card.Button.Disabled).Select(card => card.Button).ToArray();
        for (var index = 0; index < enabled.Length; index++)
        {
            var button = enabled[index];
            button.FocusNeighborLeft = enabled[(index + enabled.Length - 1) % enabled.Length].GetPath();
            button.FocusNeighborRight = enabled[(index + 1) % enabled.Length].GetPath();
            button.FocusNeighborTop = button.GetPath();
            button.FocusNeighborBottom = _back.GetPath();
        }
        _back.FocusNeighborTop = DefaultFocusedControl!.GetPath();
    }

    public override void _Process(double delta)
    {
        var hope = ForestGenerationService.GetHopeStatus(run);
        if (hope != _shownHope) RefreshOptions(hope);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed(MegaInput.cancel) || input.IsActionPressed(MegaInput.back))
            _result.TrySetResult(null);
        else if (input.IsActionPressed(MegaInput.select) && GetViewport().GuiGetFocusOwner() is Button button
            && !button.Disabled && _cards.Any(card => card.Button == button))
            button.EmitSignal(BaseButton.SignalName.Pressed);
        else return;
        GetViewport().SetInputAsHandled();
    }

    private static string RarityText(string key) => ForestText.Get("UI.CARD_PACK_RARITY." + key);

    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed()
    {
        _result.TrySetResult(null);
        QueueFree();
    }
    public void AfterOverlayShown()
    {
        Show();
        _back.Enable();
    }

    public void AfterOverlayHidden()
    {
        _back.Disable();
        Hide();
    }
    public override void _ExitTree() => _result.TrySetResult(null);
}
