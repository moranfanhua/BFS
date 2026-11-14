using BlackFlowSpire.Combat;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
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
    private Label _maxTargetLife = null!;
    private HBoxContainer _shield = null!;
    private Label _shieldCount = null!;
    private Label _level = null!;
    private Label _experience = null!;
    private Label _experienceGoal = null!;
    private TextureProgressBar _experienceRing = null!;
    private TextureRect _levelArrow = null!;
    private Label _gold = null!;
    private int _goldFontSize;
    private (int Region, int Life, int MaxLife, int Shield, ForestHope Hope, int Experience, int Gold)? _shown;
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
            existing._shown = null;
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
        var collectibles = _view.GetNode<Button>("Canvas/Collectibles");
        collectibles.Pressed += () => ForestCollectibleScreen.Open(_state);
        _floorTitle = _view.GetNode<TextureRect>("Canvas/FloorTitle");
        _targetLife = _view.GetNode<Label>("Canvas/LifeValue/Current");
        _maxTargetLife = _view.GetNode<Label>("Canvas/LifeValue/Maximum");
        _shield = _view.GetNode<HBoxContainer>("Canvas/LifeValue/Shield");
        _shieldCount = _shield.GetNode<Label>("Count");
        _level = _view.GetNode<Label>("Canvas/Level");
        _experience = _view.GetNode<Label>("Canvas/Experience/Current");
        _experienceGoal = _view.GetNode<Label>("Canvas/Experience/Maximum");
        _experienceRing = _view.GetNode<TextureProgressBar>("Canvas/ExpFill");
        _levelArrow = _view.GetNode<TextureRect>("Canvas/LevelArrow");
        _gold = _view.GetNode<Label>("Canvas/Gold");
        _goldFontSize = _gold.GetThemeFontSize("font_size");
        void RefreshText()
        {
            ForestLocalization.BindStaticText(_view);
            collectibles.TooltipText = ForestText.Get("UI.COLLECTIBLES.title");
        }
        RefreshText();
        ForestLocalization.RefreshWithLocale(this, RefreshText);
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
                && (globalUi.MapScreen.IsOpen || ForestBattleSettlementScreen.LootIsOpen
                    || (globalUi.Overlays.ScreenCount == 0 && !globalUi.CapstoneContainer.InUse));
            if (_view.Visible) RefreshValues();
        }
        finally { _updating = false; }
    }

    private void RefreshValues()
    {
        var region = ForestMapAssets.RegionNumber(_state.CurrentActIndex);
        var (life, maxLife, shield) = ForestGenerationService.GetTargetLife(_state);
        var hope = ForestGenerationService.GetHopeStatus(_state);
        var experience = ForestGenerationService.GetCommandExperience(_state);
        var gold = LocalContext.GetMe(_state)?.Gold ?? 0;
        if (_shown?.Region != region)
            _floorTitle.Texture = ForestMapAssets.Texture($"res://BlackFlowSpire/Images/Ui/TopBar/zone-{region}.png");
        if (_shown?.Life != life) _targetLife.Text = life.ToString();
        if (_shown?.MaxLife != maxLife) _maxTargetLife.Text = $"/{maxLife}";
        if (_shown?.Shield != shield)
        {
            _shieldCount.Text = shield.ToString();
            _shield.Visible = shield > 0;
        }
        if (_shown?.Hope != hope) _view.Call("set_hope", hope.Balance, hope.Maximum, _shown.HasValue);
        if (_shown?.Experience != experience)
        {
            var command = ForestCommandLevel.FromExperience(experience);
            var hasNextLevel = command.ExperienceToNext > 0;
            _level.Text = command.Level.ToString();
            _experience.Text = hasNextLevel ? command.Experience.ToString() : "";
            _experienceGoal.Text = hasNextLevel ? $"/{command.ExperienceToNext}" : "";
            _experienceRing.Value = hasNextLevel ? (double)command.Experience / command.ExperienceToNext : 1;
            _levelArrow.Visible = hasNextLevel;
        }
        if (_shown?.Gold != gold) SetCounterText(_gold, gold, _goldFontSize);
        _shown = (region, life, maxLife, shield, hope, experience, gold);
    }

    private static void SetCounterText(Label label, int value, int normalFontSize)
    {
        label.Text = value.ToString();
        var width = label.GetThemeFont("font").GetStringSize(label.Text, fontSize: normalFontSize).X;
        var fontSize = (int)Math.Floor(normalFontSize * Math.Min(1f, label.Size.X / width));
        label.AddThemeFontSizeOverride("font_size", Math.Max(1, fontSize));
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
