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

internal static class ForestHuntUi
{
    public static bool Preview()
    {
        if (NModalContainer.Instance is not { OpenModal: null } container) return false;
        container.Add(new ForestHuntNotice(null, null) { Name = "ForestHuntPreview" }, false);
        return true;
    }
    public static void Show(Control parent, NMapScreen screen, RunState state) =>
        parent.AddChild(new ForestHuntLayer(state, screen) { Name = "ForestHuntLayer" });

    internal static void DismissNotice(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: ForestHuntNotice notice } container
            && notice.BelongsTo(screen)) container.Clear();
    }

    internal static void Bind(Control root)
    {
        // Each scene keeps its body/title/number font bindings when localized.
        ForestLocalization.BindStaticText(root);
    }
}

// The map remains visible but cannot accept exploration input during the hunt.
// A native modal is used for the forced notice; its confirmed detail view stays
// below the game's top bar so saving/quitting remains available.
internal partial class ForestHuntLayer(RunState state, NMapScreen screen) : Control
{
    private ForestExplorationProgress _progress = null!;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        _progress = ForestGenerationService.GetProgress(state);
        if (_progress.HuntPhase == ForestHuntPhase.AwaitingBattle)
        {
            var details = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/Hunt/ForestHuntDetails.tscn").Instantiate<Control>();
            AddChild(details);
            details.GetNode<Control>("%Artwork").GetNode<Control>("btn_certain").Hide();
            ForestHuntUi.Bind(details);
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (!ForestRuntime.CanContinueRun || !ReferenceEquals(ForestRuntime.State, state)
            || !screen.IsOpen || !screen.IsVisibleInTree()) return;
        if (_progress.HuntPhase != ForestHuntPhase.NoticePending) return;
        if (NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new ForestHuntNotice(state, screen) { Name = "ForestHuntNotice" }, false);
    }

    public override void _GuiInput(InputEvent input)
    {
        // Background clicks never dismiss or confirm the forced flow.
        if (input is InputEventMouseButton) AcceptEvent();
    }
}

internal partial class ForestHuntNotice(RunState? state, NMapScreen? screen) : Control, IScreenContext
{
    private Button? _confirm;
    private bool _focused;
    private bool _confirmed;
    private Control? _view;
    public Control? DefaultFocusedControl => _confirm is { Disabled: false } ? _confirm : null;
    internal bool BelongsTo(NMapScreen map) => ReferenceEquals(screen, map);
    private bool HasValidRunContext => state == null || ForestRuntime.CanContinueRun
        && ReferenceEquals(ForestRuntime.State, state) && screen != null
        && GodotObject.IsInstanceValid(screen) && screen.IsOpen;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/Hunt/ForestHuntNotice.tscn").Instantiate<Control>();
        _view = view;
        AddChild(view);
        ForestHuntUi.Bind(view);
        _confirm = view.GetNode<Control>("%Artwork").GetNode<Button>("%ConfirmHunt");
        _confirm.Pressed += Confirm;
        ForestLocalization.RefreshWithLocale(this, () =>
        {
            if (_view != null && GodotObject.IsInstanceValid(_view)) ForestHuntUi.Bind(_view);
            if (state == null && _confirmed && _confirm != null) _confirm.Text = ForestText.Get("UI.CLOSE");
        });
        Entry.Logger.Info("[Forest] Hunt notice opened; playing the imported entry curves.");
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
        if (_confirmed) return;
        if (state == null)
        {
            _confirmed = true;
            foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
            var details = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/Hunt/ForestHuntDetails.tscn").Instantiate<Control>();
            _view = details;
            AddChild(details);
            ForestHuntUi.Bind(details);
            var close = new Button { Text = ForestText.Get("UI.CLOSE"), Position = new(35, 35), Size = new(150, 55) };
            ForestTheme.Format(close, new Color("9f4b4b"), 22);
            AddChild(close);
            close.Pressed += () => NModalContainer.Instance!.Clear();
            _confirm = close;
            _focused = false;
            Entry.Logger.Info("[Forest] Read-only hunt preview advanced to details; run data unchanged.");
            return;
        }
        if (!ForestTeam.CanTravel(state)) return;
        TaskHelper.RunSafely(ForestRuntime.ConfirmHunt());
    }
}
