using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

// A single run-owned footer, shared by the map, trader and other exploration rooms.
internal partial class BlackFlowSpirePartsBar(NGlobalUi globalUi, RunState state) : Control
{
    private Control _view = null!;
    private BlackFlowSpireCollectibleBar _collectibles = null!;
    private (long Value, int Count, int Capacity)? _shown;
    private double _refreshIn;

    public static void Attach(NGlobalUi globalUi, RunState state)
    {
        if (globalUi.GetNodeOrNull("BlackFlowSpirePartsBar") != null) return;
        globalUi.AddChild(new BlackFlowSpirePartsBar(globalUi, state)
        {
            Name = "BlackFlowSpirePartsBar",
            ZIndex = Math.Max(0, globalUi.MapScreen.ZIndex)
        });
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/BlackFlowSpirePartsBar.tscn").Instantiate<Control>();
        AddChild(_view);
        _collectibles = new BlackFlowSpireCollectibleBar(_view, ToggleCollectibles,
            item => BlackFlowSpireCollectibleScreen.OpenOwnedDetail(state, item));
        _view.GetNode<Button>("Canvas/PartsBox").Pressed += () =>
        {
            _collectibles.Close(immediate: true);
            BlackFlowSpirePartsUi.OpenBox(state);
        };
        BlackFlowSpireLocalization.BindStaticText(_view);
        BlackFlowSpireLocalization.RefreshWithLocale(this, () =>
        {
            BlackFlowSpireLocalization.BindStaticText(_view);
            _collectibles.RefreshText();
        });
        ActiveScreenContext.Instance.Updated += RefreshVisibility;
        RefreshVisibility();
        if (Visible) RefreshContents();
    }

    public override void _Process(double delta)
    {
        RefreshVisibility();
        if (!Visible) return;
        _refreshIn -= delta;
        if (_refreshIn > 0) return;
        _refreshIn = 0.1;
        RefreshContents();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !_collectibles.IsOpen || NModalContainer.Instance?.OpenModal != null) return;
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        _collectibles.Close();
        GetViewport().SetInputAsHandled();
    }

    private void ToggleCollectibles()
    {
        if (!IsVisibleInTree() || NModalContainer.Instance?.OpenModal != null) return;
        if (_collectibles.IsOpen) _collectibles.Close();
        else
        {
            _collectibles.Refresh(BlackFlowSpireCollectibleService.GetInventory(state).OwnedIds);
            _collectibles.Open();
        }
    }

    private void RefreshVisibility()
    {
        if (!IsNodeReady()) return;
        // Keep selection screens, battle controls and the native pause/menu layers clear.
        var mapOpen = globalUi.MapScreen.IsOpen;
        var modal = NModalContainer.Instance?.OpenModal;
        var inspectingCollectible = modal is BlackFlowSpireCollectibleScreen { IsOwnedDetail: true };
        Visible = ReferenceEquals(BlackFlowSpireRuntime.State, state) && BlackFlowSpireRuntime.CanContinueRun
            && !BlackFlowSpireRuntime.IsTraveling && !CombatManager.Instance.IsInProgress
            && (modal == null || inspectingCollectible) && !globalUi.SubmenuStack.Stack.SubmenusOpen
            && (mapOpen || BlackFlowSpireBattleSettlementScreen.LootIsOpen
                || (globalUi.Overlays.ScreenCount == 0 && !globalUi.CapstoneContainer.InUse));
        if (!Visible) _collectibles.Close(immediate: true);
    }

    private void RefreshContents()
    {
        if (!BlackFlowSpireGenerationService.IsEnabled(state)) return;
        var inventory = BlackFlowSpireGenerationService.GetEngine(state);
        _collectibles.Refresh(inventory.Collectibles.OwnedIds);
        var data = (BlackFlowSpirePartService.TotalAppraisal(inventory), inventory.TotalPartCount, inventory.Capacity);
        if (_shown == data) return;
        _shown = data;
        _view.Call("bind_box", data.Item1, data.TotalPartCount, data.Capacity);
    }

    public override void _ExitTree() => ActiveScreenContext.Instance.Updated -= RefreshVisibility;
}
