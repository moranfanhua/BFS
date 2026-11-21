using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Shop;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal partial class BlackFlowSpireShopController
{
    private bool _selling;
    private int _pendingNaturalRefreshes;
    private readonly List<(BlackFlowSpireShopSaleItem Item, Button Button)> _saleGoods = [];

    public void SetSelling(bool selling)
    {
        if (_busy || _disposed || !BlackFlowSpireTeam.CanOperate || _selling == selling) return;
        _selling = selling;
        _confirmLeave = false;
        Speak(selling ? BlackFlowSpireMerchantDialogueGroup.SaleMode : BlackFlowSpireMerchantDialogueGroup.Browse);
        BindCurrentGoods();
        _view.GetNode<ScrollContainer>("Canvas/GoodsViewport").ScrollVertical = 0;
        Refresh();
        ActiveScreenContext.Instance.Update();
    }

    private void BindCurrentGoods()
    {
        if (_selling) BindSaleGoods();
        else BindGoods();
    }

    private void RefreshSellingControls()
    {
        if (_view.GetNodeOrNull<Control>("Canvas/GoodsViewport/Padding/Grid/Investment") is { } investment)
            investment.Visible = !_selling;
        _view.GetNode<Control>("Canvas/BuySell/BuyHighlight").Visible = !_selling;
        _view.GetNode<Control>("Canvas/BuySell/SellHighlight").Visible = _selling;
        _view.GetNode<CanvasItem>("Canvas/BuySell/SellIcon").Modulate = _selling ? Colors.White : new Color(0.3f, 0.3f, 0.3f);
        _view.GetNode<Label>("Canvas/BuySell/SellText").AddThemeColorOverride("font_color", _selling ? Colors.White : new Color(0.25f, 0.25f, 0.25f));
        _view.GetNode<CanvasItem>("Canvas/BuySell/BuyIcon").Modulate = _selling ? new Color(0.3f, 0.3f, 0.3f) : Colors.White;
        _view.GetNode<Label>("Canvas/BuySell/BuyText").AddThemeColorOverride("font_color", _selling ? new Color(0.25f, 0.25f, 0.25f) : Colors.White);
        _view.GetNode<Button>("Canvas/BuySell/SellButton").Disabled = _busy || !BlackFlowSpireTeam.CanOperate;
        _view.GetNode<Button>("Canvas/BuySell/BuyButton").Disabled = _busy || !BlackFlowSpireTeam.CanOperate;
        _view.GetNode<Control>("Canvas/BatchSell").Visible = _selling;
        _view.GetNode<Button>("Canvas/BatchSell").Disabled = _busy || !BlackFlowSpireTeam.CanOperate || _saleGoods.Count == 0;
        _view.GetNode<Control>("Canvas/SaleEmpty").Visible = _selling && _saleGoods.Count == 0;
        _refresh.Visible = !_selling;
        foreach (var goods in _saleGoods) goods.Button.Disabled = _busy || !BlackFlowSpireTeam.CanOperate;
    }

    private void BindSaleGoods()
    {
        ClearGoods();
        ClearSaleGoods();
        if (_inventory.Player.RunState is not RunState state) return;
        var grid = _view.GetNode<GridContainer>("Canvas/GoodsViewport/Padding/Grid");
        var scene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopGoodsTile.tscn");
        foreach (var part in BlackFlowSpireShopSaleItem.Get(state))
        {
            var button = scene.Instantiate<Button>();
            grid.AddChild(button);
            part.Bind(button);
            _saleGoods.Add((part, button));
            button.Pressed += () => TaskHelper.RunSafely(SellParts([part.Id]));
            button.MouseEntered += () => BrowseSalePart(button, part);
            button.FocusEntered += () => BrowseSalePart(button, part);
            button.MouseExited += () => NHoverTipSet.Remove(button);
            button.FocusExited += () => NHoverTipSet.Remove(button);
        }
        for (var index = 0; index < _saleGoods.Count; index++)
        {
            var button = _saleGoods[index].Button;
            button.FocusNeighborLeft = index > 0 ? _saleGoods[index - 1].Button.GetPath() : button.GetPath();
            button.FocusNeighborRight = index + 1 < _saleGoods.Count ? _saleGoods[index + 1].Button.GetPath() : button.GetPath();
            button.FocusNeighborTop = index >= 4 ? _saleGoods[index - 4].Button.GetPath() : _view.GetNode<Button>("Canvas/BatchSell").GetPath();
            button.FocusNeighborBottom = index + 4 < _saleGoods.Count ? _saleGoods[index + 4].Button.GetPath() : _leave.GetPath();
        }
    }

    internal static void ShowSaleTip(Button button, BlackFlowSpireShopSaleItem part)
    {
        NHoverTipSet.Remove(button);
        NHoverTipSet.CreateAndShow(button,
                new HoverTip(BlackFlowSpireLocalization.CreateLocString(BlackFlowSpireText.PartKey(part.IconId) + ".name"), part.Description))
            ?.SetAlignment(button, HoverTip.GetHoverTipAlignment(button));
    }

    private void ClearSaleGoods(bool removeNodes = true)
    {
        foreach (var goods in _saleGoods)
        {
            if (!GodotObject.IsInstanceValid(goods.Button)) continue;
            NHoverTipSet.Remove(goods.Button);
            if (removeNodes)
            {
                goods.Button.GetParent()?.RemoveChild(goods.Button);
                goods.Button.QueueFree();
            }
        }
        _saleGoods.Clear();
    }

    private void OpenBatchSale()
    {
        if (_busy || _disposed || _inventory.Player.RunState is not RunState state) return;
        foreach (var goods in _saleGoods) NHoverTipSet.Remove(goods.Button);
        Speak(BlackFlowSpireMerchantDialogueGroup.SaleSelection);
        NOverlayStack.Instance?.Push(new BlackFlowSpireShopBatchSaleScreen(state, SellParts) { Name = "BlackFlowSpireShopBatchSale" });
    }

    internal Task<bool> RefreshFromNaturalSale()
    {
        // A sale can trigger a refresh while payment or batch sale holds the UI
        // lock. Apply it after those sales, rather than losing the natural effect.
        if (BlackFlowSpireTeam.IsHost && _inventory.Player.RunState is RunState state && BlackFlowSpireTeam.InCommand(state))
        {
            _stock.ReadSharedStock();
            _stock.RefreshStock(false);
            return Task.FromResult(true);
        }
        if (_busy) { _pendingNaturalRefreshes++; return Task.FromResult(true); }
        return RefreshGoods(false);
    }

    private async Task<bool> SellParts(IReadOnlyList<int> ids)
    {
        if (_busy || _disposed || _inventory.Player.RunState is not RunState state) return false;
        var selected = ids.Distinct().Order().ToArray();
        var held = BlackFlowSpireShopSaleItem.Get(state).Select(part => part.Id).ToHashSet();
        if (selected.Length == 0 || selected.Any(id => !held.Contains(id))) return false;
        _busy = true;
        Refresh();
        try
        {
            if (BlackFlowSpireTeam.IsOnline)
                return await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.SellParts, items: selected);
            if (selected.Length == 1) await BlackFlowSpirePartService.Sell(selected[0]);
            else await BlackFlowSpirePartService.SellBatch(selected);
            Speak(BlackFlowSpireMerchantDialogueGroup.SaleComplete);
            return true;
        }
        catch (Exception error)
        {
            _dialogueKey = "UI.SHOP.dialogue.failed";
            Entry.Logger.Error($"[BlackFlowSpire] Part sale failed: {error}");
            return false;
        }
        finally
        {
            _busy = false;
            while (_pendingNaturalRefreshes > 0 && !_disposed)
            {
                _pendingNaturalRefreshes--;
                await RefreshGoods(false);
            }
            if (!_disposed && GodotObject.IsInstanceValid(this))
            {
                BindCurrentGoods();
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }
}
