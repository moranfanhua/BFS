using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

// A single run-owned footer, shared by the map, trader and other exploration rooms.
internal partial class ForestPartsBar(NGlobalUi globalUi, RunState state) : Control
{
    private Control _view = null!;
    private (long Value, int Count, int Capacity)? _shown;
    private double _refreshIn;

    public static void Attach(NGlobalUi globalUi, RunState state)
    {
        if (globalUi.GetNodeOrNull("BlackFlowSpirePartsBar") != null) return;
        globalUi.AddChild(new ForestPartsBar(globalUi, state)
        {
            Name = "BlackFlowSpirePartsBar",
            ZIndex = Math.Max(0, globalUi.MapScreen.ZIndex)
        });
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/ForestPartsBar.tscn").Instantiate<Control>();
        AddChild(_view);
        _view.GetNode<Button>("Canvas/PartsBox").Pressed += () => ForestPartsUi.OpenBox(state);
        ForestLocalization.BindStaticText(_view);
        ForestLocalization.RefreshWithLocale(this, () => { ForestLocalization.BindStaticText(_view); RefreshBox(); });
        ActiveScreenContext.Instance.Updated += RefreshVisibility;
        RefreshVisibility();
        RefreshBox();
    }

    public override void _Process(double delta)
    {
        RefreshVisibility();
        if (!Visible) return;
        _refreshIn -= delta;
        if (_refreshIn > 0) return;
        _refreshIn = 0.1;
        RefreshBox();
    }

    private void RefreshVisibility()
    {
        if (!IsNodeReady()) return;
        // Keep selection screens, battle controls and the native pause/menu layers clear.
        var mapOpen = globalUi.MapScreen.IsOpen;
        Visible = ReferenceEquals(ForestRuntime.State, state) && ForestRuntime.CanContinueRun
            && !ForestRuntime.IsTraveling && !CombatManager.Instance.IsInProgress
            && NModalContainer.Instance?.OpenModal == null && !globalUi.SubmenuStack.Stack.SubmenusOpen
            && (mapOpen || (globalUi.Overlays.ScreenCount == 0 && !globalUi.CapstoneContainer.InUse));
    }

    private void RefreshBox()
    {
        if (!ForestGenerationService.IsEnabled(state)) return;
        var inventory = ForestGenerationService.GetEngine(state);
        var data = (ForestPartService.TotalAppraisal(inventory), inventory.TotalPartCount, inventory.Capacity);
        if (_shown == data) return;
        _shown = data;
        _view.Call("bind_box", data.Item1, data.TotalPartCount, data.Capacity);
    }

    public override void _ExitTree() => ActiveScreenContext.Instance.Updated -= RefreshVisibility;
}
