using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Exploration;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Map.Presentation;
using Godot;
using static BlackFlowSpire.Ui.BlackFlowSpireSacrificeScreen;

namespace BlackFlowSpire.Ui;

internal sealed class BlackFlowSpireSacrificePicker
{
    private const string SceneFolder = "res://BlackFlowSpire/Scenes/Events/";
    private const string ImageFolder = "res://BlackFlowSpire/Images/Events/Sacrifice/";
    private static readonly Vector2 Design = new(1422.2222f, 800);
    // Top-left coordinates in the 180-unit sacrifice_item_icon prefab, including the part_star scale.
    private static readonly Vector2[] StarPositions = [new(134.35f, 9.33f), new(146.39f, 18.45f),
        new(151.42f, 36.81f), new(149.55f, 56.82f), new(147.33f, 77.87f)];

    private readonly Control _view;
    private readonly Control _canvas;
    private readonly Button _back;
    private readonly Button _confirm;
    private readonly Control _detailIcon;
    private readonly IReadOnlyList<BlackFlowSpireSacrificeOffer> _offers;
    private readonly bool _parts;
    private readonly Button[] _items;
    private BlackFlowSpireSacrificeOffer? _selected;
    private Control? _judge;
    private Button? _judgeBack;

    public Control DefaultFocus => _judgeBack ?? (_items.Length > 0 ? _items[0] : _back);

    public BlackFlowSpireSacrificePicker(Control parent, IReadOnlyList<BlackFlowSpireSacrificeOffer> offers, bool parts,
        Action back, Action<int> confirm)
    {
        _parts = parts;
        _offers = offers;
        _view = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireSacrificePicker.tscn").Instantiate<Control>();
        _canvas = _view.GetNode<Control>("Canvas");
        _back = Node<Button>("Back");
        _confirm = Node<Button>("Confirm");
        _detailIcon = Node<Control>("SelectedIcon");
        _back.Pressed += back;
        _confirm.Pressed += () =>
        {
            if (_selected is not { } selected) return;
            if (selected.Loaded) OpenJudge(() => confirm(selected.Argument));
            else confirm(selected.Argument);
        };
        var grid = Node<GridContainer>("Scroll/Padding/Items");
        var itemScene = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireSacrificeItem.tscn");
        _items = offers.Select(offer =>
        {
            var item = itemScene.Instantiate<Button>();
            grid.AddChild(item);
            AddIcon(item.GetNode<Control>("Icon"), offer, detail: false);
            item.GetNode<Control>("Loaded").Visible = offer.Loaded;
            item.GetNode<Label>("LoadedText").Visible = offer.Loaded;
            item.Pressed += () => Select(_selected == offer ? null : offer);
            return item;
        }).ToArray();
        _view.Resized += Layout;
        parent.AddChild(_view);
        Layout();
        Refresh();
    }

    private T Node<T>(string path) where T : Node => _canvas.GetNode<T>(path);

    private void Layout()
    {
        var factor = Math.Min(_view.Size.X / Design.X, _view.Size.Y / Design.Y);
        _canvas.Scale = Vector2.One * factor;
        _canvas.Position = (_view.Size - Design * factor) / 2;
    }

    private void Select(BlackFlowSpireSacrificeOffer? offer)
    {
        _selected = offer;
        foreach (var child in _detailIcon.GetChildren())
        {
            _detailIcon.RemoveChild(child);
            child.QueueFree();
        }
        if (offer != null) AddIcon(_detailIcon, offer, detail: true);
        Refresh();
    }

    internal bool Parts => _parts;
    internal int? SelectedItem => _selected?.Argument;
    internal bool ConfirmingLoaded => _judge != null;
    internal int Scroll => Node<ScrollContainer>("Scroll").ScrollVertical;

    internal void Follow(int? argument, bool judge, int scroll)
    {
        var offer = argument.HasValue ? _offers.FirstOrDefault(item => item.Argument == argument.Value) : null;
        if (_selected != offer)
        {
            CancelJudge();
            Select(offer);
        }
        if (judge && offer is { Loaded: true }) OpenJudge(() => { });
        else CancelJudge();
        if (Scroll != scroll) Node<ScrollContainer>("Scroll").ScrollVertical = Math.Max(0, scroll);
    }

    public void Refresh()
    {
        Node<Control>("PartHeader").Visible = _parts;
        Node<Control>("RelicHeader").Visible = !_parts;
        Node<Control>("RelicHeader").TooltipText = Text("picker.relics");
        Node<Control>("PartHeader").TooltipText = Text("picker.parts");
        Node<Control>("EmptyRelic").Visible = !_parts && _selected == null;
        Node<Control>("EmptyPart").Visible = _parts && _selected == null;
        Node<Label>("Chosen").Text = Text("picker.chosen");
        Node<Label>("ListHint").Text = Text(_parts ? "picker.part_hint" : "picker.relic_hint");
        Node<Label>("ItemName").Text = _selected == null ? Text("picker.none") : NameOf(_selected);
        Node<RichTextLabel>("ItemDescription").Text = _selected == null ? Text("picker.prompt") : DescriptionOf(_selected);
        var appraisal = _selected?.Appraisal;
        Node<Control>("AppraisalPlate").Visible = appraisal.HasValue;
        Node<Control>("AppraisalIcon").Visible = appraisal.HasValue;
        Node<Label>("Appraisal").Visible = appraisal.HasValue;
        Node<Label>("Appraisal").Text = appraisal?.ToString() ?? "";
        for (var i = 0; i < _items.Length; i++)
        {
            var offer = _offers[i];
            _items[i].GetNode<Control>("Selected").Visible = _selected == offer;
            _items[i].GetNode<Label>("LoadedText").Text = Text("picker.loaded");
            _items[i].TooltipText = NameOf(offer);
        }
        _back.TooltipText = Text("picker.back");
        _confirm.TooltipText = Text(_selected == null ? "picker.prompt" : "picker.confirm");
        RefreshJudge();
        SetBusy(false);
    }

    public void SetBusy(bool busy)
    {
        _back.Disabled = busy || _judge != null;
        _confirm.Disabled = busy || _judge != null || _selected == null;
        _confirm.GetNode<Control>("Artwork").Visible = _selected != null;
        _confirm.GetNode<Control>("DisabledArtwork").Visible = _selected == null;
        foreach (var button in _items) button.Disabled = busy || _judge != null;
    }

    private void OpenJudge(Action approved)
    {
        if (_judge != null) return;
        _judge = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireSacrificeJudge.tscn").Instantiate<Control>();
        _canvas.AddChild(_judge);
        _judgeBack = _judge.GetNode<Button>("Back");
        _judgeBack.Pressed += () => CancelJudge();
        var confirm = _judge.GetNode<Button>("Confirm");
        confirm.Pressed += () =>
        {
            CancelJudge();
            approved();
        };
        RefreshJudge();
        SetBusy(false);
        if (!BlackFlowSpireTeam.IsGuest) _judgeBack.GrabFocus();
    }

    private void RefreshJudge()
    {
        if (_judge == null || _selected == null) return;
        _judge.GetNode<Label>("Prompt").Text = Text("picker.loaded_prompt", ("Item", NameOf(_selected)));
        _judge.GetNode<Label>("Back/Caption").Text = Text("picker.cancel");
        _judge.GetNode<Label>("Confirm/Caption").Text = Text("picker.confirm_short");
    }

    public bool CancelJudge()
    {
        if (_judge == null) return false;
        _judge.Hide();
        _judge.QueueFree();
        _judge = null;
        _judgeBack = null;
        SetBusy(false);
        if (!BlackFlowSpireTeam.IsGuest) _confirm.GrabFocus();
        return true;
    }

    public void Close()
    {
        _view.Hide();
        _view.QueueFree();
    }

    private static string NameOf(BlackFlowSpireSacrificeOffer offer) => offer.Kind == BlackFlowSpireSacrificeKind.Collectible
        ? offer.Collectible!.Title : BlackFlowSpireText.Get(BlackFlowSpireText.PartKey(offer.IconId) + ".name");

    private static string DescriptionOf(BlackFlowSpireSacrificeOffer offer) => offer.Kind switch
    {
        BlackFlowSpireSacrificeKind.Collectible => offer.Collectible!.Description,
        BlackFlowSpireSacrificeKind.Natural => BlackFlowSpirePartsUi.NaturalDescription((BlackFlowSpireNaturalPartKind)offer.SortId),
        BlackFlowSpireSacrificeKind.Processed => BlackFlowSpirePartsUi.Description((BlackFlowSpireProcessedPartKind)offer.SortId),
        BlackFlowSpireSacrificeKind.Concept => BlackFlowSpirePartsUi.ConceptDescription((BlackFlowSpireConceptPartKind)offer.SortId),
        _ => throw new ArgumentOutOfRangeException(nameof(offer))
    };

    private static void AddIcon(Control holder, BlackFlowSpireSacrificeOffer offer, bool detail)
    {
        var scale = holder.Size.X / 180f;
        if (offer.Kind == BlackFlowSpireSacrificeKind.Collectible)
        {
            AddTexture(holder, ResourceLoader.Load<Texture2D>(offer.Collectible!.IconPath), Vector2.Zero, new(180, 180), scale);
            return;
        }

        var category = offer.Kind.ToString().ToLowerInvariant();
        var ring = ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Shop/part-{category}-ring.png");
        var type = ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Shop/part-{category}-type.png");
        AddTexture(holder, ring, new(-30, -30), new(240, 240), scale);
        var artworkSize = detail ? 180f : 144f;
        var artworkInset = (180f - artworkSize) / 2f;
        AddTexture(holder, PartArtwork(offer.IconId), Vector2.One * artworkInset, Vector2.One * artworkSize, scale);
        AddTexture(holder, type, new(-0.6f, -1.3f), new(64, 64), scale);
        var maxUses = offer.Kind switch
        {
            BlackFlowSpireSacrificeKind.Processed => BlackFlowSpireProcessedParts.Get((BlackFlowSpireProcessedPartKind)offer.SortId).Uses,
            BlackFlowSpireSacrificeKind.Concept => BlackFlowSpireConceptParts.Get((BlackFlowSpireConceptPartKind)offer.SortId).Activations ?? 0,
            _ => 0
        };
        for (var i = 0; i < Math.Min(maxUses, StarPositions.Length); i++)
            AddTexture(holder, Asset(i < offer.Uses ? "move_mode_icon_times" : "move_mode_icon_times_empty"),
                StarPositions[i], new(18.72f, 18.72f), scale);
    }

    private static void AddTexture(Control parent, Texture2D texture, Vector2 position, Vector2 size, float scale) =>
        parent.AddChild(new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = texture, Position = position * scale, Size = size * scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

    internal static Texture2D Asset(string name) => ResourceLoader.Load<Texture2D>(ImageFolder + name + ".png");
    internal static Texture2D PartArtwork(string iconId) => ResourceLoader.Load<Texture2D>($"res://BlackFlowSpire/Images/Parts/{iconId}.png");
}
