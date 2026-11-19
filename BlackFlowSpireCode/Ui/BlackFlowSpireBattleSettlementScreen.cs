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
    private const float CardStride = 265;
    private static readonly Vector2 ConfirmPosition = new(40.4f, 387.6f);
    private static readonly Color Green = new("32ff84");
    private static readonly Color Teal = new("26f4c6");
    private static readonly Color PartTint = new("98d9b8");
    private static readonly Color CollectibleTint = new("ffc879");
    private readonly List<RewardCard> _cards = [];
    private Control _canvas = null!;
    private Control _result = null!;
    private Control _loot = null!;
    private Control _row = null!;
    private ScrollContainer _scroll = null!;
    private Button _continue = null!;
    private Button _leave = null!;
    private Label _leaveLabel = null!;
    private Control _confirm = null!;
    private Control _divider = null!;
    private Control? _choice;
    private NLinkedRewardSet? _choiceGroup;
    private readonly List<Button> _choiceButtons = [];
    private Control _nativeList = null!;
    private Button _keepLoot = null!;
    private Button _discardLoot = null!;
    private Tween? _confirmTween;
    private Tween? _reflowTween;
    private bool _confirmOpen;
    private NProceedButton _nativeProceed = null!;
    private bool _takingReward;
    private bool _proceeding;
    private bool _ready;
    private bool _cardsChanged = true;

    private sealed record RewardCard(Control Native, Control Frame, Button Claim);

    internal static BlackFlowSpireBattleSettlementScreen? Find(NRewardsScreen screen) =>
        screen.GetNodeOrNull<BlackFlowSpireBattleSettlementScreen>(ViewName) is { _ready: true } view ? view : null;

    internal static bool LootIsOpen => NOverlayStack.Instance?.Peek() is NRewardsScreen rewards && Find(rewards) is { } view
        && view._loot.Visible && view.Visible;

    internal Control DefaultFocus => !_loot.Visible ? _continue : _confirmOpen ? _keepLoot
        : _choice is not null ? _choiceButtons.FirstOrDefault(button => !button.Disabled) ?? _choice
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
        AddBlurredBackdrop(_loot, 0.4f);

        _scroll = new ScrollContainer
        {
            Size = DesignSize, FollowFocus = true,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
        };
        _loot.AddChild(_scroll);
        _scroll.GuiInput += ScrollLoot;
        _row = new Control { MouseFilter = MouseFilterEnum.Pass };
        _scroll.AddChild(_row);
        BuildLeaveCard();
    }

    private static void AddBlurredBackdrop(Control parent, float brightness)
    {
        parent.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        var blur = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://BlackFlowSpire/Shaders/Ui/BlackFlowSpire_settlement_blur.gdshader") };
        blur.SetShaderParameter("brightness", brightness);
        Rect(parent, Colors.White, 0, 0, DesignSize.X, DesignSize.Y).Material = blur;
    }

    private void BuildLeaveCard()
    {
        _leave = ActionButton(_row, "", 25, 138, 270, 522, RequestProceed);
        _divider = Rect(_leave, new(1, 1, 1, 0.298f), 14, 153, 2, 200);
        AddImage(_leave, "leave", 29.8f, 123.6f, 210, 192);
        AddImage(_leave, "leave-glow", -39.6f, 295.5f, 380, 47).Modulate = new Color("b4b4b459");
        _leaveLabel = Label(_leave, Text("leave"), 70.7f, 302.2f, 160, 30, 24, alignment: HorizontalAlignment.Center);

        _confirm = new Control { Size = new(220, 84), Position = ConfirmPosition, MouseFilter = MouseFilterEnum.Stop, Visible = false };
        _leave.AddChild(_confirm);
        AddImage(_confirm, "leave-pointer", 98, -12, 24, 12);
        Rect(_confirm, new("373737"), 0, 0, 220, 42);
        Rect(_confirm, new("1d1d1d"), 0, 42, 110, 42);
        Rect(_confirm, new("3b1213"), 110, 42, 110, 42);
        var title = Label(_confirm, Text("confirm_leave"), 20, 9.8f, 180, 31.2f, 24, new("919191"), HorizontalAlignment.Center);
        AddImage(_confirm, "leave-cancel", 39, 48.5f, 32, 32);
        AddImage(_confirm, "leave-confirm", 148.5f, 46.5f, 33, 33);
        _keepLoot = ActionButton(_confirm, "", -10, 31.5f, 120, 60, () => SetConfirmation(false));
        _discardLoot = ActionButton(_confirm, "", 110, 31.5f, 120, 60, Proceed);
        void RefreshText()
        {
            title.Text = Text("confirm_leave");
            _keepLoot.AccessibilityName = Text("keep_rewards");
            _discardLoot.AccessibilityName = Text("leave");
        }
        RefreshText();
        BlackFlowSpireLocalization.RefreshWithLocale(_confirm, RefreshText);
    }

    private void ScrollLoot(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } mouse) return;
        var direction = mouse.ButtonIndex switch { MouseButton.WheelDown => 1, MouseButton.WheelUp => -1, _ => 0 };
        if (direction == 0) return;
        _scroll.ScrollHorizontal += direction * 130;
        _scroll.AcceptEvent();
    }

    private static IEnumerable<NRewardButton> Candidates(NLinkedRewardSet linked) =>
        linked.GetNode<Control>("%RewardContainer").GetChildren().OfType<NRewardButton>();

    private void AddCard(Control native, bool linked)
    {
        var group = native as NLinkedRewardSet;
        var button = group == null ? (NRewardButton)native : Candidates(group).FirstOrDefault();
        if (button?.Reward is not { } reward) return;
        var frame = new Control { Position = new(15 + CardStride * _cards.Count, 121), Size = new(CardStride, 579), MouseFilter = MouseFilterEnum.Pass };
        _row.AddChild(frame);
        if (group != null)
        {
            var entry = BuildChoiceCard(frame, new(10, 17), button, group);
            entry.Disabled = _takingReward;
            _cards.Add(new(group, frame, entry));
            return;
        }
        var (underlay, tint) = reward switch
        {
            GoldReward => ("gold-underlay", Green),
            CardReward or BlackFlowSpireBattleCardPackReward => ("draft-underlay", Teal),
            RelicReward or BlackFlowSpireCollectibleReward => ("relic-underlay", CollectibleTint),
            BlackFlowSpirePartReward => ("draft-underlay", PartTint),
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
            else AddNativeIcon(frame, button, reward);
            if (isCardReward || linked)
                Label(frame, Text(linked ? "choose_one" : "draft_detail"), 24, 298, 236, 46, 18,
                    new("b3bbbb"), HorizontalAlignment.Center);
        }
        var claim = ActionButton(frame, Text("claim"), 41, 424, 202, 76, () => Claim(button), tint);
        claim.Disabled = _takingReward;
        claim.MouseForcePassScrollEvents = true;
        var tips = reward.HoverTips;
        void ShowTips()
        {
            if (_choice == null && !claim.Disabled && _loot.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(screen))
                NHoverTipSet.CreateAndShow(claim, tips, HoverTipAlignment.Right);
        }
        claim.MouseEntered += ShowTips;
        claim.MouseExited += () => NHoverTipSet.Remove(claim);
        claim.FocusEntered += ShowTips;
        claim.FocusExited += () => NHoverTipSet.Remove(claim);
        _cards.Add(new(native, frame, claim));
    }

    private static Texture2D? RewardTexture(NRewardButton native) =>
        native.GetNodeOrNull<Control>("%Icon")?.GetChildren().OfType<TextureRect>().FirstOrDefault()?.Texture;

    private void OpenChoice(NLinkedRewardSet group)
    {
        if (_choice != null || _takingReward || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        SetConfirmation(false, animate: false);
        var candidates = Candidates(group).ToArray();
        _choiceGroup = group;
        _choice = new Control { Size = DesignSize, MouseFilter = MouseFilterEnum.Stop };
        _loot.AddChild(_choice);
        _scroll.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        _scroll.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;
        AddBlurredBackdrop(_choice, 1f);
        const float step = CardStride + 50;
        var left = (DesignSize.X - (candidates.Length * step - 50)) / 2;
        for (var index = 0; index < candidates.Length; index++)
        {
            if (index > 0)
                Label(_choice, Text("choice_or"), left + index * step - 50, 356, 50, 30, 20,
                    new("898989"), HorizontalAlignment.Center);
            _choiceButtons.Add(BuildChoiceCard(_choice, new(left + index * step, 108), candidates[index], null));
        }
        var back = ActionButton(_choice, "", 10, 9.5f, 70, 69, () => { if (!_takingReward) CloseChoice(); });
        AddImage(back, "choice-close", 0, 0, 70, 69);
        back.AccessibilityName = Text("back_to_loot");
        _choiceButtons.Add(back);
        ActiveScreenContext.Instance.Update();
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true) DefaultFocus.GrabFocus();
    }

    private void CloseChoice()
    {
        if (_choice is not { } choice) return;
        var group = _choiceGroup;
        _choice = null;
        _choiceGroup = null;
        _choiceButtons.Clear();
        _loot.RemoveChild(choice);
        choice.QueueFree();
        _scroll.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Inherited;
        _scroll.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Inherited;
        ActiveScreenContext.Instance.Update();
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true)
            (_cards.Find(card => card.Native == group)?.Claim ?? DefaultFocus).GrabFocus();
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
        SetConfirmation(false, animate: false);
        _loot.Hide();
        _continue.Show();
        ActiveScreenContext.Instance.Update();
    }

    private void Claim(NRewardButton native)
    {
        if (_takingReward || !_loot.IsVisibleInTree() || !native.IsEnabled || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        SetConfirmation(false);
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
            else RequestProceed();
        }
        return true;
    }

    private void RequestProceed()
    {
        if (_takingReward || _choice != null || !_nativeProceed.IsEnabled
            || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        if (screen.IsComplete) Proceed();
        else SetConfirmation(!_confirmOpen);
    }

    private void SetConfirmation(bool open, bool animate = true)
    {
        if (_confirmOpen == open && (animate || !_confirm.Visible)) return;
        _confirmOpen = open;
        _confirmTween?.Kill();
        var directional = NControllerManager.Instance?.IsUsingDirectionalNavigation == true;
        if (GetViewport().GuiGetFocusOwner() is { } focused && (focused == _leave || _confirm.IsAncestorOf(focused)))
        {
            if (!directional) focused.ReleaseFocus();
            else if (!open && _leave.IsVisibleInTree()) _leave.GrabFocus();
        }
        _keepLoot.Disabled = _discardLoot.Disabled = !open;
        if (!animate)
        {
            _confirm.Visible = open;
            return;
        }
        var hiddenPosition = ConfirmPosition + Vector2.Up * 10;
        if (!_confirm.Visible)
        {
            _confirm.Position = hiddenPosition;
            _confirm.Modulate = new(1, 1, 1, 0);
            _confirm.Show();
        }
        if (open && directional) _keepLoot.GrabFocus();
        _confirmTween = _confirm.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _confirmTween.TweenProperty(_confirm, "position", open ? ConfirmPosition : hiddenPosition, 0.2);
        _confirmTween.TweenProperty(_confirm, "modulate:a", open ? 1f : 0f, 0.2);
        if (!open) _confirmTween.Chain().TweenCallback(Callable.From(_confirm.Hide));
    }

    private void Proceed()
    {
        if (!_nativeProceed.IsEnabled || _takingReward || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        SetConfirmation(false, animate: false);
        _proceeding = true;
        try { _nativeProceed.ForceClick(); }
        finally { _proceeding = false; }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!_ready || !IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(screen)) return;
        if (input.IsActionPressed(MegaInput.cancel) && _loot.Visible)
        {
            if (_choice != null)
            {
                if (!_takingReward) CloseChoice();
            }
            else if (_confirmOpen) SetConfirmation(false);
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
            NRewardButton => new[] { (Native: control, Linked: false) },
            NLinkedRewardSet linked when BlackFlowSpireBattleRewardChoicePatch.IsForestChoice(linked) =>
                new[] { (Native: control, Linked: false) },
            NLinkedRewardSet linked => Candidates(linked).Select(button => (Native: (Control)button, Linked: true)),
            _ => Enumerable.Empty<(Control Native, bool Linked)>()
        }).ToArray();
        _cardsChanged = false;
        var restoreFocus = false;
        var removed = false;
        var animate = _loot.IsVisibleInTree();
        for (var index = _cards.Count - 1; index >= 0; index--)
        {
            var card = _cards[index];
            if (live.Any(item => item.Native == card.Native)) continue;
            restoreFocus |= card.Claim.HasFocus();
            NHoverTipSet.Remove(card.Claim);
            _cards.RemoveAt(index);
            removed = true;
            card.Claim.Disabled = true;
            card.Claim.MouseFilter = card.Frame.MouseFilter = MouseFilterEnum.Ignore;
            if (animate) FadeClaimedCard(card.Frame);
            else card.Frame.QueueFree();
        }
        var retained = _cards.Count;
        foreach (var (native, linked) in live)
            if (GodotObject.IsInstanceValid(native) && native.IsNodeReady() && _cards.All(card => card.Native != native))
                AddCard(native, linked);
        if (removed || _cards.Count != retained || !_ready)
        {
            SetConfirmation(false, animate: false);
            LayoutRewardRow(removed && animate);
        }
        _divider.Visible = _cards.Count != 0;
        RefreshButtons();
        if (_choiceGroup != null && !live.Any(item => item.Native == _choiceGroup)) CloseChoice();
        if (restoreFocus && _loot.IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(screen)) DefaultFocus.GrabFocus();
    }

    private static void FadeClaimedCard(Control frame)
    {
        var fade = frame.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        fade.TweenProperty(frame, "position:y", frame.Position.Y - 50, 0.3);
        fade.TweenProperty(frame, "modulate:a", 0f, 0.3);
        fade.Chain().TweenCallback(Callable.From(frame.QueueFree));
    }

    private void LayoutRewardRow(bool animate)
    {
        _reflowTween?.Kill();
        _reflowTween = animate ? _row.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Linear) : null;
        void MoveTo(Control control, float targetX)
        {
            if (_reflowTween == null) control.Position = new(targetX, control.Position.Y);
            else if (!Mathf.IsEqualApprox(control.Position.X, targetX))
                _reflowTween.TweenProperty(control, "position:x", targetX, 0.2).SetDelay(0.2);
        }
        for (var index = 0; index < _cards.Count; index++) MoveTo(_cards[index].Frame, 15 + index * CardStride);
        var leaveX = 25 + _cards.Count * CardStride;
        MoveTo(_leave, leaveX);
        var contentSize = new Vector2(leaveX + _leave.Size.X, DesignSize.Y);
        if (_reflowTween == null) _row.CustomMinimumSize = contentSize;
        else _reflowTween.TweenProperty(_row, "custom_minimum_size", contentSize, 0.2).SetDelay(0.2);
    }

    private void RefreshButtons()
    {
        var takingReward = _cards.Any(card => card.Native is NLinkedRewardSet group
            ? Candidates(group).Any(button => !button.IsEnabled) : !((NRewardButton)card.Native).IsEnabled);
        if (_takingReward != takingReward)
        {
            foreach (var card in _cards) card.Claim.Disabled = takingReward;
            foreach (var candidate in _choiceButtons) candidate.Disabled = takingReward;
        }
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
