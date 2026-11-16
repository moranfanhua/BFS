using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Collectibles;
using BlackFlowSpire.Map.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal sealed partial class BlackFlowSpireCollectibleScreen(RunState run, BlackFlowSpireCollectible? detail = null) : Control, IScreenContext
{
    private const string TextKey = "UI.COLLECTIBLES.";
    private Label _title = null!;
    private Label _status = null!;
    private Label _id = null!;
    private Label _name = null!;
    private Label _description = null!;
    private Label _flavor = null!;
    private TextureRect _art = null!;
    private VBoxContainer _list = null!;
    private LineEdit _search = null!;
    private Button _owned = null!;
    private Button _all = null!;
    private Button _close = null!;
    private string? _selected;
    private bool _ownedOnly;

    public bool IsOwnedDetail => detail != null;
    public Control? DefaultFocusedControl => IsOwnedDetail ? _close : _search;

    internal static void Open(RunState run)
    {
        if (BlackFlowSpireRuntime.CanContinueRun && !BlackFlowSpireRuntime.IsTraveling
            && NModalContainer.Instance is { OpenModal: null } container)
            container.Add(new BlackFlowSpireCollectibleScreen(run) { Name = "BlackFlowSpireCollectibles" });
    }

    internal static void OpenOwnedDetail(RunState run, BlackFlowSpireCollectible item)
    {
        if (ReferenceEquals(BlackFlowSpireRuntime.State, run) && BlackFlowSpireRuntime.CanContinueRun && !BlackFlowSpireRuntime.IsTraveling
            && NModalContainer.Instance is { OpenModal: null } container
            && BlackFlowSpireCollectibleService.GetInventory(run).OwnedIds.Contains(item.Id))
            container.Add(new BlackFlowSpireCollectibleScreen(run, item) { Name = "BlackFlowSpireCollectibleDetail" });
    }

    internal static void RefreshShared()
    {
        if (NModalContainer.Instance?.OpenModal is BlackFlowSpireCollectibleScreen screen)
            screen.Refresh();
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _ownedOnly = BlackFlowSpireCollectibleService.GetInventory(run).OwnedIds.Count > 0;
        var panel = new PanelContainer
        {
            AnchorLeft = 0.13f, AnchorTop = 0.13f, AnchorRight = 0.87f, AnchorBottom = 0.87f,
            MouseFilter = MouseFilterEnum.Stop
        };
        var frame = Style("111c23");
        frame.ContentMarginLeft = 28;
        frame.ContentMarginRight = 28;
        frame.ContentMarginTop = 22;
        frame.ContentMarginBottom = 22;
        panel.AddThemeStyleboxOverride("panel", frame);
        AddChild(panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        panel.AddChild(body);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        body.AddChild(header);
        _title = LabelAt(28);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        if (!IsOwnedDetail)
        {
            _owned = AddButton(header, () => { _ownedOnly = true; Refresh(); });
            _all = AddButton(header, () => { _ownedOnly = false; Refresh(); });
        }
        _close = AddButton(header, Close);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 24);
        body.AddChild(columns);
        if (!IsOwnedDetail) AddCatalogList(columns);

        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        columns.AddChild(detailScroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 10);
        detailScroll.AddChild(details);
        _art = new TextureRect
        {
            CustomMinimumSize = new(140, 140), SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        details.AddChild(_art);
        _name = LabelAt(23);
        details.AddChild(_name);
        _status = LabelAt(16);
        _status.Modulate = new Color("9cc5c4");
        details.AddChild(_status);
        _id = LabelAt(15);
        _id.Modulate = new Color("a9bac0");
        details.AddChild(_id);
        _description = LabelAt(18);
        details.AddChild(_description);
        _flavor = LabelAt(17);
        _flavor.Modulate = new Color("a9bac0");
        details.AddChild(_flavor);

        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Refresh();
    }

    private void AddCatalogList(Container columns)
    {
        var browse = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f };
        columns.AddChild(browse);
        _search = new LineEdit { ClearButtonEnabled = true };
        _search.AddThemeFontOverride("font", BlackFlowSpireFonts.Body);
        _search.AddThemeFontSizeOverride("font_size", 17);
        browse.AddChild(_search);
        _search.TextChanged += _ => Refresh();
        var rows = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        browse.AddChild(rows);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddChild(_list);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed(MegaInput.cancel) && !input.IsActionPressed(MegaInput.back)) return;
        Close();
        GetViewport().SetInputAsHandled();
    }

    private void Close()
    {
        if (NModalContainer.Instance is { } container && ReferenceEquals(container.OpenModal, this))
            container.Clear();
    }

    private void Refresh()
    {
        var inventory = BlackFlowSpireCollectibleService.GetInventory(run);
        _title.Text = BlackFlowSpireText.Get(TextKey + "title");
        _close.Text = BlackFlowSpireText.Get(TextKey + "close");
        if (detail != null)
        {
            Show(detail, inventory);
            return;
        }

        _owned.Text = BlackFlowSpireText.Get(TextKey + "owned") + $" ({inventory.OwnedIds.Count})";
        _all.Text = BlackFlowSpireText.Get(TextKey + "all");
        _owned.Disabled = _ownedOnly;
        _all.Disabled = !_ownedOnly;
        _search.PlaceholderText = BlackFlowSpireText.Get(TextKey + "search");
        var query = _search.Text.Trim();
        var items = BlackFlowSpireCollectibleCatalog.All.Where(item => (!_ownedOnly || inventory.OwnedIds.Contains(item.Id))
            && (query.Length == 0 || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Number.ToString("000").Contains(query, StringComparison.Ordinal))).ToArray();
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        var group = new ButtonGroup();
        var selected = items.FirstOrDefault(item => item.Id == _selected) ?? items.FirstOrDefault();
        foreach (var item in items)
        {
            var button = AddButton(_list, () => Show(item, inventory));
            button.Text = $"{item.Number:000}  {item.Title}";
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.Alignment = HorizontalAlignment.Left;
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.ButtonPressed = item == selected;
        }
        Show(selected, inventory);
    }

    private void Show(BlackFlowSpireCollectible? item, BlackFlowSpireCollectibleInventory inventory)
    {
        _selected = item?.Id;
        _art.Texture = item == null ? null : ResourceLoader.Load<Texture2D>(item.IconPath);
        _art.Visible = item != null;
        _name.Text = item?.Title ?? BlackFlowSpireText.Get(TextKey + "empty");
        _status.Text = item == null ? "" : BlackFlowSpireText.Get(TextKey + (inventory.OwnedIds.Contains(item.Id) ? "owned"
            : BlackFlowSpireCollectibleRules.CanAcquire(item) ? "available" : "reference"));
        _id.Text = item?.Id ?? "";
        _description.Text = item?.Description ?? "";
        _flavor.Text = item?.Flavor ?? "";
    }

    private static Button AddButton(Container parent, Action press)
    {
        var button = new Button { CustomMinimumSize = new(72, 32), MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeFontOverride("font", BlackFlowSpireFonts.Body);
        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeStyleboxOverride("normal", Style("20313b"));
        button.AddThemeStyleboxOverride("hover", Style("315563"));
        button.AddThemeStyleboxOverride("pressed", Style("315563"));
        button.Pressed += press;
        parent.AddChild(button);
        return button;
    }

    private static Label LabelAt(int size)
    {
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontOverride("font", BlackFlowSpireFonts.Body);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static StyleBoxFlat Style(string color) => new()
    {
        BgColor = new Color(color), BorderColor = new Color("56747e"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 10, ContentMarginRight = 10
    };
}
