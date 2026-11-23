using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireRestUi(RunState state, NMapScreen screen) : Node
{
    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || !screen.IsOpen || !screen.IsVisibleInTree()
            || NModalContainer.Instance is not { OpenModal: null } container) return;
        var map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (BlackFlowSpireRestRules.IsPending(map, progress))
            container.Add(new BlackFlowSpireRestScreen(state, screen, progress, BlackFlowSpireRestRules.Options(map, map.PositionNodeId)),
                showBackstop: false);
    }

    public static void Dismiss(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireRestScreen rest } container
            && ReferenceEquals(rest.Screen, screen)) container.Clear();
    }
}

internal partial class BlackFlowSpireRestScreen(RunState state, NMapScreen screen, BlackFlowSpireExplorationProgress progress,
    IReadOnlyList<BlackFlowSpireRestChoice> choices) : Control, IScreenContext
{
    private const string Key = "UI.REST.";
    private BlackFlowSpireEventView _view = null!;
    private BlackFlowSpireRestChoice? _result;
    private bool _submitting;
    private bool _focused;

    public NMapScreen Screen => screen;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => _submitting ? null : _result.HasValue ? _view?.LeaveButton : _view?.GetChoice(0);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _view = new BlackFlowSpireEventView(this, index => Submit(choices[index]), () => Submit(null));
        if (BlackFlowSpireTeam.IsOnline && BlackFlowSpireTeam.CanOperate) AddChild(new BlackFlowSpireTeamMapStatus(state));
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || !GodotObject.IsInstanceValid(screen) || !screen.IsOpen)
        {
            BlackFlowSpireRestUi.Dismiss(screen);
            return;
        }
        FitViewport();
        var busy = _submitting || BlackFlowSpireRuntime.IsTraveling;
        _view.SetBusy(busy);
        if (!busy && _result != progress.RestChoice) Refresh();
        if (!_focused && DefaultFocusedControl is Button { Disabled: false } button)
        {
            _focused = true;
            button.GrabFocus();
        }
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
        var result = progress.RestChoice;
        var description = result is { } chosen ? ChoiceKey(chosen) + ".result" : Key + "description";
        _view.SetStory(BlackFlowSpireEventView.LoadTexture("pic_rogue_6_33"),
            BlackFlowSpireText.Get(Key + "title"), BlackFlowSpireText.Get(description));
        if (result.HasValue)
        {
            _view.ShowResult(BlackFlowSpireText.Get(Key + "leave"));
            if (_result != result) _focused = false;
        }
        else
        {
            _view.SetOptions(choices.Select(choice =>
            {
                var key = ChoiceKey(choice);
                return new BlackFlowSpireEventOption(BlackFlowSpireText.Get(key + ".title"),
                    BlackFlowSpireText.Get(key + ".description"), BlackFlowSpireEventView.LoadTexture(Icon(choice)));
            }).ToArray());
        }
        _result = result;
    }

    private void Submit(BlackFlowSpireRestChoice? choice)
    {
        if (_submitting || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitting = true;
        _view.SetBusy(true);
        Callable.From(() => TaskHelper.RunSafely(SubmitAsync(choice))).CallDeferred();
    }

    private async Task SubmitAsync(BlackFlowSpireRestChoice? choice)
    {
        try { await BlackFlowSpireRuntime.ResolveRest(choice); }
        finally { _submitting = false; }
    }

    private static string ChoiceKey(BlackFlowSpireRestChoice choice) => Key + (int)choice;

    private static string Icon(BlackFlowSpireRestChoice choice) => choice switch
    {
        BlackFlowSpireRestChoice.MaximumLife => "hpmax",
        BlackFlowSpireRestChoice.Hope => "population",
        BlackFlowSpireRestChoice.ActionPoints => "ap_drop",
        BlackFlowSpireRestChoice.PartsCapacity => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };
}
