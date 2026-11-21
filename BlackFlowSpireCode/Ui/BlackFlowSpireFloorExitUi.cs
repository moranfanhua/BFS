using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireFloorExitUi(RunState state, NMapScreen screen) : Node
{
    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state) || BlackFlowSpireRuntime.IsTraveling
            || !screen.IsOpen || !screen.IsVisibleInTree()) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        if (BlackFlowSpireFloorExitRules.IsPending(map, BlackFlowSpireGenerationService.GetProgress(state))
            && NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new BlackFlowSpireFloorExitScreen(state, screen) { Name = "BlackFlowSpireFloorExitChoice" }, showBackstop: false);
    }
}

internal partial class BlackFlowSpireFloorExitScreen(RunState state, NMapScreen screen) : Control, IScreenContext
{
    private BlackFlowSpireEventView _view = null!;
    private bool _submitted;
    private bool _focused;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => _submitted ? null : _view?.GetChoice(1);
    private bool ValidContext => BlackFlowSpireRuntime.CanContinueRun && ReferenceEquals(BlackFlowSpireRuntime.State, state)
        && GodotObject.IsInstanceValid(screen) && screen.IsOpen;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _view = new BlackFlowSpireEventView(this, index => Choose(index == 0));
        if (BlackFlowSpireTeam.IsOnline && BlackFlowSpireTeam.CanOperate) AddChild(new BlackFlowSpireTeamMapStatus(state));
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    private void FitViewport()
    {
        var area = GetViewportRect();
        var inverse = (GetParent() as Control)?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        Position = inverse * area.Position;
        Size = inverse * area.End - Position;
    }

    private void Refresh()
    {
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var node = map.GetNode(map.PositionNodeId);
        var final = node.Content == BlackFlowSpireNodeContent.Final;
        var key = final ? "UI.FLOOR_EXIT.FINAL" : "UI.FLOOR_EXIT.PATH";
        var background = BlackFlowSpireEventView.LoadTexture(BlackFlowSpireFloorExitRules.Background(map, node));
        _view.SetStory(background, BlackFlowSpireText.Get(key + ".title"), BlackFlowSpireText.Get(key + ".description"));
        _view.SetOptions([
            new(BlackFlowSpireText.Get(key + ".accept"), BlackFlowSpireText.Get(key + ".accept_description"),
                BlackFlowSpireEventView.LoadTexture(final ? "zone_end" : "scrap_drop")),
            new(BlackFlowSpireText.Get(key + ".leave"), BlackFlowSpireText.Get(key + ".leave_description"),
                BlackFlowSpireEventView.LoadTexture("leave"))
        ]);
    }

    public override void _Process(double delta)
    {
        if (_submitted) return;
        if (!ValidContext)
        {
            if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance.Clear();
            return;
        }
        FitViewport();
        if (!_focused && DefaultFocusedControl is Button { Disabled: false } button)
        {
            _focused = true;
            button.GrabFocus();
        }
    }

    private void Choose(bool accept)
    {
        if (_submitted || !ValidContext || !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitted = true;
        _view.SetBusy(true);
        if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance.Clear();
        Callable.From(() => { TaskHelper.RunSafely(BlackFlowSpireRuntime.ResolveFloorExit(accept)); }).CallDeferred();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        Choose(false);
        GetViewport().SetInputAsHandled();
    }
}
