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
    private Control _view = null!;
    private Button _leave = null!;
    private Button _firstChoice = null!;
    private BlackFlowSpireRestChoice? _result;
    private bool _submitting;
    private bool _focused;

    public NMapScreen Screen => screen;
    public NetScreenType ScreenType => NetScreenType.Room;
    public Control? DefaultFocusedControl => _submitting ? null : _result.HasValue ? _leave : _firstChoice;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Events/BlackFlowSpireExitChoice.tscn").Instantiate<Control>();
        AddChild(_view);
        if (BlackFlowSpireTeam.IsHost) AddChild(new BlackFlowSpireTeamMapStatus(state));
        _view.Call("configure_rest_choices", choices.Count);
        _firstChoice = _view.GetNode<Button>("Canvas/Choices/Option0");
        _leave = _view.GetNode<Button>("Canvas/Leave");
        _leave.Pressed += () =>
        {
            if (!_view.Call("finish_result_text").AsBool()) Submit(null);
        };
        for (var index = 0; index < choices.Count; index++)
        {
            var selectedIndex = index;
            var choice = choices[index];
            var button = _view.GetNode<Button>("Canvas/Choices/Option" + index);
            var confirm = button.GetNode<Button>("Confirm");
            button.Pressed += () =>
            {
                _view.Call("select_choice", selectedIndex);
                confirm.GrabFocus();
            };
            confirm.Pressed += () => Submit(choice);
        }
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
        _view.Call("set_busy", busy);
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
        _view.Call("bind_story", "pic_rogue_6_33", BlackFlowSpireText.Get(Key + "title"), BlackFlowSpireText.Get(description));
        _leave.TooltipText = BlackFlowSpireText.Get(Key + "leave");
        if (result.HasValue)
        {
            if (_result != result)
            {
                _view.Call("show_rest_result");
                _focused = false;
            }
        }
        else
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                var key = ChoiceKey(choice);
                _view.Call("bind_choice", index, BlackFlowSpireText.Get(key + ".title"), BlackFlowSpireText.Get(key + ".description"), Icon(choice));
            }
        _result = result;
    }

    private void Submit(BlackFlowSpireRestChoice? choice)
    {
        if (_submitting || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanTravel(state)) return;
        _submitting = true;
        _view.Call("set_busy", true);
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
