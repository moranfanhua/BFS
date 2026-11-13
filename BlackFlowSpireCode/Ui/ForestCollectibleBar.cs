using BlackFlowSpire.Map.Collectibles;
using Godot;

namespace BlackFlowSpire.Ui;

internal sealed class ForestCollectibleBar
{
    private const int Columns = 3;
    private const int ShortRows = 3;
    private const int CardHeight = 100;
    private const int RowSpacing = 26;
    private const int ShortTopPadding = 30;
    private const int ShortBottomPadding = 60;
    private const int LongPadding = 40;
    private const float Slide = 18;
    private const double Duration = 0.16;

    private readonly Control _view;
    private readonly Button _toggle;
    private readonly Button _previews;
    private readonly HBoxContainer _icons;
    private readonly Label _count;
    private readonly Control _dismiss;
    private readonly Control _window;
    private readonly Control _panel;
    private readonly ScrollContainer _scroll;
    private readonly MarginContainer _margins;
    private readonly GridContainer _grid;
    private readonly Button _cardTemplate;
    private readonly Action<ForestCollectible> _inspect;
    private readonly HashSet<string> _ownedIds = new(StringComparer.Ordinal);
    private ForestCollectible[] _items = [];
    private bool _cardsDirty = true;
    private bool _short;
    private float _progress;
    private Tween? _transition;

    public bool IsOpen { get; private set; }

    public ForestCollectibleBar(Control view, Action toggle, Action<ForestCollectible> inspect)
    {
        _view = view;
        _inspect = inspect;
        var entry = view.GetNode<Control>("Canvas/Collectibles");
        _toggle = entry.GetNode<Button>("Toggle");
        _previews = entry.GetNode<Button>("Previews");
        _icons = _previews.GetNode<HBoxContainer>("Icons");
        _count = _toggle.GetNode<Label>("CountDisplay/Count");
        _dismiss = view.GetNode<Control>("DismissCollectibles");
        _window = view.GetNode<Control>("Canvas/CollectiblesWindow");
        _panel = _window.GetNode<Control>("Panel");
        _scroll = _panel.GetNode<ScrollContainer>("Items");
        _margins = _scroll.GetNode<MarginContainer>("Margins");
        _grid = _margins.GetNode<GridContainer>("Grid");
        _cardTemplate = view.GetNode<Button>("CollectibleCard");
        _toggle.Pressed += toggle;
        _previews.Pressed += toggle;
        _dismiss.GuiInput += OnOutsideInput;
        _window.Resized += UpdateLayout;
        _previews.Hide();
        entry.Show();
    }

    public void Refresh(IReadOnlySet<string> ownedIds)
    {
        if (_ownedIds.SetEquals(ownedIds)) return;
        _ownedIds.Clear();
        _ownedIds.UnionWith(ownedIds);
        _items = ForestCollectibleCatalog.All.Where(item => _ownedIds.Contains(item.Id)).ToArray();
        _count.Text = _items.Length.ToString();
        var hasItems = _items.Length > 0;
        _toggle.Disabled = !hasItems;
        _toggle.MouseDefaultCursorShape = hasItems ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;

        ClearChildren(_icons);
        var slot = ForestMapAssets.Texture("res://BlackFlowSpire/Images/Ui/Collectibles/slot.png");
        var size = new Vector2(54, 54);
        var spacing = _icons.GetThemeConstant("separation");
        var capacity = (int)((_previews.Size.X - _icons.Position.X + spacing) / (size.X + spacing));
        foreach (var item in _items.Take(capacity))
        {
            var icon = ForestMapAssets.Image(slot, size);
            icon.CustomMinimumSize = size;
            _icons.AddChild(icon);
            icon.AddChild(ForestMapAssets.Image(ForestMapAssets.Texture(item.IconPath), size));
        }
        _previews.Visible = hasItems;
        if (!hasItems) Close();
        RefreshText();
    }

    public void RefreshText()
    {
        _cardsDirty = true;
        if (IsOpen) RefreshCards();
    }

    public void Open()
    {
        if (_items.Length == 0) return;
        IsOpen = true;
        RefreshCards();
        _scroll.ScrollVertical = 0;
        _window.Show();
        _dismiss.Show();
        SetEntryExpanded(true);
        ApplyProgress(_progress);
        AnimateTo(1);
        if (_grid.GetChildCount() > 0) _grid.GetChild<Button>(0).GrabFocus();
    }

    public void Close(bool immediate = false)
    {
        if (!IsOpen && (!immediate || !_window.Visible)) return;
        var focus = _view.GetViewport().GuiGetFocusOwner();
        if (focus != null && _window.IsAncestorOf(focus))
        {
            if (immediate || _toggle.Disabled) focus.ReleaseFocus();
            else _toggle.GrabFocus();
        }

        IsOpen = false;
        SetEntryExpanded(false);
        if (!immediate)
        {
            AnimateTo(0);
            return;
        }

        _transition?.Kill();
        FinishClosing();
    }

    private void OnOutsideInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dismiss.AcceptEvent();
            Close();
        }
    }

    private void RefreshCards()
    {
        if (!_cardsDirty) return;
        _cardsDirty = false;
        var focused = _view.GetViewport().GuiGetFocusOwner();
        var focusedIndex = focused?.GetParent() == _grid ? focused.GetIndex() : -1;
        ClearChildren(_grid);
        foreach (var item in _items)
        {
            var card = (Button)_cardTemplate.Duplicate(0);
            card.GetNode<TextureRect>("Icon").Texture = ForestMapAssets.Texture(item.IconPath);
            card.GetNode<Label>("Title").Text = item.Title;
            card.GetNode<Label>("Effect").Text = item.Description;
            card.Pressed += () => { if (IsOpen) _inspect(item); };
            _grid.AddChild(card);
            card.Show();
        }
        if (focusedIndex >= 0 && _grid.GetChildCount() > 0)
            _grid.GetChild<Button>(Math.Min(focusedIndex, _grid.GetChildCount() - 1)).GrabFocus();
        _short = _items.Length <= Columns * ShortRows;
        _panel.GetNode<Control>("Arrow").Visible = _short;
        _panel.GetNode<Control>("Background/Gradient").Visible = !_short;
        _scroll.VerticalScrollMode = _short ? ScrollContainer.ScrollMode.Disabled : ScrollContainer.ScrollMode.ShowNever;
        _margins.AddThemeConstantOverride("margin_top", _short ? ShortTopPadding : LongPadding);
        _margins.AddThemeConstantOverride("margin_bottom", _short ? ShortBottomPadding : LongPadding);
        ApplyProgress(_progress);
    }

    private void UpdateLayout()
    {
        var rows = Math.Max(1, (_items.Length + Columns - 1) / Columns);
        var shortHeight = ShortTopPadding + rows * CardHeight + (rows - 1) * RowSpacing + ShortBottomPadding;
        _panel.OffsetTop = _panel.OffsetBottom - (_short ? shortHeight : _window.Size.Y);
    }

    private void SetEntryExpanded(bool expanded)
    {
        _toggle.GetNode<Control>("CountDisplay").Visible = !expanded;
        _toggle.GetNode<Control>("Collapse").Visible = expanded;
    }

    private void AnimateTo(float target)
    {
        _transition?.Kill();
        _transition = _view.CreateTween().SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        _transition.TweenMethod(Callable.From<float>(ApplyProgress), _progress, target, Duration);
        if (target == 0) _transition.TweenCallback(Callable.From(FinishClosing));
    }

    // The clip's alpha and Y tracks are linear, with easing applied by the tween.
    private void ApplyProgress(float progress)
    {
        _progress = progress;
        _window.Modulate = new Color(1, 1, 1, progress);
        var offset = Slide * (1 - progress);
        _panel.OffsetBottom = _short ? offset : 0;
        UpdateLayout();
        _scroll.OffsetTop = _short ? 0 : offset;
        _scroll.OffsetBottom = _scroll.OffsetTop;
    }

    private void FinishClosing()
    {
        ApplyProgress(0);
        _window.Hide();
        _dismiss.Hide();
    }

    private static void ClearChildren(Control parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
