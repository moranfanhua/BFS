using BlackFlowSpire.Persistence;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using static BlackFlowSpire.Ui.BlackFlowSpireExpeditionScreen;

namespace BlackFlowSpire.Ui;

internal sealed class BlackFlowSpireExpeditionPicker
{
    private readonly Control _view;
    private readonly Control _canvas;
    private readonly Button _back;
    private readonly Button _confirm;
    private readonly List<(Button Button, CardModel Card)> _items = [];
    private NCard? _before;
    private NCard? _after;
    private CardModel? _selected;

    public Control DefaultFocus => _items.Count > 0 ? _items[0].Button : _back;

    public BlackFlowSpireExpeditionPicker(Control parent, Player owner, Action back, Action<int> dispatch)
    {
        _view = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireExpeditionPicker.tscn").Instantiate<Control>();
        _canvas = _view.GetNode<Control>("Canvas");
        _back = Node<Button>("Back");
        _confirm = Node<Button>("Confirm");
        var grid = Node<GridContainer>("Scroll/Cards");
        var itemScene = ResourceLoader.Load<PackedScene>(SceneFolder + "BlackFlowSpireExpeditionCard.tscn");
        foreach (var card in owner.Deck.Cards.Where(BlackFlowSpireGenerationService.CanDispatchCard))
        {
            var item = itemScene.Instantiate<Button>();
            grid.AddChild(item);
            item.GetNode<TextureRect>("Portrait").Texture = card.Portrait;
            item.GetNode<Label>("Name").Text = card.Title;
            item.Pressed += () => Select(card);
            _items.Add((item, card));
        }
        _back.Pressed += back;
        _confirm.Pressed += () =>
        {
            for (var index = 0; index < owner.Deck.Cards.Count; index++)
                if (ReferenceEquals(owner.Deck.Cards[index], _selected))
                {
                    dispatch(index);
                    return;
                }
        };
        _view.Resized += () => FitCanvas(_canvas, _view.Size);
        parent.AddChild(_view);
        FitCanvas(_canvas, _view.Size);
        RefreshLocale();
        SetBusy(false);
    }

    private T Node<T>(string path) where T : Node => _canvas.GetNode<T>(path);

    private void Select(CardModel card)
    {
        _selected = ReferenceEquals(_selected, card) ? null : card;
        ClearPreviews();
        if (_selected is { } selected)
        {
            _before = Preview(Node<Control>("Before"), selected, CardPreviewMode.Normal);
            if (selected.IsUpgradable)
            {
                var upgraded = (CardModel)selected.MutableClone();
                upgraded.UpgradeInternal();
                upgraded.UpgradePreviewType = CardUpgradePreviewType.Deck;
                _after = Preview(Node<Control>("After"), upgraded, CardPreviewMode.Upgrade);
            }
        }
        RefreshLocale();
        SetBusy(false);
    }

    private static NCard? Preview(Control anchor, CardModel card, CardPreviewMode mode)
    {
        var node = ResourceLoader.Load<PackedScene>("res://scenes/cards/card.tscn").Instantiate<NCard>();
        node.Model = card;
        node.Visibility = ModelVisibility.Visible;
        anchor.AddChild(node);
        node.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
        node.Scale = Vector2.One * 0.62f;
        node.UpdateVisuals(PileType.Deck, mode);
        return node;
    }

    private void ClearPreviews()
    {
        if (_before != null)
        {
            _before.GetParent().RemoveChild(_before);
            _before.QueueFree();
        }
        if (_after != null)
        {
            _after.GetParent().RemoveChild(_after);
            _after.QueueFree();
        }
        _before = _after = null;
    }

    public void RefreshLocale()
    {
        var selected = _selected;
        Node<Label>("CardList").Text = Text("picker.cards");
        Node<Label>("Prompt").Text = selected == null ? Text("picker.prompt") : "";
        Node<Label>("Description").Text = selected == null ? "" :
            Text(_after == null ? "picker.description" : "picker.upgrade", ("Card", selected.Title));
        _back.TooltipText = Text("picker.back");
        _confirm.TooltipText = Text(selected == null ? "picker.prompt" : "picker.confirm");
        foreach (var (button, card) in _items)
        {
            button.GetNode<Label>("Name").Text = card.Title;
            button.GetNode<Control>("Selected").Visible = ReferenceEquals(card, selected);
            button.TooltipText = card.Title;
        }
    }

    public void SetBusy(bool busy)
    {
        _back.Disabled = busy;
        _confirm.Disabled = busy || _selected == null;
        Node<Control>("Confirm/Enabled").Visible = !_confirm.Disabled;
        Node<Control>("Confirm/Disabled").Visible = _confirm.Disabled;
        foreach (var (button, _) in _items) button.Disabled = busy;
    }

    public void Close()
    {
        _view.Hide();
        _view.QueueFree();
    }
}
