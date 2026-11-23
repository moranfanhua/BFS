using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireMovementModeScreen(RunState state, NMapScreen screen, Control widget,
    Action<BlackFlowSpireProcessedPart?> preview) : Control, IScreenContext
{
    private const int Walk = -1;
    private readonly Dictionary<int, Button> _rows = [];
    private readonly NMapScreen _screen = screen;
    private Control _view = null!;
    private Tween? _animation;
    private int _selected;
    private bool _closing;
    private bool _previewing;
    private Control? _host;
    private Viewport? _viewport;

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public Control? DefaultFocusedControl => _view == null || _closing ? null
        : Confirm.Visible ? Confirm.GetNode<Button>("Card/Cancel") : _rows.GetValueOrDefault(_selected);

    private Control Confirm => _view.GetNode<Control>("Confirm");
    private Control Panel => _view.GetNode<Control>("Panel");
    private Button Finish => Panel.GetNode<Button>("Finish");
    private BlackFlowSpireEngineInventory Inventory => BlackFlowSpireGenerationService.GetEngine(state);

    internal static bool CanOpen(RunState state, NMapScreen screen) =>
        ReferenceEquals(BlackFlowSpireRuntime.State, state) && BlackFlowSpireRuntime.CanContinueRun
        && screen.IsOpen && !BlackFlowSpireMapUi.IsInspection(screen) && !BlackFlowSpireRuntime.IsTraveling
        && !CombatManager.Instance.IsInProgress && BlackFlowSpireTeam.CanOperate
        && BlackFlowSpireGenerationService.GetProgress(state) is { PendingNodeId: null, HuntPhase: BlackFlowSpireHuntPhase.None };

    internal static void Dismiss(NMapScreen screen)
    {
        if (NModalContainer.Instance is { OpenModal: BlackFlowSpireMovementModeScreen mode } && ReferenceEquals(mode._screen, screen))
            mode.ClearModal();
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _host = GetParent() as Control;
        _viewport = GetViewport();
        if (_host != null) _host.Resized += FitViewport;
        _viewport.SizeChanged += FitViewport;
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;

        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Map/BlackFlowSpireMovementMode.tscn").Instantiate<Control>();
        AddChild(_view);
        _view.GetNode<Button>("Outside").Pressed += () => Close(exhaust: false);
        Finish.Pressed += OpenConfirmation;
        Confirm.GetNode<Button>("Card/Cancel").Pressed += CancelConfirmation;
        Confirm.GetNode<Button>("Card/Accept").Pressed += () => Close(exhaust: true);
        var inventory = Inventory;
        _selected = inventory.LoadedPartId ?? Walk;
        var progress = BlackFlowSpireGenerationService.GetProgress(state);
        _view.Call("bind_header", BlackFlowSpireText.Get("UI.MOVEMENT.title"), BlackFlowSpireText.Get("UI.AP.caption"),
            progress.ActionPointsRemaining, BlackFlowSpireText.Get("UI.MOVEMENT.warning"), BlackFlowSpireText.Get("UI.MOVEMENT.exhaust"),
            BlackFlowSpireText.Get("UI.MOVEMENT.confirm"), BlackFlowSpireText.Get("UI.MOVEMENT.cancel"));

        AddMode(Walk, BlackFlowSpireText.Get("UI.PARTS.walk"), BlackFlowSpireText.Get("UI.MOVEMENT.walk_description"), "", 0, 0);
        foreach (var part in inventory.Parts.Where(part => part.RemainingUses > 0))
        {
            var definition = BlackFlowSpireProcessedParts.Get(part.Kind);
            AddMode(part.Id, BlackFlowSpirePartsUi.Title(part.Kind), BlackFlowSpirePartsUi.Description(part.Kind), BlackFlowSpireShopParts.IconId(part.Kind),
                part.RemainingUses, definition.Uses);
        }
        if (!_rows.ContainsKey(_selected)) _selected = Walk;
        UpdateSelection();
        Finish.Disabled = !CanExhaust();
        Finish.Modulate = new Color(1, 1, 1, Finish.Disabled ? 0.4f : 1f);
        _animation = _view.Call("animate", true).As<Tween>();
    }

    private void FitViewport()
    {
        var area = GetViewportRect();
        var inverse = _host?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        Position = inverse * area.Position;
        Size = inverse * area.End - Position;
    }

    private void AddMode(int id, string title, string description, string icon, int uses, int max)
    {
        var row = _view.Call("add_mode", title, description, icon, uses, max,
            BlackFlowSpireText.Get("UI.PARTS.uses", ("Count", uses)), BlackFlowSpireText.Get("UI.MOVEMENT.loaded")).As<Button>();
        row.Pressed += () => Select(id);
        _rows.Add(id, row);
    }

    private void Select(int id)
    {
        if (_closing || Confirm.Visible || id == _selected || !CanOpen(state, _screen)) return;
        _selected = id;
        _previewing = true;
        preview(Inventory.Parts.FirstOrDefault(part => part.Id == id && part.RemainingUses > 0));
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var (id, row) in _rows) row.Call("mark_selected", id == _selected);
        Panel.GetNode<ScrollContainer>("Rows").CallDeferred("ensure_control_visible", _rows[_selected]);
    }

    private bool CanExhaust() => CanOpen(state, _screen) && BlackFlowSpireTeam.CanTravel(state)
        && BlackFlowSpireHuntRules.CanExhaust(BlackFlowSpireGenerationService.GetOrGenerate(state),
            BlackFlowSpireGenerationService.GetProgress(state), Inventory);

    private void OpenConfirmation()
    {
        if (_closing || !CanExhaust()) return;
        Panel.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        Confirm.Show();
        ActiveScreenContext.Instance.Update();
        Confirm.GetNode<Button>("Card/Cancel").GrabFocus();
    }

    private void CancelConfirmation()
    {
        if (_closing) return;
        Confirm.Hide();
        Panel.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Inherited;
        ActiveScreenContext.Instance.Update();
        Finish.GrabFocus();
    }

    private void Close(bool exhaust)
    {
        if (_closing || (exhaust ? !Confirm.Visible || !CanExhaust() : Confirm.Visible)) return;
        _closing = true;
        Confirm.Hide();
        Panel.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        _animation?.Kill();
        _animation = _view.Call("animate", false).As<Tween>();
        _animation!.Finished += () =>
        {
            var available = HasMap && CanOpen(state, _screen);
            var selected = _selected == Walk ? (int?)null : _selected;
            var change = available && selected != Inventory.LoadedPartId
                && (!selected.HasValue || Inventory.Parts.Any(part => part.Id == selected && part.RemainingUses > 0));
            ClearModal();
            if (available && exhaust) TaskHelper.RunSafely(BlackFlowSpireTeam.IsOnline ? BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.Exhaust) : BlackFlowSpirePartService.ExhaustActionPoints(state));
            else if (change) TaskHelper.RunSafely(CommitMode(selected));
        };
    }

    private async Task CommitMode(int? id)
    {
        if (BlackFlowSpireTeam.IsOnline) await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.Equip, id ?? -1);
        else await BlackFlowSpirePartService.Load(id);
        ActiveScreenContext.Instance.Update();
    }

    private bool HasMap => GodotObject.IsInstanceValid(widget) && widget.IsInsideTree()
        && GodotObject.IsInstanceValid(_screen) && _screen.IsOpen;

    private void ClearModal()
    {
        if (NModalContainer.Instance is not { } container || !ReferenceEquals(container.OpenModal, this)) return;
        RestoreMap();
        SetProcessInput(false);
        Hide();
        container.Clear();
    }

    private void RestoreMap()
    {
        if (!HasMap) return;
        if (_previewing && ReferenceEquals(BlackFlowSpireRuntime.State, state)) preview(Inventory.LoadedPart);
        _previewing = false;
    }

    public override void _Process(double delta)
    {
        if (!HasMap || !ReferenceEquals(BlackFlowSpireRuntime.State, state) || !BlackFlowSpireRuntime.CanContinueRun) ClearModal();
    }

    public override void _Input(InputEvent input)
    {
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        if (Confirm.Visible) CancelConfirmation(); else Close(exhaust: false);
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        if (_host != null && GodotObject.IsInstanceValid(_host)) _host.Resized -= FitViewport;
        if (_viewport != null && GodotObject.IsInstanceValid(_viewport)) _viewport.SizeChanged -= FitViewport;
        RestoreMap();
    }
}
