using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpirePassageUi
{
    public static void Show(Control parent, NMapScreen screen, RunState state) =>
        parent.AddChild(new BlackFlowSpirePassageLayer(state, screen) { Name = "BlackFlowSpirePassageLayer" });

    public static void DismissNotice(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpirePassageNotice notice } container
            && notice.BelongsTo(screen)) container.Clear();
    }
}

internal partial class BlackFlowSpirePassageLayer(RunState state, NMapScreen screen) : Control
{
    private BlackFlowSpireMap _map = null!;
    private BlackFlowSpireExplorationProgress _progress = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        _map = BlackFlowSpireGenerationService.GetOrGenerate(state);
        _progress = BlackFlowSpireGenerationService.GetProgress(state);
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || BlackFlowSpireRuntime.IsTraveling || !screen.IsOpen || !screen.IsVisibleInTree()) return;
        if (BlackFlowSpirePassageRules.IsPending(_map, _progress)
            && NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new BlackFlowSpirePassageNotice(state, screen) { Name = "BlackFlowSpirePassageNotice" });
    }
}

internal partial class BlackFlowSpirePassageNotice(RunState state, NMapScreen screen) : Control, IScreenContext
{
    private NVerticalPopup _popup = null!;
    private bool _submitted;
    private bool _focused;
    public Control? DefaultFocusedControl => _submitted ? null : _popup?.NoButton;
    internal bool BelongsTo(NMapScreen map) => ReferenceEquals(screen, map);
    private bool HasValidContext => BlackFlowSpireRuntime.CanContinueRun && ReferenceEquals(BlackFlowSpireRuntime.State, state)
        && GodotObject.IsInstanceValid(screen) && screen.IsOpen;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _popup = ResourceLoader.Load<PackedScene>("res://scenes/ui/vertical_popup.tscn").Instantiate<NVerticalPopup>();
        AddChild(_popup);
        _popup.InitYesButton(BlackFlowSpireLocalization.CreateLocString("UI.PASSAGE.travel"), _ => Choose(true));
        _popup.InitNoButton(BlackFlowSpireLocalization.CreateLocString("UI.PASSAGE.stay"), _ => Choose(false));
        RefreshText();
        BlackFlowSpireLocalization.RefreshWithLocale(this, RefreshText);
    }

    private void RefreshText()
    {
        _popup.SetText(BlackFlowSpireText.Get("MAP.DOOR.title"), BlackFlowSpireText.Get("UI.PASSAGE.message"));
        _popup.YesButton.SetText(BlackFlowSpireText.Get("UI.PASSAGE.travel"));
        _popup.NoButton.SetText(BlackFlowSpireText.Get("UI.PASSAGE.stay"));
    }

    public override void _Process(double delta)
    {
        if (!HasValidContext)
        {
            if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance.Clear();
            return;
        }
        if (!_focused && !_submitted)
        {
            _focused = true;
            _popup.NoButton.GrabFocus();
            ActiveScreenContext.Instance.Update();
        }
    }

    private void Choose(bool usePassage)
    {
        if (_submitted || !HasValidContext || BlackFlowSpireTeam.IsGuest) return;
        _submitted = true;
        // Let the native popup finish closing before a teleport or hunt opens another UI.
        Callable.From(() => { TaskHelper.RunSafely(BlackFlowSpireRuntime.ResolvePassage(usePassage)); }).CallDeferred();
    }
}
