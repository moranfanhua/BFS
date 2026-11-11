using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

// The warehouse is modal so it remains above the map, sharing the same run inventory.
internal partial class ForestPartsSelectionScreen(RunState state, bool inspection) : Control, IScreenContext
{
    private Control _view = null!;
    private readonly Dictionary<int, Button> _cards = [];
    private ForestShopSaleItem[] _parts = [];
    private int? _selected;
    private bool _sortValue;
    private bool _descending;
    private bool _busy;
    private bool _closed;
    private Viewport? _layoutViewport;
    private Control? _layoutParent;

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public Control? DefaultFocusedControl => _view == null ? null : Detail.Visible
        ? Detail.GetNode<Button>("Panel/Back") : _cards.Values.FirstOrDefault() ?? Collapse;
    private Control Detail => _view.GetNode<Control>("Detail");
    private Button Collapse => _view.GetNode<Button>("Footer/Canvas/PartsBox");
    private ForestShopSaleItem? Selected => _parts.FirstOrDefault(part => part.Id == _selected);

    public override void _Ready()
    {
        // The native modal host may reserve HUD space. Fit the full visible
        // viewport instead of inheriting that inset or the host's offset.
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _layoutViewport = GetViewport();
        _layoutViewport.SizeChanged += FitViewport;
        _layoutParent = GetParent() as Control;
        if (_layoutParent != null) _layoutParent.Resized += FitViewport;
        FitViewport();
        MouseFilter = MouseFilterEnum.Stop;
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/ForestWarehouse.tscn").Instantiate<Control>();
        AddChild(_view);
        Collapse.Pressed += Close;
        _view.GetNode<Button>("Canvas/Sort/Type").Pressed += () => Sort(false);
        _view.GetNode<Button>("Canvas/Sort/Value").Pressed += () => Sort(true);
        _view.GetNode<Button>("Canvas/EyeButton").Pressed += () =>
        {
            if (ForestGenerationService.GetEngine(state).LoadedPartId is { } id) Select(id);
        };
        Detail.GetNode<Button>("Panel/Back").Pressed += CloseDetail;
        Detail.GetNode<Button>("Panel/Previous").Pressed += () => Cycle(-1);
        Detail.GetNode<Button>("Panel/Next").Pressed += () => Cycle(1);
        Detail.GetNode<Button>("Panel/Actions/Equip").Pressed += () =>
        {
            if (Selected is not { Category: "processed" } part) return;
            TaskHelper.RunSafely(Apply(() => ForestPartService.Load(part.Loaded ? null : part.Id)));
        };
        Detail.GetNode<Button>("Panel/Actions/Discard").Pressed += () =>
        {
            if (Selected is not { } part) return;
            TaskHelper.RunSafely(Apply(() => part.Category switch
            {
                "natural" => ForestNaturalService.Discard(part.Id),
                "concept" => ForestConceptService.Discard(part.Id),
                _ => ForestPartService.Discard(part.Id)
            }));
        };
        Detail.GetNode<Button>("Panel/Actions/Sell").Pressed += () =>
        {
            if (Selected is { } part) TaskHelper.RunSafely(Apply(() => ForestPartService.Sell(part.Id), closeBefore: true));
        };
        Detail.GetNode<Button>("Panel/Actions/Use").Pressed += () =>
            TaskHelper.RunSafely(Apply(ForestPartService.UseRandomPart, closeBefore: true));
        ForestLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    private void FitViewport()
    {
        if (!IsInsideTree()) return;
        var area = GetViewportRect();
        var inverse = _layoutParent?.GetGlobalTransformWithCanvas().AffineInverse() ?? Transform2D.Identity;
        var start = inverse * area.Position;
        Position = start;
        Size = inverse * area.End - start;
    }

    private void Sort(bool value)
    {
        if (_busy) return;
        _descending = _sortValue == value ? !_descending : value;
        _sortValue = value;
        Refresh();
    }

    private void Refresh()
    {
        if (_closed || !IsInsideTree()) return;
        ForestLocalization.BindStaticText(_view);
        var inventory = ForestGenerationService.GetEngine(state);
        var items = ForestShopSaleItem.Get(state);
        static int Category(ForestShopSaleItem part) => part.Category switch { "processed" => 0, "natural" => 1, _ => 2 };
        _parts = (_sortValue ? (_descending ? items.OrderByDescending(part => part.Appraisal) : items.OrderBy(part => part.Appraisal))
            : (_descending ? items.OrderByDescending(Category) : items.OrderBy(Category))).ThenBy(part => part.Id).ToArray();
        var columns = _view.GetNode<HBoxContainer>("Canvas/Items/Padding/Columns");
        foreach (var child in columns.GetChildren()) { columns.RemoveChild(child); child.QueueFree(); }
        _cards.Clear();
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/ForestWarehouseCard.tscn");
        var categories = new Godot.Collections.Array<string>();
        foreach (var part in _parts) categories.Add(part.Category);
        var loadedIcon = _parts.FirstOrDefault(part => part.Loaded)?.IconId ?? "";
        _view.Call("bind_inventory", ForestPartService.TotalAppraisal(inventory), inventory.TotalPartCount,
            inventory.Capacity, categories, loadedIcon);
        _view.Call("bind_sort", _sortValue, _descending);
        var slots = Math.Max(inventory.Capacity, _parts.Length);
        VBoxContainer? column = null;
        for (var index = 0; index < slots; index++)
        {
            if (index % 3 == 0)
            {
                column = new VBoxContainer();
                column.AddThemeConstantOverride("separation", 10);
                columns.AddChild(column);
            }
            var card = scene.Instantiate<Button>();
            column!.AddChild(card);
            ForestLocalization.BindStaticText(card);
            if (index >= _parts.Length) { card.Call("bind_empty"); continue; }
            var part = _parts[index];
            card.Call("bind_part", part.Title, part.Activations.HasValue ? part.BatchDescription : part.Description, part.IconId, part.Category,
                part.Uses, part.MaxUses, part.Loaded, part.Appraisal);
            _cards.Add(part.Id, card);
            card.Pressed += () => Select(part.Id);
        }
        var buttons = _cards.Values.ToArray();
        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            button.FocusNeighborLeft = (index >= 3 ? buttons[index - 3] : Collapse).GetPath();
            button.FocusNeighborRight = (index + 3 < buttons.Length ? buttons[index + 3] : button).GetPath();
            button.FocusNeighborTop = (index % 3 > 0 ? buttons[index - 1] : _view.GetNode<Button>("Canvas/Sort/Type")).GetPath();
            button.FocusNeighborBottom = (index % 3 < 2 && index + 1 < buttons.Length ? buttons[index + 1] : Collapse).GetPath();
        }
        RefreshDetail();
    }

    private void Select(int id)
    {
        if (_busy || !_cards.ContainsKey(id)) return;
        var opening = !Detail.Visible;
        _selected = id;
        RefreshDetail();
        if (opening) Detail.Call("enter");
        ActiveScreenContext.Instance.Update();
    }

    private void Cycle(int direction)
    {
        if (_busy || Selected is not { } part || Detail.Call("is_switching").AsBool()) return;
        var index = Array.IndexOf(_parts, part) + direction;
        if (index < 0 || index >= _parts.Length) return;
        var id = _parts[index].Id;
        Detail.Call("switch_part", direction, Callable.From(() =>
        {
            if (_closed || !_cards.ContainsKey(id)) return;
            _selected = id;
            RefreshDetail(animateActions: true);
            ActiveScreenContext.Instance.Update();
        }));
    }

    private void RefreshDetail(bool animateActions = false)
    {
        var part = Selected;
        Detail.Visible = part != null;
        if (part == null) { _selected = null; return; }
        var flavor = LocString.GetIfExists("gameplay_ui", ForestText.Prefix + ForestText.PartKey(part.IconId) + ".flavor")
            ?.GetFormattedText() ?? "";
        Detail.Call("bind_part", part.Title, part.Activations.HasValue ? part.BatchDescription : part.Description,
            part.IconId, part.Category, part.Uses, part.MaxUses, part.Loaded, part.Appraisal,
            ForestText.Get("UI.SECRET_SHOP.category_" + part.Category),
            ForestText.Get("UI.PARTS.uses", ("Count", part.Uses)), flavor);
        var index = Array.IndexOf(_parts, part);
        Detail.GetNode<Button>("Panel/Previous").Visible = index > 0;
        Detail.GetNode<Button>("Panel/Next").Visible = index + 1 < _parts.Length;
        var equip = Detail.GetNode<Button>("Panel/Actions/Equip");
        equip.Visible = part.Category == "processed" && !ForestTeam.IsGuest;
        equip.GetNode<Label>("Text").Text = ForestText.Get(part.Loaded ? "UI.PARTS.detail_unequip" : "UI.PARTS.detail_equip");
        Detail.GetNode<Button>("Panel/Actions/Sell").Visible = state.CurrentRoom is MerchantRoom && !ForestTeam.IsGuest;
        Detail.GetNode<Label>("Panel/Actions/Sell/Text").Text = ForestText.Get("UI.PARTS.sell_gold", ("Gold", part.Gold));
        var processed = ForestGenerationService.GetEngine(state).Parts.FirstOrDefault(item => item.Id == part.Id);
        var use = Detail.GetNode<Button>("Panel/Actions/Use");
        use.Visible = processed != null && part.Loaded && !ForestTeam.IsGuest && ForestProcessedParts.Get(processed.Kind).RandomDestination;
        if (use.Visible)
        {
            var progress = ForestGenerationService.GetProgress(state);
            use.Disabled = _busy || inspection || !ForestTeam.CanTravel(state) || ForestGenerationService.GetEngine(state).IsOverloaded
                || progress.PendingNodeId.HasValue || progress.HuntPhase != ForestHuntPhase.None;
        }
        foreach (var button in Detail.GetNode<Control>("Panel/Actions").GetChildren().OfType<Button>())
            if (button != use) button.Disabled = _busy;
        Detail.GetNode<Button>("Panel/Back").Disabled = _busy;
        Detail.Call("layout_actions", animateActions);
    }

    internal void RefreshShared()
    {
        if (!_busy && !_closed && IsNodeReady()) Refresh();
    }

    private async Task Apply(Func<Task> action, bool closeBefore = false)
    {
        if (_busy || _closed || Detail.Call("is_switching").AsBool() || !ForestRuntime.CanContinueRun
            || ForestRuntime.IsTraveling || !ForestTeam.CanManage(state)) return;
        if (closeBefore)
        {
            Close();
            Callable.From(() => { TaskHelper.RunSafely(action()); }).CallDeferred();
            return;
        }
        _busy = true;
        RefreshDetail();
        try { await action(); }
        catch (Exception error) { Entry.Logger.Error("[Forest] Warehouse action failed: " + error); }
        finally
        {
            if (!_closed && GodotObject.IsInstanceValid(this) && IsInsideTree())
            {
                _busy = false;
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }

    private void CloseDetail()
    {
        if (_busy) return;
        var id = _selected;
        _selected = null;
        Detail.Hide();
        ActiveScreenContext.Instance.Update();
        if (id.HasValue && _cards.TryGetValue(id.Value, out var card)) card.GrabFocus();
    }

    private void Close()
    {
        if (_busy || _closed) return;
        _closed = true;
        if (NModalContainer.Instance is { } container && ReferenceEquals(container.OpenModal, this)) container.Clear();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        if (Detail.Visible) CloseDetail(); else Close();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        _closed = true;
        if (_layoutViewport != null && GodotObject.IsInstanceValid(_layoutViewport))
            _layoutViewport.SizeChanged -= FitViewport;
        if (_layoutParent != null && GodotObject.IsInstanceValid(_layoutParent))
            _layoutParent.Resized -= FitViewport;
    }
}
