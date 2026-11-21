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
internal partial class BlackFlowSpirePartsSelectionScreen(RunState state, bool inspection) : Control, IScreenContext
{
    private Control _view = null!;
    private readonly Dictionary<int, Button> _cards = [];
    private BlackFlowSpireShopSaleItem[] _parts = [];
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
    private BlackFlowSpireShopSaleItem? Selected => _parts.FirstOrDefault(part => part.Id == _selected);

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
        _view = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/BlackFlowSpireWarehouse.tscn").Instantiate<Control>();
        AddChild(_view);
        Collapse.Pressed += Close;
        _view.GetNode<Button>("Canvas/Sort/Type").Pressed += () => Sort(false);
        _view.GetNode<Button>("Canvas/Sort/Value").Pressed += () => Sort(true);
        _view.GetNode<Button>("Canvas/EyeButton").Pressed += () =>
        {
            if (BlackFlowSpireGenerationService.GetEngine(state).LoadedPartId is { } id) Select(id);
        };
        Detail.GetNode<Button>("Panel/Back").Pressed += CloseDetail;
        Detail.GetNode<Button>("Panel/Previous").Pressed += () => Cycle(-1);
        Detail.GetNode<Button>("Panel/Next").Pressed += () => Cycle(1);
        Detail.GetNode<Button>("Panel/Actions/Equip").Pressed += () =>
        {
            if (Selected is not { Category: "processed" } part) return;
            TaskHelper.RunSafely(Apply(() => BlackFlowSpireTeam.IsOnline ? BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.Equip, part.Loaded ? -1 : part.Id) : BlackFlowSpirePartService.Load(part.Loaded ? null : part.Id)));
        };
        Detail.GetNode<Button>("Panel/Actions/Discard").Pressed += () =>
        {
            if (Selected is not { } part) return;
            TaskHelper.RunSafely(Apply(() => BlackFlowSpireTeam.IsOnline ? BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.Discard, part.Id) : part.Category switch
            {
                "natural" => BlackFlowSpireNaturalService.Discard(part.Id),
                "concept" => BlackFlowSpireConceptService.Discard(part.Id),
                _ => BlackFlowSpirePartService.Discard(part.Id)
            }));
        };
        Detail.GetNode<Button>("Panel/Actions/Sell").Pressed += () =>
        {
            if (Selected is { } part) TaskHelper.RunSafely(Apply(() => BlackFlowSpireTeam.IsOnline ? BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.SellParts, items: [part.Id]) : BlackFlowSpirePartService.Sell(part.Id), closeBefore: true));
        };
        Detail.GetNode<Button>("Panel/Actions/Use").Pressed += () =>
            TaskHelper.RunSafely(Apply(BlackFlowSpirePartService.UseRandomPart, closeBefore: true));
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
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
        BlackFlowSpireLocalization.BindStaticText(_view);
        var inventory = BlackFlowSpireGenerationService.GetEngine(state);
        var items = BlackFlowSpireShopSaleItem.Get(state);
        static int Category(BlackFlowSpireShopSaleItem part) => part.Category switch { "processed" => 0, "natural" => 1, _ => 2 };
        _parts = (_sortValue ? (_descending ? items.OrderByDescending(part => part.Appraisal) : items.OrderBy(part => part.Appraisal))
            : (_descending ? items.OrderByDescending(Category) : items.OrderBy(Category))).ThenBy(part => part.Id).ToArray();
        var columns = _view.GetNode<HBoxContainer>("Canvas/Items/Padding/Columns");
        foreach (var child in columns.GetChildren()) { columns.RemoveChild(child); child.QueueFree(); }
        _cards.Clear();
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Ui/BlackFlowSpireWarehouseCard.tscn");
        var categories = new Godot.Collections.Array<string>();
        foreach (var part in _parts) categories.Add(part.Category);
        var loadedIcon = _parts.FirstOrDefault(part => part.Loaded)?.IconId ?? "";
        _view.Call("bind_inventory", BlackFlowSpirePartService.TotalAppraisal(inventory), inventory.TotalPartCount,
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
            BlackFlowSpireLocalization.BindStaticText(card);
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
        var flavor = LocString.GetIfExists("gameplay_ui", BlackFlowSpireText.Prefix + BlackFlowSpireText.PartKey(part.IconId) + ".flavor")
            ?.GetFormattedText() ?? "";
        Detail.Call("bind_part", part.Title, part.Activations.HasValue ? part.BatchDescription : part.Description,
            part.IconId, part.Category, part.Uses, part.MaxUses, part.Loaded, part.Appraisal,
            BlackFlowSpireText.Get("UI.SECRET_SHOP.category_" + part.Category),
            BlackFlowSpireText.Get("UI.PARTS.uses", ("Count", part.Uses)), flavor);
        var index = Array.IndexOf(_parts, part);
        Detail.GetNode<Button>("Panel/Previous").Visible = index > 0;
        Detail.GetNode<Button>("Panel/Next").Visible = index + 1 < _parts.Length;
        var equip = Detail.GetNode<Button>("Panel/Actions/Equip");
        equip.Visible = part.Category == "processed" && BlackFlowSpireTeam.CanOperate;
        equip.GetNode<Label>("Text").Text = BlackFlowSpireText.Get(part.Loaded ? "UI.PARTS.detail_unequip" : "UI.PARTS.detail_equip");
        Detail.GetNode<Button>("Panel/Actions/Sell").Visible = state.CurrentRoom is MerchantRoom && BlackFlowSpireTeam.CanOperate;
        Detail.GetNode<Label>("Panel/Actions/Sell/Text").Text = BlackFlowSpireText.Get("UI.PARTS.sell_gold", ("Gold", part.Gold));
        var processed = BlackFlowSpireGenerationService.GetEngine(state).Parts.FirstOrDefault(item => item.Id == part.Id);
        Detail.GetNode<Button>("Panel/Actions/Discard").Visible = BlackFlowSpireTeam.CanOperate;
        var use = Detail.GetNode<Button>("Panel/Actions/Use");
        use.Visible = processed != null && part.Loaded && BlackFlowSpireTeam.CanOperate && BlackFlowSpireProcessedParts.Get(processed.Kind).RandomDestination;
        if (use.Visible)
        {
            var progress = BlackFlowSpireGenerationService.GetProgress(state);
            use.Disabled = _busy || inspection || !BlackFlowSpireTeam.CanTravel(state) || BlackFlowSpireGenerationService.GetEngine(state).IsOverloaded
                || progress.PendingNodeId.HasValue || progress.HuntPhase != BlackFlowSpireHuntPhase.None;
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
        if (_busy || _closed || Detail.Call("is_switching").AsBool() || !BlackFlowSpireRuntime.CanContinueRun
            || BlackFlowSpireRuntime.IsTraveling || !BlackFlowSpireTeam.CanOperate) return;
        if (closeBefore)
        {
            Close();
            Callable.From(() => { TaskHelper.RunSafely(action()); }).CallDeferred();
            return;
        }
        _busy = true;
        RefreshDetail();
        try { await action(); }
        catch (Exception error) { Entry.Logger.Error("[BlackFlowSpire] Warehouse action failed: " + error); }
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
