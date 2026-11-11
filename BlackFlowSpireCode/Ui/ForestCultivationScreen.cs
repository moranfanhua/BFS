using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace BlackFlowSpire.Ui;

internal partial class ForestCultivationScreen(RunState state, Action closed) : Control, IOverlayScreen
{
    private const double GrowthSeconds = 3.366666555404663;
    private Control _view = null!;
    private int _quantity = 1;
    private int _stage;
    private bool _busy;
    private bool _closing;
    private bool _notified;
    private ulong _started;
    private AudioStreamPlayer? _sound;
    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => false;
    public Control? DefaultFocusedControl => _view == null ? null : _stage switch
    {
        0 => _view.GetNode<Button>(SeedCount > 0 ? "Canvas/Entry/Confirm" : "Canvas/Entry/Cancel"),
        1 => _view.GetNode<Button>("Canvas/Playback/Skip"),
        _ => _view.GetNode<Button>("Canvas/Playback/Results/Confirm")
    };
    private int SeedCount => ForestSecretShop.SeedCount(state);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/ForestCultivation.tscn").Instantiate<Control>();
        AddChild(_view);
        foreach (var (name, delta) in new[] { ("Minimum", int.MinValue), ("Minus", -1), ("Plus", 1), ("Maximum", int.MaxValue) })
            _view.GetNode<Button>("Canvas/Entry/Counter/" + name).Pressed += () =>
            {
                if (_busy) return;
                _quantity = delta == int.MinValue ? 1 : delta == int.MaxValue ? Math.Max(1, SeedCount)
                    : Math.Clamp(_quantity + delta, 1, Math.Max(1, SeedCount));
                Refresh();
            };
        _view.GetNode<Button>("Canvas/Entry/Cancel").Pressed += Close;
        _view.GetNode<Button>("Canvas/Entry/Confirm").Pressed += () => TaskHelper.RunSafely(Confirm());
        _view.GetNode<Button>("Canvas/Playback/Skip").Pressed += ShowHarvest;
        _view.GetNode<Button>("Canvas/Playback/Results/Confirm").Pressed += () => TaskHelper.RunSafely(Acknowledge());
        ForestLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
        if (ForestSecretShop.GetStock(state).PendingHarvest.Count > 0) ShowHarvest();
    }

    private void Refresh()
    {
        if (_closing || !IsInsideTree()) return;
        ForestLocalization.BindStaticText(_view);
        _view.GetNode<Label>("Canvas/Entry/Counter/Number").Text = _quantity.ToString();
        _view.GetNode<Label>("Canvas/Entry/Stock").Text = ForestText.Get("UI.SECRET_SHOP.stock", ("Count", SeedCount));
        foreach (var child in _view.GetNode<Control>("Canvas/Entry/Counter").GetChildren().OfType<Button>())
            child.Disabled = _busy || SeedCount == 0 || (child.Name.ToString() is "Minimum" or "Minus" ? _quantity <= 1 : _quantity >= SeedCount);
        _view.GetNode<Button>("Canvas/Entry/Confirm").Disabled = _busy || SeedCount == 0;
        _view.GetNode<Button>("Canvas/Entry/Confirm").Modulate = SeedCount == 0 ? new Color(0.4f, 0.4f, 0.4f) : Colors.White;
        _view.GetNode<Button>("Canvas/Entry/Cancel").Disabled = _busy;
        _view.GetNode<Button>("Canvas/Playback/Results/Confirm").Disabled = _busy;
        if (_stage == 2) BindHarvest();
    }

    private async Task Confirm()
    {
        if (_busy || _stage != 0 || SeedCount < _quantity) return;
        _busy = true;
        Refresh();
        try
        {
            await ForestSecretShop.Cultivate(state, _quantity);
            if (_closing || !IsInsideTree()) return;
            _stage = 1;
            _view.Call("set_stage", 1);
            _view.GetNode<Control>("Canvas/Playback/Growth").Call("sample", 0.0);
            _view.GetNode<Control>("Canvas/Playback/Growth").Call("start_particles");
            _started = Time.GetTicksMsec();
            _sound = new AudioStreamPlayer { Stream = ResourceLoader.Load<AudioStream>("res://BlackFlowSpire/Audio/cultivation.wav") };
            var settings = SaveManager.Instance.SettingsSave;
            _sound.VolumeLinear = settings.VolumeMaster * settings.VolumeSfx;
            AddChild(_sound);
            _sound.Play();
        }
        catch (Exception error)
        {
            Entry.Logger.Error("[Forest] Cultivation failed: " + error);
            // If a checkpoint failed after committing the in-memory receipt,
            // show that harvest; do not charge seeds a second time.
            if (!_closing && ForestSecretShop.GetStock(state).PendingHarvest.Count > 0) ShowHarvest();
        }
        finally
        {
            _busy = false;
            if (!_closing && GodotObject.IsInstanceValid(this)) { Refresh(); ActiveScreenContext.Instance.Update(); }
        }
    }

    public override void _Process(double delta)
    {
        if (_stage != 1 || _closing) return;
        var time = Math.Min(GrowthSeconds, (Time.GetTicksMsec() - _started) / 1000.0);
        _view.GetNode<Control>("Canvas/Playback/Growth").Call("sample", time);
        if (time >= GrowthSeconds) ShowHarvest();
    }

    private void ShowHarvest()
    {
        if (_closing || _stage == 2) return;
        _stage = 2;
        _view.GetNode<Control>("Canvas/Playback/Growth").Call("sample", GrowthSeconds);
        _view.GetNode<Control>("Canvas/Playback/Growth").Call("stop_particles");
        BindHarvest();
        _view.Call("show_results");
        ActiveScreenContext.Instance.Update();
    }

    private void BindHarvest()
    {
        var cards = _view.GetNode<HBoxContainer>("Canvas/Playback/Results/Scroll/Center/Cards");
        foreach (var child in cards.GetChildren()) { cards.RemoveChild(child); child.QueueFree(); }
        var held = ForestShopSaleItem.Get(state).ToDictionary(part => part.Id);
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/ForestHarvestCard.tscn");
        foreach (var id in ForestSecretShop.GetStock(state).PendingHarvest)
        {
            if (!held.TryGetValue(id, out var part)) continue;
            var card = scene.Instantiate<Control>();
            cards.AddChild(card);
            card.GetNode<Label>("Category").Text = ForestText.Get("UI.SECRET_SHOP.category_" + part.Category);
            card.GetNode<Label>("Title").Text = part.Title;
            card.GetNode<RichTextLabel>("Description").Text = part.BatchDescription;
            card.GetNode<Label>("Price/Value").Text = part.Gold.ToString();
            var icon = card.GetNode<Control>("Icon");
            icon.Call("bind_part", part.IconId, part.Category, part.Uses, part.MaxUses, false);
            icon.GetNode<Control>("Type").Position = new Vector2(-36, -37);
            icon.GetNode<Control>("Type").Size = new Vector2(30, 30);
        }
    }

    private async Task Acknowledge()
    {
        if (_busy || _stage != 2) return;
        _busy = true;
        Refresh();
        try { await ForestSecretShop.AcknowledgeHarvest(state); _busy = false; Close(); }
        catch (Exception error) { _busy = false; Entry.Logger.Error("[Forest] Harvest acknowledgement failed: " + error); Refresh(); }
    }

    private void Close()
    {
        if (_busy || _closing || _stage == 1) return;
        _closing = true;
        NOverlayStack.Instance?.Remove(this);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        if (_stage == 0) Close();
        else if (_stage == 1) ShowHarvest();
        else TaskHelper.RunSafely(Acknowledge());
        GetViewport().SetInputAsHandled();
    }
    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed()
    {
        if (!_notified) { _notified = true; closed(); }
        this.QueueFreeSafely();
    }
    public void AfterOverlayShown() => Show();
    public void AfterOverlayHidden() => Hide();
}
