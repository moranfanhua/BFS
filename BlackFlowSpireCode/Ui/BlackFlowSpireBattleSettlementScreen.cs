using System.Globalization;
using BlackFlowSpire.Combat;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using static BlackFlowSpire.Ui.BlackFlowSpireBattleSettlementArt;

namespace BlackFlowSpire.Ui;

internal sealed partial class BlackFlowSpireBattleSettlementScreen(NRewardsScreen screen, RunState run, CombatRoom room) : Control
{
    private const string ViewName = "BlackFlowSpireBattleSettlement";
    private static readonly Color Green = new("32ff84");
    private static readonly Color Teal = new("26f4c6");
    private readonly List<RewardCard> _cards = [];
    private Control _canvas = null!;
    private Control _result = null!;
    private Control _loot = null!;
    private HBoxContainer _row = null!;
    private ScrollContainer _scroll = null!;
    private Button _continue = null!;
    private Button _leave = null!;
    private Label _leaveLabel = null!;
    private Control _confirm = null!;
    private Control _divider = null!;
    private Control _nativeList = null!;
    private Button _keepLoot = null!;
    private NProceedButton _nativeProceed = null!;
    private bool _takingReward;
    private bool _proceeding;
    private bool _ready;
    private bool _cardsChanged = true;
    private int _leaveX;

    private sealed record RewardCard(NRewardButton Native, Control Frame, Button Claim);

    internal static BlackFlowSpireBattleSettlementScreen? Find(NRewardsScreen screen) =>
        screen.GetNodeOrNull<BlackFlowSpireBattleSettlementScreen>(ViewName) is { _ready: true } view ? view : null;

    internal static bool LootIsOpen => NOverlayStack.Instance?.Peek() is NRewardsScreen rewards && Find(rewards) is { } view
        && view._loot.Visible && view.Visible;

    internal Control DefaultFocus => !_loot.Visible ? _continue : _confirm.Visible ? _keepLoot
        : _cards.FirstOrDefault(card => !card.Claim.Disabled)?.Claim ?? _leave;

    internal Control RewardList => _row;

    internal Control? CardFor(NRewardButton button) => _cards.Find(card => ReferenceEquals(card.Native, button))?.Frame;

    internal static void Attach(NRewardsScreen screen, RunState run, CombatRoom room)
    {
        var view = new BlackFlowSpireBattleSettlementScreen(screen, run, room) { Name = ViewName };
        screen.AddChild(view);
        try
        {
            view.Build();
        }
        catch (Exception error)
        {
            screen.RemoveChild(view);
            view.QueueFree();
            Entry.Logger.Error($"[BlackFlowSpire] Could not build the battle settlement: {error}");
            return;
        }

        var nativeWindow = screen.GetNode<Control>("Rewards");
        nativeWindow.Hide();
        // Keep the proceed node available to the native screen and its tutorial.
        view._nativeProceed.Modulate = Colors.Transparent;
        view._nativeProceed.MouseFilter = MouseFilterEnum.Ignore;
        view._nativeProceed.FocusMode = FocusModeEnum.None;
        var waiting = screen.GetNode<Control>("%WaitingForOtherPlayers");
        var waitingLayer = waiting;
        while (waitingLayer.GetParent() is Control parent && parent != screen) waitingLayer = parent;
        if (waitingLayer != nativeWindow && waitingLayer.GetParent() == screen)
            screen.MoveChild(view, waitingLayer.GetIndex());
        view._ready = true;
        ActiveScreenContext.Instance.Update();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Rect(this, Colors.Black, 0, 0, 0, 0).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Size = DesignSize, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        Resized += Layout;
        Layout();

        _result = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_result);
        _nativeProceed = screen.GetNode<NProceedButton>("ProceedButton");
        _nativeList = screen.GetNode<Control>("%RewardsContainer");
        BuildLoot();
        _continue = ActionButton(_canvas, "", 0, 0, DesignSize.X, DesignSize.Y, OpenLoot);
        _continue.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _continue.MouseDefaultCursorShape = CursorShape.Arrow;
        BlackFlowSpireLocalization.RefreshWithLocale(this, () => RefreshResult(animate: false));
        RefreshResult(animate: true);
        SyncCards();
        _nativeList.ChildOrderChanged += MarkCardsChanged;
    }

    private void Layout()
    {
        var factor = Math.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * factor;
        _canvas.Position = (Size - DesignSize * factor) / 2;
    }

    private void RefreshResult(bool animate)
    {
        foreach (var child in _result.GetChildren())
        {
            _result.RemoveChild(child);
            child.QueueFree();
        }
        var variant = room.RoomType switch { RoomType.Boss => "boss", RoomType.Elite => "emergency", _ => "normal" };
        var kind = BlackFlowSpireEncounterFactory.SpecialKind(room.Encounter) switch
        {
            BlackFlowSpireSpecialEncounter.Hunt => "hunt",
            BlackFlowSpireSpecialEncounter.Resident => "resident",
            _ => variant
        };
        var title = BlackFlowSpireEncounterFactory.TryGet(room.Encounter, out var setup)
            ? BlackFlowSpireText.Get(setup.Stage!.TitleKey) : room.Encounter.Title.GetFormattedText();
        BuildResult(_result, BlackFlowSpireMapAssets.Background(run.CurrentActIndex), LocalContext.GetMe(run)?.Character,
            title, Text(kind), LocManager.Instance.Language == "zhs",
            BlackFlowSpireGenerationService.GetTargetLife(run), BlackFlowSpireCommandLevel.FromExperience(BlackFlowSpireGenerationService.GetCommandExperience(run)),
            BlackFlowSpireGenerationService.GetHopeStatus(run), BlackFlowSpireCombatHealthRuntime.GetSettlement(room), animate);
    }

    private void BuildLoot()
    {
        _loot = new Control { Size = DesignSize, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _canvas.AddChild(_loot);
        _loot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        Rect(_loot, Colors.White, 0, 0, DesignSize.X, DesignSize.Y).Material = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Ui/BlackFlowSpire_settlement_blur.gdshader")
        };

        _scroll = new ScrollContainer
        {
            Position = new(15, 121), Size = new(549, 583), FollowFocus = true,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _loot.AddChild(_scroll);
        _scroll.GuiInput += ScrollLoot;
        _row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        _row.AddThemeConstantOverride("separation", 0);
        _scroll.AddChild(_row);

        _divider = Rect(_loot, new(1, 1, 1, 0.3f), 569, 291, 2, 200);
        _leave = ActionButton(_loot, "", 581, 258, 234, 226, RequestProceed);
        AddImage(_leave, "leave", 2, 3, 210, 192, true);
        AddImage(_leave, "leave-glow", 3, 174, 234, 45).Modulate = new(1, 1, 1, 0.5f);
        _leaveLabel = Label(_leave, Text("leave"), 6, 178, 234, 39, 25, alignment: HorizontalAlignment.Center);

        _confirm = new Control { Size = DesignSize, MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _loot.AddChild(_confirm);
        Rect(_confirm, new(0, 0, 0, 0.75f), 0, 0, DesignSize.X, DesignSize.Y);
        Rect(_confirm, new("172322"), 476, 295, 470, 185);
        Rect(_confirm, Teal, 476, 295, 470, 2);
        Label(_confirm, Text("confirm_leave"), 491, 318, 440, 79, 24, alignment: HorizontalAlignment.Center);
        _keepLoot = ActionButton(_confirm, Text("keep_rewards"), 511, 410, 185, 43, CloseConfirmation, Teal);
        ActionButton(_confirm, Text("leave"), 725, 410, 185, 43, Proceed, new("ffc062"));
    }

    private void ScrollLoot(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } mouse) return;
        var direction = mouse.ButtonIndex switch { MouseButton.WheelDown => 1, MouseButton.WheelUp => -1, _ => 0 };
        if (direction == 0) return;
        _scroll.ScrollHorizontal += direction * 130;
        _scroll.AcceptEvent();
    }

    private void AddCard(NRewardButton native, bool linked)
    {
        if (native.Reward is not { } reward) return;
        var frame = new Control { CustomMinimumSize = new(265, 579), MouseFilter = MouseFilterEnum.Pass };
        _row.AddChild(frame);
        var (underlay, tint) = reward switch
        {
            GoldReward => ("gold-underlay", Green),
            CardReward or BlackFlowSpireBattleCardPackReward => ("draft-underlay", Teal),
            RelicReward => ("relic-underlay", new Color("ffc879")),
            PotionReward => ("potion-underlay", new Color("8adfed")),
            _ => ("draft-underlay", Teal)
        };
        AddImage(frame, "card-frame", 2, 23, 272, 588);
        AddImage(frame, underlay, -2, 9, 298, 615);
        var titleGlow = AddImage(frame, "card-title", 4, 252, 272, 47);
        titleGlow.Material = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Ui/BlackFlowSpire_reward_title.gdshader")
        };
        titleGlow.Modulate = new(tint, 0.33f);
        Rect(frame, new(1, 1, 1, 0.23f), 37, 300, 210, 2);
        Label(frame, RewardTitle(reward), 23, 248, 238, 48, 23, alignment: HorizontalAlignment.Center);
        if (reward is GoldReward gold)
        {
            AddImage(frame, "gold", 52, 52, 180, 180, true);
            AddImage(frame, "count", 107, 333, 25, 25, true);
            Label(frame, FormatIngots(gold.Amount), 137, 304, 128, 69, 53,
                font: ResourceLoader.Load<Font>("res://BlackFlowSpire/Fonts/Numbers.tres"));
        }
        else
        {
            var isCardReward = reward is CardReward or BlackFlowSpireBattleCardPackReward;
            if (isCardReward) AddImage(frame, "draft", 52, 52, 180, 180, true);
            else AddNativeIcon(frame, native, reward);
            if (isCardReward || linked)
                Label(frame, Text(linked ? "choose_one" : "draft_detail"), 24, 298, 236, 46, 18,
                    new("b3bbbb"), HorizontalAlignment.Center);
        }
        var claim = ActionButton(frame, Text("claim"), 41, 424, 202, 76, () => Claim(native), tint);
        claim.Disabled = _takingReward;
        claim.MouseForcePassScrollEvents = true;
        var tips = reward.HoverTips;
        void ShowTips()
        {
            if (!claim.Disabled && _loot.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(screen))
                NHoverTipSet.CreateAndShow(claim, tips, HoverTipAlignment.Right);
        }
        claim.MouseEntered += ShowTips;
        claim.MouseExited += () => NHoverTipSet.Remove(claim);
        claim.FocusEntered += ShowTips;
        claim.FocusExited += () => NHoverTipSet.Remove(claim);
        _cards.Add(new(native, frame, claim));
    }

    private static void AddNativeIcon(Control frame, NRewardButton native, Reward reward)
    {
        if (reward is PotionReward { Potion: { } potion })
        {
            AddImage(frame, potion.LargeImage, 55, 45, 148, 148, true);
            return;
        }
        // The populated native icon may hold a customized texture or shader.
        var icon = native.GetNodeOrNull<Control>("%Icon")?.GetChildren().OfType<TextureRect>().FirstOrDefault();
        if (icon?.Texture is not { } texture) return;
        var copy = AddImage(frame, texture, 53, 39, 152, 161, true);
        copy.Material = icon.Material;
        copy.Modulate = icon.Modulate;
    }

    private static string RewardTitle(Reward reward) => reward switch
    {
        GoldReward gold when !gold.ToSerializable().WasGoldStolenBack => Text("originium"),
        CardReward => Text("draft"),
        _ => reward.Description.GetFormattedText()
    };

    internal static string FormatIngots(int gold) =>
        (gold / (decimal)BlackFlowSpirePartService.GoldPerOriginium).ToString("0.#", CultureInfo.CurrentCulture);

    private static Button ActionButton(Control parent, string text, float x, float y, float width, float height,
        Action action, Color? tint = null)
    {
        var button = new Button { Position = new(x, y), Size = new(width, height) };
        BlackFlowSpireMapAssets.ClearButtonStyle(button);
        var focus = new StyleBoxFlat { BgColor = new(0, 0, 0, 0), BorderColor = new(0.8f, 1, 0.99f, 0.8f) };
        focus.SetBorderWidthAll(1);
        focus.SetCornerRadiusAll(22);
        button.AddThemeStyleboxOverride("focus", focus);
        parent.AddChild(button);
        if (tint is { } color)
        {
            var glow = AddSlice(button, "claim", 0, 0, width, height, 60, color);
            button.MouseEntered += () => glow.Modulate = color.Lightened(0.23f);
            button.MouseExited += () => glow.Modulate = color;
            Label(button, text, 4, 0, width - 8, height, 23, alignment: HorizontalAlignment.Center);
        }
        button.Pressed += action;
        return button;
    }

    private void OpenLoot()
    {
        if (!ActiveScreenContext.Instance.IsCurrent(screen)) return;
        _continue.Hide();
        _loot.Show();
        ActiveScreenContext.Instance.Update();
    }

    private void CloseLoot()
    {
        CloseConfirmation();
        _loot.Hide();
        _continue.Show();
        ActiveScreenContext.Instance.Update();
    }

    private void Claim(NRewardButton native)
    {
        if (_takingReward || !_loot.IsVisibleInTree() || !native.IsEnabled || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        if (CardFor(native) is { } frame)
            _scroll.EnsureControlVisible(frame);
        if (native.GetNodeOrNull<Control>("%Icon") is { } icon && CardFor(native) is { } card)
            icon.GlobalPosition = card.GlobalPosition + card.Size * _canvas.Scale / 2;
        native.ForceClick();
        if (_cardsChanged) SyncCards();
        RefreshButtons();
    }

    internal bool InterceptProceed()
    {
        if (_proceeding) return false;
        if (ActiveScreenContext.Instance.IsCurrent(screen))
        {
            if (!_loot.Visible) OpenLoot();
            else if (!_confirm.Visible) RequestProceed();
        }
        return true;
    }

    private void RequestProceed()
    {
        if (_takingReward || !_nativeProceed.IsEnabled || _confirm.Visible || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        if (screen.IsComplete) Proceed();
        else
        {
            _confirm.Show();
            _keepLoot.GrabFocus();
        }
    }

    private void CloseConfirmation()
    {
        _confirm.Hide();
        if (_loot.IsVisibleInTree() && NControllerManager.Instance?.IsUsingDirectionalNavigation == true) _leave.GrabFocus();
    }

    private void Proceed()
    {
        CloseConfirmation();
        if (!_nativeProceed.IsEnabled || _takingReward || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        _proceeding = true;
        try { _nativeProceed.ForceClick(); }
        finally { _proceeding = false; }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!_ready || !IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        if (input.IsActionPressed(MegaInput.cancel) && _loot.Visible)
        {
            if (_confirm.Visible) CloseConfirmation();
            else CloseLoot();
        }
        else if (input.IsActionPressed(MegaInput.select) && GetViewport().GuiGetFocusOwner() is Button button
            && IsAncestorOf(button) && !button.Disabled)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        var active = NOverlayStack.Instance?.Peek() == screen && NRun.Instance?.GlobalUi is { } global
            && !global.MapScreen.IsOpen && !global.CapstoneContainer.InUse && !global.SubmenuStack.Stack.SubmenusOpen;
        Visible = active;
        if (!active || !_loot.Visible) return;
        if (_cardsChanged) SyncCards();
        RefreshButtons();
    }

    private void MarkCardsChanged() => _cardsChanged = true;

    private void SyncCards()
    {
        var live = _nativeList.GetChildren().OfType<Control>().SelectMany(control => control switch
        {
            NRewardButton button => new[] { (Button: button, Linked: false) },
            NLinkedRewardSet linked => linked.GetNode<Control>("%RewardContainer").GetChildren().OfType<NRewardButton>()
                .Select(button => (Button: button, Linked: true)),
            _ => Enumerable.Empty<(NRewardButton Button, bool Linked)>()
        }).ToArray();
        _cardsChanged = false;
        var restoreFocus = false;
        for (var index = _cards.Count - 1; index >= 0; index--)
        {
            var card = _cards[index];
            if (live.Any(item => item.Button == card.Native)) continue;
            restoreFocus |= card.Claim.HasFocus();
            NHoverTipSet.Remove(card.Claim);
            _row.RemoveChild(card.Frame);
            card.Frame.QueueFree();
            _cards.RemoveAt(index);
        }
        foreach (var (button, linked) in live)
            if (GodotObject.IsInstanceValid(button) && button.IsNodeReady() && CardFor(button) == null) AddCard(button, linked);

        var slots = Math.Clamp(_cards.Count, 1, 2);
        var leaveX = slots == 2 ? 581 : 317;
        if (_leaveX != leaveX)
        {
            _leaveX = leaveX;
            _scroll.Size = new(265 * slots + 18, _scroll.Size.Y);
            _divider.Position = new(leaveX - 12, _divider.Position.Y);
            _leave.Position = new(leaveX, _leave.Position.Y);
        }
        _scroll.Visible = _divider.Visible = _cards.Count != 0;
        RefreshButtons();
        if (restoreFocus && _loot.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(screen)) DefaultFocus.GrabFocus();
    }

    private void RefreshButtons()
    {
        var takingReward = _cards.Any(card => !card.Native.IsEnabled);
        if (_takingReward != takingReward)
            foreach (var card in _cards) card.Claim.Disabled = takingReward;
        _takingReward = takingReward;
        _leave.Disabled = takingReward || !_nativeProceed.IsEnabled;
        _leaveLabel.Modulate = _leave.Disabled ? new Color(1, 1, 1, 0.55f) : Colors.White;
        var label = Text(screen.IsComplete ? "proceed" : "leave");
        if (_leaveLabel.Text != label) _leaveLabel.Text = label;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_nativeList)) _nativeList.ChildOrderChanged -= MarkCardsChanged;
    }
}
