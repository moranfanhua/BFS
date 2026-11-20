using System.Runtime.CompilerServices;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireExpeditionUi(RunState state, NMapScreen screen, BlackFlowSpireMap map,
    BlackFlowSpireExplorationProgress progress) : Node
{
    // The settled result survives rejoining. Each open run shows it once on this client.
    private static readonly ConditionalWeakTable<RunState, HashSet<(int Act, int Node)>> Seen = new();

    public static bool HasNotice(RunState state, BlackFlowSpireExplorationProgress progress) =>
        progress.ExpeditionReturn is { } notice && !Seen.GetOrCreateValue(state).Contains((notice.SourceActIndex, notice.SourceNodeId));

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || state.CurrentRoom is not MapRoom
            || !screen.IsOpen || !screen.IsVisibleInTree()
            || NRun.Instance?.HasNode("BlackFlowSpireZoneEntry") == true
            || NModalContainer.Instance is not { OpenModal: null } container) return;
        var notice = HasNotice(state, progress) ? progress.ExpeditionReturn : null;
        if (notice == null && (!BlackFlowSpireExpeditionRules.IsPending(map, progress)
            || BlackFlowSpireTeam.IsGuest && progress.ExpeditionScene != BlackFlowSpireExpeditionScene.Intro)) return;
        container.Add(new BlackFlowSpireExpeditionScreen(state, screen, map, progress, notice, () =>
        {
            if (notice == null) return;
            Seen.GetOrCreateValue(state).Add((notice.SourceActIndex, notice.SourceNodeId));
            Dismiss(screen);
            BlackFlowSpireMapUi.Show(screen, state, false);
            ActiveScreenContext.Instance.Update();
        }), showBackstop: false);
    }

    public static void Dismiss(NMapScreen screen, bool choiceOnly = false)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireExpeditionScreen expedition } container
            && ReferenceEquals(expedition.Screen, screen) && (!choiceOnly || expedition.IsChoice)) container.Clear();
    }
}

internal partial class BlackFlowSpireExpeditionScreen(RunState state, NMapScreen screen, BlackFlowSpireMap map,
    BlackFlowSpireExplorationProgress progress, BlackFlowSpireExpeditionReturn? notice, Action closeNotice) : Control, IScreenContext
{
    internal const string AssetFolder = "res://BlackFlowSpire/Images/Events/Expedition/";
    internal const string SceneFolder = "res://BlackFlowSpire/Scenes/Events/";
    private static readonly Vector2 Design = new(1280, 720);
    private BlackFlowSpireEventView? _eventView;
    private BlackFlowSpireExpeditionPicker? _picker;
    private Control? _returnCanvas;
    private Button? _continue;
    private BlackFlowSpireExpeditionScene _shown;
    private bool _submitting;
    private bool _waiting;
    private bool _focused;
    private bool Busy => _submitting || BlackFlowSpireRuntime.IsTraveling;

    public NMapScreen Screen => screen;
    internal bool IsChoice => notice == null;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => _continue ?? (_picker is { } picker ? picker.DefaultFocus
        : Busy || _waiting ? null : _shown != BlackFlowSpireExpeditionScene.Intro ? _eventView?.LeaveButton
        : _eventView?.GetChoice(HasCards ? 0 : 1));
    private bool HasCards => LocalContext.GetMe(state)?.Deck.Cards.Any(BlackFlowSpireGenerationService.CanDispatchCard) == true;
    private bool WaitingForParty => LocalContext.GetMe(state) is { } player && BlackFlowSpireGenerationService.HasExpeditionChoice(state, player.NetId);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Stop;
        FitViewport();
        if (notice != null)
        {
            var panel = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireExpeditionReturn.tscn").Instantiate<Control>();
            AddChild(panel);
            _returnCanvas = panel.GetNode<Control>("Canvas");
            _continue = _returnCanvas.GetNode<Button>("Continue");
            _continue.Pressed += closeNotice;
            var animation = panel.GetNode<AnimationPlayer>("AnimationPlayer");
            animation.AnimationFinished += _ => _continue.Disabled = false;
            animation.Play("entry_expedition");
        }
        else
        {
            _eventView = new(this, index =>
            {
                if (index == 0) OpenPicker();
                else Submit(BlackFlowSpireExpeditionRules.Rest);
            }, () => Submit(BlackFlowSpireExpeditionRules.Leave));
            if (BlackFlowSpireTeam.IsHost) AddChild(new BlackFlowSpireTeamMapStatus(state) { ZIndex = 1 });
        }
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || !GodotObject.IsInstanceValid(screen) || !screen.IsOpen
            || notice == null && !BlackFlowSpireExpeditionRules.IsPending(map, progress))
        {
            BlackFlowSpireExpeditionUi.Dismiss(screen);
            return;
        }
        FitViewport();
        if (_returnCanvas != null) FitCanvas(_returnCanvas, Size);
        if (_eventView != null)
        {
            var busy = Busy;
            if (!busy && (_shown != progress.ExpeditionScene || _waiting != WaitingForParty))
            {
                ClosePicker();
                _eventView.ResetSelection();
                Refresh();
            }
            _eventView.SetBusy(busy || _picker != null);
            _picker?.SetBusy(busy);
        }
        if (!_focused && DefaultFocusedControl is Button { Disabled: false } button)
        {
            _focused = true;
            button.GrabFocus();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_picker == null || Busy || !inputEvent.IsActionPressed("ui_cancel")) return;
        ClosePicker();
        GetViewport().SetInputAsHandled();
    }

    private void FitViewport()
    {
        var viewport = GetViewportRect();
        var inverse = (GetParent() as Control)?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        Position = inverse * viewport.Position;
        Size = inverse * viewport.End - Position;
    }

    internal static void FitCanvas(Control canvas, Vector2 size)
    {
        var factor = Math.Min(size.X / Design.X, size.Y / Design.Y);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (size - Design * factor) / 2;
    }

    private void Refresh()
    {
        if (notice != null)
        {
            var reward = BlackFlowSpirePartsUi.Title(notice.Reward);
            _returnCanvas!.GetNode<Label>("Content/Title").Text = Text("return.title");
            var description = Text("return.team", ("Part", reward));
            if (LocalContext.GetMe(state) is { } player && notice.Cards.TryGetValue(player.NetId, out var card))
            {
                var key = card.Upgraded ? "return.upgraded" : "return.description";
                description = Text(notice.Hope > 0 ? key + ".team" : key,
                    ("Card", SaveUtil.CardOrDeprecated(new ModelId(card.Category, card.Entry)).Title), ("Part", reward));
            }
            _returnCanvas.GetNode<Label>("Content/Description").Text = description;
            _continue!.TooltipText = Text("leave");
            return;
        }

        _shown = progress.ExpeditionScene;
        _waiting = WaitingForParty;
        _focused = false;
        var story = _shown switch
        {
            BlackFlowSpireExpeditionScene.Rested => "story.rested",
            BlackFlowSpireExpeditionScene.Departed => BlackFlowSpireTeam.IsOnline ? "story.team_departed" : "story.departed",
            _ when _waiting => "story.waiting",
            _ => "story.intro"
        };
        _eventView!.SetStory(Asset("pic_rogue_6_22"), Text("title"), Text(story));
        if (_waiting) _eventView.SetOptions([]);
        else if (_shown == BlackFlowSpireExpeditionScene.Intro)
        {
            var hasCards = HasCards;
            _eventView.SetOptions([
                new(Text("dispatch.title"), Text(hasCards ? "dispatch.description" : "dispatch.empty"),
                    ResourceLoader.Load<Texture2D>("res://BlackFlowSpire/Images/Map/Nodes/icon_expedition.png"), hasCards),
                new(Text(BlackFlowSpireTeam.IsOnline ? "pass.title" : "rest.title"),
                    Text(BlackFlowSpireTeam.IsOnline ? "pass.description" : "rest.description"), BlackFlowSpireEventView.LoadTexture("population"))
            ]);
            _picker?.RefreshLocale();
        }
        else _eventView.ShowResult(Text("leave"));
    }

    private void OpenPicker()
    {
        if (Busy || _waiting || _picker != null || LocalContext.GetMe(state) is not { } owner || !HasCards) return;
        _eventView!.Visible = false;
        _picker = new(this, owner, ClosePicker, Submit);
        _focused = false;
        ActiveScreenContext.Instance.Update();
    }

    private void ClosePicker()
    {
        if (_picker == null) return;
        _picker.Close();
        _picker = null;
        _eventView!.Visible = true;
        _focused = false;
        ActiveScreenContext.Instance.Update();
    }

    private void Submit(int choice)
    {
        if (Busy || _waiting || choice == BlackFlowSpireExpeditionRules.Leave && !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitting = true;
        _eventView!.SetBusy(true);
        _picker?.SetBusy(true);
        Callable.From(() => { _ = TaskHelper.RunSafely(SubmitAsync(choice)); }).CallDeferred();
    }

    private async Task SubmitAsync(int choice)
    {
        try { await BlackFlowSpireRuntime.ResolveExpedition(choice); }
        finally { _submitting = false; }
    }

    internal static Texture2D Asset(string name) => ResourceLoader.Load<Texture2D>(AssetFolder + name + ".png");
    internal static string Text(string key, params (string Name, object Value)[] arguments) =>
        BlackFlowSpireText.Get("UI.EXPEDITION." + key, arguments);
}
