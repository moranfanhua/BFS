using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireHuntUi
{
    public static void Show(Control parent, NMapScreen screen, RunState state) =>
        parent.AddChild(new BlackFlowSpireHuntLayer(state, screen) { Name = "BlackFlowSpireHuntLayer" });

    internal static void DismissNotice(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireHuntNotice notice } container
            && notice.BelongsTo(screen)) container.Clear();
    }

    internal static void Bind(Control root)
    {
        // Each scene keeps its body/title/number font bindings when localized.
        BlackFlowSpireLocalization.BindStaticText(root);
    }
}

// The map remains visible but cannot accept exploration input during the hunt.
// A native modal is used for the forced notice; its confirmed detail view stays
// below the game's top bar so saving/quitting remains available.
internal partial class BlackFlowSpireHuntLayer(RunState state, NMapScreen screen) : Control
{
    private BlackFlowSpireExplorationProgress _progress = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        _progress = BlackFlowSpireGenerationService.GetProgress(state);
        if (_progress.HuntPhase == BlackFlowSpireHuntPhase.AwaitingBattle)
        {
            var details = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/Hunt/BlackFlowSpireHuntDetails.tscn").Instantiate<Control>();
            AddChild(details);
            details.GetNode<Control>("%Artwork").GetNode<Control>("btn_certain").Hide();
            BlackFlowSpireHuntUi.Bind(details);
            var start = details.GetNode<Button>("%StartHuntBattle");
            start.Disabled = BlackFlowSpireGenerationService.GetEngine(state).IsOverloaded;
            start.Pressed += () => TaskHelper.RunSafely(BlackFlowSpireRuntime.StartHunt());
            details.GetNode<Label>("Canvas/Availability").Visible = start.Disabled;
            var intelligence = details.GetNode<Button>("%EnemyIntelligence");
            intelligence.Disabled = false;
            intelligence.GetNode<TextureRect>("Image").Modulate = Colors.White;
            intelligence.Pressed += () =>
                BlackFlowSpireEnemyIntelModal.Open(BlackFlowSpireResidentEncounterService.CreateHunt(BlackFlowSpireGenerationService.GetOrGenerate(state)), hunt: true);
            if (!start.Disabled && BlackFlowSpireTeam.CanOperate) start.GrabFocus();
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (!BlackFlowSpireRuntime.CanContinueRun || !ReferenceEquals(BlackFlowSpireRuntime.State, state)
            || !screen.IsOpen || !screen.IsVisibleInTree()) return;
        if (_progress.HuntPhase != BlackFlowSpireHuntPhase.NoticePending) return;
        if (NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new BlackFlowSpireHuntNotice(state, screen) { Name = "BlackFlowSpireHuntNotice" }, false);
    }

    public override void _GuiInput(InputEvent input)
    {
        // Background clicks never dismiss or confirm the forced flow.
        if (input is InputEventMouseButton) AcceptEvent();
    }
}

internal partial class BlackFlowSpireHuntNotice(RunState state, NMapScreen screen) : Control, IScreenContext
{
    private Button? _confirm;
    private bool _focused;
    public Control? DefaultFocusedControl => _confirm is { Disabled: false } ? _confirm : null;
    internal bool BelongsTo(NMapScreen map) => ReferenceEquals(screen, map);
    private bool HasValidRunContext => BlackFlowSpireRuntime.CanContinueRun
        && ReferenceEquals(BlackFlowSpireRuntime.State, state)
        && GodotObject.IsInstanceValid(screen) && screen.IsOpen;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/Hunt/BlackFlowSpireHuntNotice.tscn").Instantiate<Control>();
        AddChild(view);
        BlackFlowSpireHuntUi.Bind(view);
        _confirm = view.GetNode<Control>("%Artwork").GetNode<Button>("%ConfirmHunt");
        _confirm.Pressed += Confirm;
        BlackFlowSpireLocalization.RefreshWithLocale(this, () =>
        {
            if (GodotObject.IsInstanceValid(view)) BlackFlowSpireHuntUi.Bind(view);
        });
        Entry.Logger.Info("[BlackFlowSpire] Hunt notice opened; playing the imported entry curves.");
    }

    public override void _Process(double delta)
    {
        if (!HasValidRunContext)
        {
            if (ReferenceEquals(NModalContainer.Instance?.OpenModal, this)) NModalContainer.Instance.Clear();
            return;
        }
        if (!_focused && _confirm is { Disabled: false })
        {
            _focused = true;
            _confirm.GrabFocus();
            ActiveScreenContext.Instance.Update();
        }
    }

    private void Confirm()
    {
        if (!HasValidRunContext) return;
        if (!BlackFlowSpireTeam.CanTravel(state)) return;
        TaskHelper.RunSafely(BlackFlowSpireRuntime.ConfirmHunt());
    }
}
