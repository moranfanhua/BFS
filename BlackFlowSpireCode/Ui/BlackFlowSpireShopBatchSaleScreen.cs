using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using BlackFlowSpire.Shop;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireShopBatchSaleScreen(RunState state, Func<IReadOnlyList<int>, Task<bool>> sell) : Control, IOverlayScreen
{
    private Control _layout = null!;
    private readonly Dictionary<int, Button> _tiles = [];
    private readonly HashSet<int> _selected = [];
    private readonly List<int> _selectionOrder = [];
    private IReadOnlyList<BlackFlowSpireShopSaleItem> _parts = [];
    private bool _busy;
    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => false;
    public Control? DefaultFocusedControl => _tiles.Values.FirstOrDefault() ?? _layout?.GetNode<Button>("Canvas/Panel/Cancel");

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _layout = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopBatchSale.tscn").Instantiate<Control>();
        AddChild(_layout);
        if (BlackFlowSpireSecretShop.IsCurrent(state))
            _layout.GetNode<TextureRect>("Canvas/Background").Texture = ResourceLoader.Load<Texture2D>("res://BlackFlowSpire/Images/Shop/Secret/bg_scrap_shop.png");
        BlackFlowSpireLocalization.BindStaticText(_layout);
        _parts = BlackFlowSpireShopSaleItem.Get(state);
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopGoodsTile.tscn");
        var grid = _layout.GetNode<GridContainer>("Canvas/Goods/Padding/Items");
        foreach (var part in _parts)
        {
            var button = scene.Instantiate<Button>();
            grid.AddChild(button);
            part.Bind(button);
            _tiles.Add(part.Id, button);
            button.Pressed += () => Toggle(part.Id);
            button.MouseEntered += () => BlackFlowSpireShopController.ShowSaleTip(button, part);
            button.FocusEntered += () => BlackFlowSpireShopController.ShowSaleTip(button, part);
            button.MouseExited += () => NHoverTipSet.Remove(button);
            button.FocusExited += () => NHoverTipSet.Remove(button);
        }
        _layout.GetNode<Button>("Canvas/Reset").Pressed += () => { if (!_busy) { _selected.Clear(); _selectionOrder.Clear(); RefreshSelection(); } };
        _layout.GetNode<Button>("Canvas/Panel/Cancel").Pressed += Close;
        _layout.GetNode<Button>("Canvas/Panel/Confirm").Pressed += () => TaskHelper.RunSafely(Confirm());
        RefreshSelection();
        BlackFlowSpireLocalization.RefreshWithLocale(this, () => { BlackFlowSpireLocalization.BindStaticText(_layout); RefreshSelection(); });
    }

    private void RefreshSelection()
    {
        var list = _layout.GetNode<VBoxContainer>("Canvas/Panel/Scroll/Items");
        foreach (var child in list.GetChildren()) { list.RemoveChild(child); child.QueueFree(); }
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopBatchSaleItem.tscn");
        foreach (var id in _selectionOrder)
        {
            var part = _parts.First(part => part.Id == id);
            var row = scene.Instantiate<Control>();
            list.AddChild(row);
            BlackFlowSpireLocalization.BindStaticText(row);
            row.Call("bind_item", part.Title, part.BatchDescription, part.IconId, part.Category, part.Uses, part.MaxUses, part.Loaded, part.Gold);
            var remove = row.GetNode<Button>("Remove");
            remove.Disabled = _busy;
            remove.Pressed += () => Toggle(part.Id);
        }
        foreach (var pair in _tiles)
        {
            pair.Value.Disabled = _busy;
            pair.Value.Call("set_selected", _selected.Contains(pair.Key));
        }
        var total = _parts.Where(part => _selected.Contains(part.Id)).Aggregate(0L, (sum, part) => sum + part.Gold);
        _layout.GetNode<Label>("Canvas/Panel/Confirm/Content/Total").Text = total.ToString();
        _layout.GetNode<Button>("Canvas/Panel/Confirm").Disabled = _busy || _selected.Count == 0;
        _layout.GetNode<Control>("Canvas/Panel/Confirm/Content").Modulate = new Color(1, 1, 1, _selected.Count == 0 ? 0.4f : 1);
        _layout.GetNode<Control>("Canvas/Panel/Empty").Visible = _selected.Count == 0;
        _layout.GetNode<Button>("Canvas/Reset").Disabled = _busy || _selected.Count == 0;
        _layout.GetNode<Button>("Canvas/Panel/Cancel").Disabled = _busy;
    }

    private void Toggle(int id)
    {
        if (_busy) return;
        if (_selected.Add(id)) _selectionOrder.Insert(0, id);
        else { _selected.Remove(id); _selectionOrder.Remove(id); }
        RefreshSelection();
    }

    private async Task Confirm()
    {
        if (_busy || _selected.Count == 0) return;
        _busy = true;
        foreach (var button in _tiles.Values) NHoverTipSet.Remove(button);
        RefreshSelection();
        if (await sell(_selected.Order().ToArray()))
        {
            _busy = false;
            Close();
        }
        else
        {
            // A completed sale is never replayed if a later native hook failed.
            var held = BlackFlowSpireShopSaleItem.Get(state).Select(part => part.Id).ToHashSet();
            foreach (var id in _tiles.Keys.Where(id => !held.Contains(id)).ToArray())
            {
                _tiles[id].GetParent()?.RemoveChild(_tiles[id]);
                _tiles[id].QueueFree();
                _tiles.Remove(id);
            }
            _parts = BlackFlowSpireShopSaleItem.Get(state);
            _selected.IntersectWith(held);
            _selectionOrder.RemoveAll(id => !held.Contains(id));
            _busy = false;
            RefreshSelection();
        }
    }

    private void Close()
    {
        if (!_busy) NOverlayStack.Instance?.Remove(this);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed(MegaInput.cancel) || input.IsActionPressed(MegaInput.back))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _ExitTree() { foreach (var button in _tiles.Values) if (GodotObject.IsInstanceValid(button)) NHoverTipSet.Remove(button); }
    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() => this.QueueFreeSafely();
    public void AfterOverlayShown() => Show();
    public void AfterOverlayHidden() => Hide();
}
