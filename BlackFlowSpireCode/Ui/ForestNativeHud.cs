using BlackFlowSpire.Combat;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

// Keep the original HUD state on its owning run scene, including across
// native room/overlay animations that attempt to show these controls again.
internal partial class ForestNativeHud(NGlobalUi globalUi, RunState state) : Control
{
    private const string ControllerName = "BlackFlowSpireNativeHud";
    private RunState _state = state;
    private Control[] _items = [];
    private bool[] _originalVisibility = [];
    private Control _view = null!;
    private TextureRect _floorTitle = null!;
    private Label _targetLife = null!;
    private Label _gold = null!;
    private int _goldFontSize;
    private (int Region, int Life, int Gold)? _shown;
    private bool _suppressed;
    private bool _updating;
    private bool _disposed;

    public static void Attach(NGlobalUi globalUi, RunState state)
    {
        if (globalUi.GetNodeOrNull<ForestNativeHud>(ControllerName) is { } existing)
        {
            existing._updating = true;
            existing.Restore();
            existing._state = state;
            existing._updating = false;
            existing.Refresh();
            return;
        }
        if (ForestGenerationService.IsEnabled(state))
            globalUi.AddChild(new ForestNativeHud(globalUi, state) { Name = ControllerName });
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _items = [globalUi.TopBar, globalUi.RelicInventory];
        foreach (var item in _items) item.VisibilityChanged += Refresh;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/ForestTopBar.tscn").Instantiate<Control>();
        _view.ZIndex = Math.Max(0, globalUi.MapScreen.ZIndex);
        AddChild(_view);
        _floorTitle = _view.GetNode<TextureRect>("Canvas/FloorTitle");
        _targetLife = _view.GetNode<Label>("Canvas/LifeValue/Current");
        _view.GetNode<Label>("Canvas/LifeValue/Maximum").Text = $"/{ForestCombatHealth.InitialTargetLife}";
        _gold = _view.GetNode<Label>("Canvas/Gold");
        _goldFontSize = _gold.GetThemeFontSize("font_size");
        ForestLocalization.BindStaticText(_view);
        ForestLocalization.RefreshWithLocale(this, () => ForestLocalization.BindStaticText(_view));
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        if (_disposed || _updating || _gold == null) return;
        _updating = true;
        try
        {
            if (ReferenceEquals(ForestRuntime.State, _state) && ForestGenerationService.IsEnabled(_state))
            {
                if (!_suppressed)
                {
                    _originalVisibility = _items.Select(item => item.Visible).ToArray();
                    _suppressed = true;
                }
                foreach (var item in _items)
                    if (GodotObject.IsInstanceValid(item) && item.Visible) item.Hide();
            }
            else Restore();

            _view.Visible = _suppressed && ForestRuntime.CanContinueRun
                && !CombatManager.Instance.IsInProgress && !globalUi.SubmenuStack.Stack.SubmenusOpen
                && NModalContainer.Instance?.OpenModal == null
                && (globalUi.MapScreen.IsOpen || (globalUi.Overlays.ScreenCount == 0 && !globalUi.CapstoneContainer.InUse));
            if (_view.Visible) RefreshValues();
        }
        finally { _updating = false; }
    }

    private void RefreshValues()
    {
        var region = ForestMapAssets.RegionNumber(_state.CurrentActIndex);
        var life = ForestGenerationService.GetTargetLife(_state);
        var gold = LocalContext.GetMe(_state)?.Gold ?? 0;
        if (_shown?.Region != region)
            _floorTitle.Texture = ForestMapAssets.Texture($"res://BlackFlowSpire/Images/Ui/TopBar/zone-{region}.png");
        if (_shown?.Life != life) _targetLife.Text = life.ToString();
        if (_shown?.Gold != gold)
        {
            _gold.Text = gold.ToString();
            var width = _gold.GetThemeFont("font").GetStringSize(_gold.Text, fontSize: _goldFontSize).X;
            var fontSize = (int)Math.Floor(_goldFontSize * Math.Min(1f, _gold.Size.X / width));
            _gold.AddThemeFontSizeOverride("font_size", Math.Max(1, fontSize));
        }
        _shown = (region, life, gold);
    }

    private void Restore()
    {
        if (!_suppressed) return;
        _suppressed = false;
        for (var index = 0; index < _items.Length; index++)
            if (GodotObject.IsInstanceValid(_items[index]) && !_items[index].IsQueuedForDeletion())
                _items[index].Visible = _originalVisibility[index];
    }

    public override void _ExitTree()
    {
        _disposed = true;
        foreach (var item in _items)
            if (GodotObject.IsInstanceValid(item)) item.VisibilityChanged -= Refresh;
        Restore();
    }
}
