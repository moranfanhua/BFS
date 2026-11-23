using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Map.Parts;
using BlackFlowSpire.Integration;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Shop;
using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace BlackFlowSpire.Ui;

internal static class BlackFlowSpireShopUi
{
    private const string ControllerName = "BlackFlowSpireShop";
    public static BlackFlowSpireShopController? Find(NMerchantRoom room) => room.GetNodeOrNull<BlackFlowSpireShopController>(ControllerName);

    public static void Attach(NMerchantRoom room)
    {
        BlackFlowSpireNaturalService.TraderRefreshHandler ??= RefreshFromNaturalSale;
        if (Find(room) is { } existing)
        {
            existing.Show();
            return;
        }
        if (BlackFlowSpireTeam.IsOnline && BlackFlowSpireRuntime.State is { } state)
            foreach (var player in state.Players)
                BlackFlowSpireShopInventory.Prepare(RunManager.Instance.MerchantSynchronizer.GetInventoryFor(player));
        foreach (var child in room.GetChildren().OfType<CanvasItem>()) child.Hide();
        room.AddChild(new BlackFlowSpireShopController(room) { Name = ControllerName });
        ActiveScreenContext.Instance.Update();
    }

    private static Task<bool> RefreshFromNaturalSale(RunState state) =>
        ReferenceEquals(BlackFlowSpireRuntime.State, state) && NMerchantRoom.Instance is { } room && Find(room) is { } shop
            ? shop.RefreshFromNaturalSale() : Task.FromResult(false);
}

internal partial class BlackFlowSpireShopController(NMerchantRoom room) : Control
{
    private readonly List<GoodsBinding> _goods = [];
    private Control _view = null!;
    private MerchantInventory _inventory = null!;
    private BlackFlowSpireShopInventory _stock = null!;
    private Button _leave = null!;
    private Button _refresh = null!;
    private bool _busy;
    private bool _confirmLeave;
    private bool _disposed;
    private bool _sharedStockDirty;
    private Texture2D? _removalArtwork;
    private string _dialogueKey = "UI.SHOP.DIALOGUE.BUY_SELECT.NONE.00";

    public Control DefaultFocus => _selling ? _saleGoods.FirstOrDefault().Button ?? _leave
        : _stock.IsSecret ? _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden")
        : _goods.FirstOrDefault(goods => !goods.Button.Disabled)?.Button ?? _leave;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _inventory = DisplayInventory();
        _stock = BlackFlowSpireShopInventory.Prepare(_inventory);
        _view = ResourceLoader.Load<PackedScene>(_stock.IsSecret
            ? "res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireSecretShopScreen.tscn"
            : "res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopScreen.tscn").Instantiate<Control>();
        AddChild(_view);
        _leave = _view.GetNode<Button>("Canvas/Leave");
        _leave.Pressed += RequestLeave;
        _refresh = _view.GetNode<Button>("Canvas/Refresh");
        _refresh.Pressed += () => TaskHelper.RunSafely(RefreshGoods(true));
        _view.GetNode<Button>("Canvas/BuySell/SellButton").Pressed += () => SetSelling(true);
        _view.GetNode<Button>("Canvas/BuySell/BuyButton").Pressed += () => SetSelling(false);
        _view.GetNode<Button>("Canvas/BatchSell").Pressed += OpenBatchSale;

        if (_stock.IsSecret)
            _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden").Pressed += OpenGarden;
        BindGoods();
        _inventory.Player.GoldChanged += Refresh;
        BlackFlowSpireLocalization.RefreshWithLocale(this, Refresh);
        Speak(BlackFlowSpireMerchantDialogueGroup.Browse);
        Refresh();
        if (_stock.IsSecret && _inventory.Player.RunState is RunState state && BlackFlowSpireSecretShop.GetStock(state).PendingHarvest.Count > 0)
            Callable.From(() => OpenGarden()).CallDeferred();
    }

    private static MerchantInventory DisplayInventory()
    {
        var merchant = RunManager.Instance.MerchantSynchronizer;
        if (BlackFlowSpireTeam.IsOnline && BlackFlowSpireRuntime.State is { } state
            && BlackFlowSpireGenerationService.TeamData(state).Turn.PlayerId is { } id
            && state.Players.FirstOrDefault(p => p.NetId == id) is { } buyer)
            return merchant.GetInventoryFor(buyer);
        return merchant.GetLocalInventory();
    }

    private void BindGoods()
    {
        ClearSaleGoods();
        ClearGoods();
        var grid = _view.GetNode<GridContainer>("Canvas/GoodsViewport/Padding/Grid");
        var tileScene = ResourceLoader.Load<PackedScene>("res://BlackFlowSpire/Scenes/Shop/BlackFlowSpireShopGoodsTile.tscn");
        var slots = room.Inventory.GetAllSlots().ToArray();
        _removalArtwork = slots.FirstOrDefault(slot => slot.Entry is MerchantCardRemovalEntry)
            ?.GetNodeOrNull<Sprite2D>("%Visual")?.Texture;
        IEnumerable<MerchantEntry> entries = _stock.IsSecret ? _stock.SecretParts
            : _stock.Offers.Cast<MerchantEntry>().Concat(_stock.Collectibles);
        if (_stock.Part != null) entries = entries.Prepend(_stock.Part);
        foreach (var entry in entries)
        {
            var button = tileScene.Instantiate<Button>();
            grid.AddChild(button);
            var goods = new GoodsBinding(entry, button);
            _goods.Add(goods);
            button.Pressed += () => TaskHelper.RunSafely(Buy(goods));
            button.MouseEntered += () => ShowTip(goods);
            button.MouseExited += () => NHoverTipSet.Remove(button);
            button.FocusEntered += () => ShowTip(goods);
            button.FocusExited += () => NHoverTipSet.Remove(button);
            entry.EntryUpdated += Refresh;
            entry.PurchaseCompleted += OnPurchaseCompleted;
            entry.PurchaseFailed += OnPurchaseFailed;
        }
    }

    private void Refresh()
    {
        if (_disposed || !IsInsideTree()) return;
        if (_sharedStockDirty && !_busy)
        {
            _sharedStockDirty = false;
            var display = DisplayInventory();
            if (!ReferenceEquals(_inventory, display))
            {
                _inventory.Player.GoldChanged -= Refresh;
                _inventory = display;
                _inventory.Player.GoldChanged += Refresh;
                _stock = BlackFlowSpireShopInventory.Prepare(display);
            }
            _stock.ReadSharedStock();
            BindCurrentGoods();
        }
        BlackFlowSpireLocalization.BindStaticText(_view);
        RefreshDialogue();
        _view.GetNode<Control>("Canvas/Leave/Image").Visible = !_confirmLeave;
        _view.GetNode<Control>("Canvas/Leave/Confirm").Visible = _confirmLeave;
        var waiting = WaitingForClaims;
        _leave.Disabled = _busy || !BlackFlowSpireTeam.CanOperate || waiting;
        _leave.TooltipText = waiting ? BlackFlowSpireText.Get("UI.TEAM.shop_claims") : "";
        _refresh.TooltipText = BlackFlowSpireTeam.CanOperate ? "" : BlackFlowSpireText.Get("UI.TEAM.shop");
        _view.GetNode<Button>("Canvas/BuySell/SellButton").TooltipText = _refresh.TooltipText;
        _refresh.Disabled = _busy || !BlackFlowSpireTeam.CanOperate || !_stock.CanRefresh || _inventory.Player.Gold < _stock.RefreshGold;
        _refresh.GetNode<Label>("Price/Content/Cost").Text = BlackFlowSpireText.Get("UI.COST.amount", ("Amount", _stock.RefreshGold));
        _refresh.Modulate = _refresh.Disabled ? new Color(0.65f, 0.65f, 0.65f) : Colors.White;
        RefreshSellingControls();
        if (_stock.IsSecret && _inventory.Player.RunState is RunState state)
        {
            var garden = _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden");
            garden.Disabled = _busy || !BlackFlowSpireTeam.CanOperate;
            garden.GetNode<Label>("Count").Text = BlackFlowSpireSecretShop.SeedCount(state).ToString();
            garden.Visible = !_selling;
        }
        foreach (var goods in _goods)
        {
            var entry = goods.Entry;
            var sale = false;
            switch (entry)
            {
                case BlackFlowSpireShopPartEntry part:
                    goods.Title = part.Title;
                    goods.Artwork = ResourceLoader.Load<Texture2D>(part.ArtworkPath);
                    break;
                case BlackFlowSpireShopOfferEntry offer:
                    goods.Title = offer.Title;
                    goods.Artwork = offer.Kind switch
                    {
                        BlackFlowSpireShopOfferKind.RemoveCard => _removalArtwork,
                        BlackFlowSpireShopOfferKind.UpgradeCard => ModelDb.Relic<Whetstone>().Icon,
                        BlackFlowSpireShopOfferKind.PotionPack => ModelDb.Relic<PotionBelt>().Icon,
                        _ => ResourceLoader.Load<Texture2D>(ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card.png"))
                    };
                    break;
                case BlackFlowSpireShopCollectibleEntry collectible:
                    goods.Title = collectible.Item.Title;
                    goods.Artwork = ResourceLoader.Load<Texture2D>(collectible.Item.IconPath);
                    break;
            }
            goods.Button.Disabled = _busy || !entry.IsStocked || !BlackFlowSpireTeam.CanOperate;
            goods.Button.GetNode<TextureRect>("Artwork").Texture = goods.Artwork;
            goods.Button.Call("bind_item", goods.Title, entry.IsStocked ? entry.Cost : 0, entry.IsStocked,
                entry.EnoughGold, sale);
            if (entry is BlackFlowSpireShopPartEntry partEntry)
                goods.Button.Call("bind_part", partEntry.Part.Processed.HasValue ? "processed" : partEntry.Part.Natural.HasValue ? "natural" : "concept",
                    partEntry.Part.Processed is { } processed ? Map.Parts.BlackFlowSpireProcessedParts.Get(processed).Uses : 0,
                    partEntry.Part.Processed is { } maximum ? Map.Parts.BlackFlowSpireProcessedParts.Get(maximum).Uses : 0, false);
        }
        UpdateNavigation();
    }

    internal void RefreshSharedStock()
    {
        if (!IsNodeReady() || _disposed) return;
        _sharedStockDirty = true;
        Refresh();
    }

    internal async Task<bool> RefreshGoods(bool chargeGold)
    {
        if (_busy || _disposed || !_stock.CanRefresh) return false;
        if (chargeGold && BlackFlowSpireTeam.IsOnline)
            return await BlackFlowSpireTeam.Operate((RunState)_inventory.Player.RunState, BlackFlowSpireTeamAction.ShopRefresh);
        var price = chargeGold ? _stock.RefreshGold : 0;
        if (_inventory.Player.Gold < price) return false;
        _busy = true;
        _confirmLeave = false;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        Refresh();
        try
        {
            if (price > 0)
            {
                await PlayerCmd.LoseGold(price, _inventory.Player, GoldLossType.Spent);
                RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
            }
            _stock.RefreshStock(chargeGold);
            BindCurrentGoods();
            if (chargeGold) Speak(BlackFlowSpireMerchantDialogueGroup.Browse);
            await (_stock.Part ?? _stock.SecretParts[0]).Save();
            return true;
        }
        catch (Exception error)
        {
            _dialogueKey = "UI.SHOP.dialogue.failed";
            Entry.Logger.Error($"[BlackFlowSpire] Shop refresh failed: {error}");
            return false;
        }
        finally
        {
            _busy = false;
            if (!_disposed && GodotObject.IsInstanceValid(this))
            {
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }

    private async Task Buy(GoodsBinding goods)
    {
        if (_busy || !goods.Entry.IsStocked || !BlackFlowSpireTeam.CanOperate) return;
        _busy = true;
        _confirmLeave = false;
        foreach (var item in _goods) NHoverTipSet.Remove(item.Button);
        Refresh();
        try
        {
            if (BlackFlowSpireTeam.IsOnline && _inventory.Player.RunState is RunState state)
            {
                bool bought = goods.Entry switch
                {
                    BlackFlowSpireShopOfferEntry offer => await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.ShopOffer, (int)offer.Kind),
                    BlackFlowSpireShopCollectibleEntry collectible => await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.ShopCollectible, collectible.Item.Number),
                    BlackFlowSpireShopPartEntry part => await BlackFlowSpireTeam.Operate(state, BlackFlowSpireTeamAction.ShopPart,
                        _stock.IsSecret ? _stock.SecretParts.ToList().IndexOf(part) : 0, key: part.Part.Id),
                    _ => false
                };
                if (bought) OnPurchaseCompleted(PurchaseStatus.Success, goods.Entry);
                else OnPurchaseFailed(goods.Entry.EnoughGold ? PurchaseStatus.FailureOutOfStock : PurchaseStatus.FailureGold);
                RefreshSharedStock();
                return;
            }
            if (await goods.Entry.OnTryPurchaseUnsynchronized(_inventory)
                && _inventory.Player.RunState is RunState run)
                await BlackFlowSpirePartService.SaveAndRefresh(run);
        }
        catch (Exception error)
        {
            _dialogueKey = "UI.SHOP.dialogue.failed";
            Entry.Logger.Error($"[BlackFlowSpire] Shop purchase UI failed: {error}");
        }
        finally
        {
            _busy = false;
            if (!_disposed && GodotObject.IsInstanceValid(this))
            {
                Refresh();
                ActiveScreenContext.Instance.Update();
            }
        }
    }

    private void OnPurchaseCompleted(PurchaseStatus status, MerchantEntry entry)
    {
        Speak(BlackFlowSpireMerchantDialogueGroup.Purchase);
        Refresh();
    }

    private void OnPurchaseFailed(PurchaseStatus status)
    {
        _dialogueKey = status == PurchaseStatus.FailureGold ? "UI.SHOP.dialogue.no_gold" : "UI.SHOP.dialogue.failed";
        Refresh();
    }

    private void ShowTip(GoodsBinding goods)
    {
        if (_busy || !goods.Entry.IsStocked) return;
        BrowseGoods(goods);
        // Mouse entry and keyboard focus may both fire for the same button.
        NHoverTipSet.Remove(goods.Button);
        NHoverTipSet? tip = goods.Entry switch
        {
            BlackFlowSpireShopPartEntry part => NHoverTipSet.CreateAndShow(goods.Button,
                new HoverTip(BlackFlowSpireLocalization.CreateLocString(part.NameKey), part.Description)),
            BlackFlowSpireShopOfferEntry offer => NHoverTipSet.CreateAndShow(goods.Button,
                new HoverTip(BlackFlowSpireLocalization.CreateLocString(offer.TextKey + ".title"),
                    BlackFlowSpireLocalization.CreateLocString(offer.TextKey + ".description"))),
            BlackFlowSpireShopCollectibleEntry collectible => NHoverTipSet.CreateAndShow(goods.Button,
                new HoverTip(BlackFlowSpireLocalization.CreateLocString(collectible.Item.TextKey + ".name"), collectible.Item.Description)),
            _ => null
        };
        tip?.SetAlignment(goods.Button, HoverTip.GetHoverTipAlignment(goods.Button));
    }

    private void UpdateNavigation()
    {
        var available = _goods.Where(goods => !goods.Button.Disabled).ToArray();
        foreach (var goods in available)
        {
            var index = _goods.IndexOf(goods) + 1; // Investment occupies the first grid cell.
            foreach (var (dx, dy, side) in new[] { (-1, 0, Side.Left), (1, 0, Side.Right), (0, -1, Side.Top), (0, 1, Side.Bottom) })
            {
                var candidate = available.Where(other => other != goods).Select(other =>
                    (Other: other, Index: _goods.IndexOf(other) + 1))
                    .Where(other => dx != 0 ? other.Index / 4 == index / 4 && Math.Sign(other.Index % 4 - index % 4) == dx
                        : other.Index % 4 == index % 4 && Math.Sign(other.Index / 4 - index / 4) == dy)
                    .OrderBy(other => Math.Abs(other.Index - index)).Select(other => other.Other).FirstOrDefault();
                var fallback = side == Side.Bottom ? _leave : side == Side.Left && !_refresh.Disabled ? _refresh : goods.Button;
                var path = (candidate?.Button ?? fallback).GetPath();
                switch (side)
                {
                    case Side.Left: goods.Button.FocusNeighborLeft = path; break;
                    case Side.Right: goods.Button.FocusNeighborRight = path; break;
                    case Side.Top: goods.Button.FocusNeighborTop = path; break;
                    case Side.Bottom: goods.Button.FocusNeighborBottom = path; break;
                }
            }
        }
        if (available.Length > 0)
        {
            _refresh.FocusNeighborRight = available[0].Button.GetPath();
            _refresh.FocusNeighborBottom = available[0].Button.GetPath();
            _leave.FocusNeighborLeft = available[^1].Button.GetPath();
            _leave.FocusNeighborTop = available[^1].Button.GetPath();
        }
        if (_stock.IsSecret)
        {
            var garden = _view.GetNode<Button>("Canvas/GoodsViewport/Padding/Grid/Garden");
            garden.FocusNeighborRight = (_goods.FirstOrDefault()?.Button ?? _leave).GetPath();
            garden.FocusNeighborBottom = (_goods.ElementAtOrDefault(3)?.Button ?? _leave).GetPath();
            garden.FocusNeighborLeft = _refresh.GetPath();
            if (_goods.Count > 0) _goods[0].Button.FocusNeighborLeft = garden.GetPath();
            if (_goods.Count > 3) _goods[3].Button.FocusNeighborTop = garden.GetPath();
        }
    }

    private void RequestLeave()
    {
        if (_busy || !BlackFlowSpireTeam.CanOperate || WaitingForClaims) return;
        if (_confirmLeave) Leave();
        else
        {
            _confirmLeave = true;
            Refresh();
        }
    }

    private void Leave()
    {
        if (_busy) return;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        if (BlackFlowSpireTeam.IsOnline)
            TaskHelper.RunSafely(BlackFlowSpireTeam.Operate((RunState)_inventory.Player.RunState, BlackFlowSpireTeamAction.LeaveNode));
        else NMapScreen.Instance?.Open(false);
    }

    private void OpenGarden()
    {
        if (_busy || _disposed || !BlackFlowSpireTeam.CanOperate || !_stock.IsSecret || _inventory.Player.RunState is not RunState state
            || MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack.Instance is not { } overlays) return;
        foreach (var goods in _goods) NHoverTipSet.Remove(goods.Button);
        Speak(BlackFlowSpireMerchantDialogueGroup.Browse);
        _busy = true;
        Refresh();
        SetCultivationVisible(true);
        overlays.Push(new BlackFlowSpireCultivationScreen(state, () =>
        {
            if (_disposed || !GodotObject.IsInstanceValid(this)) return;
            _busy = false;
            SetCultivationVisible(false);
            Refresh();
            ActiveScreenContext.Instance.Update();
        }) { Name = "BlackFlowSpireCultivation" });
    }

    private void SetCultivationVisible(bool cultivating)
    {
        foreach (var path in new[] { "Canvas/GoodsViewport", "Canvas/BuySell", "Canvas/Refresh", "Canvas/Leave" })
            _view.GetNode<Control>(path).Visible = !cultivating;
    }

    public override void _ExitTree()
    {
        _disposed = true;
        _inventory.Player.GoldChanged -= Refresh;
        ClearGoods(false);
        ClearSaleGoods(false);
    }

    private void ClearGoods(bool removeNodes = true)
    {
        foreach (var goods in _goods)
        {
            goods.Entry.EntryUpdated -= Refresh;
            goods.Entry.PurchaseCompleted -= OnPurchaseCompleted;
            goods.Entry.PurchaseFailed -= OnPurchaseFailed;
            if (GodotObject.IsInstanceValid(goods.Button))
            {
                NHoverTipSet.Remove(goods.Button);
                if (removeNodes)
                {
                    goods.Button.GetParent()?.RemoveChild(goods.Button);
                    goods.Button.QueueFree();
                }
            }
        }
        _goods.Clear();
    }

    private sealed class GoodsBinding(MerchantEntry entry, Button button)
    {
        public MerchantEntry Entry { get; } = entry;
        public Button Button { get; } = button;
        public string Title { get; set; } = "";
        public Texture2D? Artwork { get; set; }
    }
}
