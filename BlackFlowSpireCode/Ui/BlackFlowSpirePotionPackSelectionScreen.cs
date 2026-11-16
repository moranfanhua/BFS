using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpirePotionPackSelectionScreen(IReadOnlyList<PotionModel> potions,
    Func<PotionModel, Task<string?>> tryClaim) : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<PotionModel?> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<NButton> _buttons = [];
    private NChoiceSelectionSkipButton _skip = null!;
    private Label _status = null!;
    private bool _claiming;
    private ulong _openedTicks;

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl => _buttons.Count > 0 ? _buttons[_buttons.Count / 2] : _skip;

    public static async Task<PotionModel?> Choose(IReadOnlyList<PotionModel> potions, Func<PotionModel, Task<string?>> tryClaim)
    {
        if (potions.Count == 0 || NOverlayStack.Instance is not { } stack) return null;
        var screen = new BlackFlowSpirePotionPackSelectionScreen(potions, tryClaim) { Name = "BlackFlowSpirePotionPackSelection" };
        stack.Push(screen);
        try
        {
            return await screen._choice.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(screen) && screen.IsInsideTree() && !screen.IsQueuedForDeletion())
                stack.Remove(screen);
        }
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var layout = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpirePotionPackSelection.tscn")
            .Instantiate<Control>();
        // These scenes are provided by the base game at runtime.
        var banner = ResourceLoader.Load<PackedScene>("res://scenes/ui/common_banner.tscn").Instantiate<NCommonBanner>();
        banner.Name = "Banner";
        layout.AddChild(banner);
        layout.MoveChild(banner, 0);
        _skip = ResourceLoader.Load<PackedScene>("res://scenes/ui/choice_selection_skip_button.tscn")
            .Instantiate<NChoiceSelectionSkipButton>();
        _skip.Name = "SkipButton";
        _skip.FocusMode = FocusModeEnum.All;
        _skip.OffsetTop = 322f;
        _skip.OffsetBottom = 395f;
        layout.AddChild(_skip);
        AddChild(layout);
        void RefreshText()
        {
            banner.label.SetTextAutoSize(BlackFlowSpireText.Get("UI.SHOP.choose_potion"));
            _skip.GetNode<MegaLabel>("Label").SetTextAutoSize(
                new LocString("gameplay_ui", "CHOOSE_CARD_SKIP_BUTTON").GetFormattedText());
        }
        RefreshText();
        BlackFlowSpireLocalization.RefreshWithLocale(this, RefreshText);
        banner.AnimateIn();
        _skip.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
        {
            if (!_claiming) _choice.TrySetResult(null);
        }));
        _skip.AnimateIn();

        var row = layout.GetNode<Control>("PotionRow");
        void FitRow() => row.Scale = Vector2.One * Mathf.Min(1f, (Size.X - 80) / (potions.Count * 340f));
        FitRow();
        Resized += FitRow;
        _status = BlackFlowSpireTheme.Label("", 22);
        _status.Position = new Vector2(-480, 205);
        _status.Size = new Vector2(960, 40);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeColorOverride("font_color", new Color("e5b980"));
        row.AddChild(_status);

        for (var index = 0; index < potions.Count; index++)
        {
            var potion = potions[index];
            SaveManager.Instance.MarkPotionAsSeen(potion);
            // Match the native card row: evenly spaced choices without a panel.
            var button = new NButton
            {
                Position = new Vector2((index - (potions.Count - 1) * 0.5f) * 340 - 140, -180),
                Size = new Vector2(280, 360), PivotOffset = new Vector2(140, 180), FocusMode = FocusModeEnum.All
            };
            row.AddChild(button);
            _buttons.Add(button);
            var image = NPotion.Create(potion)!;
            button.AddChild(image);
            image.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            image.Position = new Vector2(50, 35);
            image.Size = new Vector2(180, 180);
            image.Image.MouseFilter = MouseFilterEnum.Ignore;
            image.Outline.MouseFilter = MouseFilterEnum.Ignore;
            var title = BlackFlowSpireTheme.Label(potion.Title.GetFormattedText(), 26);
            title.Position = new Vector2(0, 255);
            title.Size = new Vector2(280, 60);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            title.AddThemeFontOverride("font", BlackFlowSpireFonts.Title);
            title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
            title.AddThemeConstantOverride("outline_size", 6);
            button.AddChild(title);
            button.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(_ =>
            {
                NHoverTipSet.Remove(button);
                NHoverTipSet.CreateAndShow(button, potion.HoverTips)?.SetAlignment(button, HoverTip.GetHoverTipAlignment(button));
                button.Scale = Vector2.One * 1.05f;
            }));
            button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(_ =>
            {
                NHoverTipSet.Remove(button);
                button.Scale = Vector2.One;
            }));
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => TaskHelper.RunSafely(Claim(potion))));
            CreateTween().TweenProperty(button, "modulate:a", 1f, 0.4).From(0f);
        }
        for (var index = 0; index < _buttons.Count; index++)
        {
            var button = _buttons[index];
            button.FocusNeighborLeft = _buttons[(index + _buttons.Count - 1) % _buttons.Count].GetPath();
            button.FocusNeighborRight = _buttons[(index + 1) % _buttons.Count].GetPath();
            button.FocusNeighborTop = button.GetPath();
            button.FocusNeighborBottom = _skip.GetPath();
        }
        _skip.FocusNeighborTop = DefaultFocusedControl!.GetPath();
    }

    private async Task Claim(PotionModel potion)
    {
        if (_claiming || _choice.Task.IsCompleted || Time.GetTicksMsec() - _openedTicks < 350) return;
        _claiming = true;
        SetChoicesEnabled(false);
        try
        {
            var error = await tryClaim(potion);
            if (!IsInsideTree() || _choice.Task.IsCompleted) return;
            if (error == null) _choice.TrySetResult(potion);
            else _status.Text = error;
        }
        finally
        {
            _claiming = false;
            if (IsInsideTree() && IsVisibleInTree() && !_choice.Task.IsCompleted) SetChoicesEnabled(true);
        }
    }

    private void SetChoicesEnabled(bool enabled)
    {
        foreach (var button in _buttons)
        {
            if (enabled) button.Enable();
            else
            {
                NHoverTipSet.Remove(button);
                button.Disable();
            }
        }
        if (enabled) _skip.Enable();
        else _skip.Disable();
    }

    public void AfterOverlayOpened()
    {
        _openedTicks = Time.GetTicksMsec();
        CreateTween().TweenProperty(this, "modulate:a", 1f, 0.2).From(0f);
    }

    public void AfterOverlayClosed()
    {
        _choice.TrySetResult(null);
        this.QueueFreeSafely();
    }

    public void AfterOverlayShown()
    {
        Show();
        if (!_claiming && !_choice.Task.IsCompleted) SetChoicesEnabled(true);
    }

    public void AfterOverlayHidden()
    {
        SetChoicesEnabled(false);
        Hide();
    }

    public override void _ExitTree()
    {
        _choice.TrySetResult(null);
        foreach (var button in _buttons)
            if (GodotObject.IsInstanceValid(button)) NHoverTipSet.Remove(button);
    }
}
